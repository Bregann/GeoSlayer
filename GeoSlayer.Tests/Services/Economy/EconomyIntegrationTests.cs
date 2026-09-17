using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Exceptions;
using GeoSlayer.Domain.Services.Economy;
using GeoSlayer.Domain.Services.Materials;
using GeoSlayer.Domain.Services.Progression;
using GeoSlayer.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace GeoSlayer.Tests.Services.Economy
{
    /// <summary>
    /// Selling and banking against a real database (DESIGN.md §5.4, §5.4a).
    /// </summary>
    [TestFixture]
    public class EconomyIntegrationTests : DatabaseIntegrationTestBase
    {
        private const double OriginLat = 51.5074;
        private const double OriginLng = -0.1278;

        private EconomyService _sut = null!;
        private MaterialService _materials = null!;
        private ProgressionService _progression = null!;
        private Player _player = null!;

        private static CancellationToken Ct => CancellationToken.None;

        private long _nextOsmId = 7_000_000;

        protected override async Task CustomSetUp()
        {
            var (_, player) = await TestDatabaseSeedHelper.SeedMinimalData(DbContext);
            _player = player;

            await TestDatabaseSeedHelper.SeedProgressionDefinitions(DbContext);
            await TestDatabaseSeedHelper.SeedMaterialDefinitions(DbContext);
            await TestDatabaseSeedHelper.SeedSkillDefinitions(DbContext);
            await TestDatabaseSeedHelper.SeedMuseumDefinitions(DbContext);

            _progression = TestDatabaseSeedHelper.CreateProgressionService(DbContext);
            await _progression.EnsureStartingUnlocks(_player.Id, Ct);

            _materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext, TerrainType.Urban);
            _sut = TestDatabaseSeedHelper.CreateEconomyService(DbContext, _progression, _materials);

            await AtOrigin();
        }

        private async Task AtOrigin()
        {
            _player.LastLatitude = OriginLat;
            _player.LastLongitude = OriginLng;
            _player.LastSyncAtUtc = DateTime.UtcNow;
            await DbContext.SaveChangesAsync();
        }

        private async Task<PointOfInterest> AddPoi(
            SkillType skill, double latOffset = 0.0001, string name = "The Shop")
        {
            var poi = new PointOfInterest
            {
                Name = name,
                Skill = skill,
                OsmId = _nextOsmId++,
                OsmType = "node",
                Location = new Point(OriginLng, OriginLat + latOffset) { SRID = 4326 },
                XpReward = 10,
            };

            DbContext.PointsOfInterest.Add(poi);
            await DbContext.SaveChangesAsync();

            return poi;
        }

        private async Task<Material> Give(string key, long quantity)
        {
            var material = await DbContext.Materials.FirstAsync(m => m.Key == key);

            var row = await DbContext.PlayerMaterials
                .FirstOrDefaultAsync(pm => pm.PlayerId == _player.Id && pm.MaterialId == material.Id);

            if (row is null)
            {
                row = new PlayerMaterial { PlayerId = _player.Id, MaterialId = material.Id };
                DbContext.PlayerMaterials.Add(row);
            }

            row.Quantity = quantity;
            await DbContext.SaveChangesAsync();

            return material;
        }

        private async Task<long> Held(int materialId) =>
            await DbContext.PlayerMaterials
                .Where(pm => pm.PlayerId == _player.Id && pm.MaterialId == materialId)
                .Select(pm => pm.Quantity)
                .FirstOrDefaultAsync();

        // ── Selling pays, and removes what was sold ─────────────────────

        [Test]
        public async Task SellingAtAShop_PaysCoinAndTakesTheMaterials()
        {
            var shop = await AddPoi(SkillType.Trading);
            var scrap = await Give("scrap", 100);

            var result = await _sut.Sell(
                _player.Id, shop.Id, new Dictionary<int, long> { [scrap.Id] = 40 }, Ct);

            Assert.Multiple(async () =>
            {
                Assert.That(result.CoinEarned, Is.GreaterThan(0));
                Assert.That(result.CoinBalance, Is.EqualTo(result.CoinEarned));
                Assert.That(await Held(scrap.Id), Is.EqualTo(60), "only what was sold leaves");
                Assert.That(result.PoiName, Is.EqualTo("The Shop"));
            });
        }

        [Test]
        public async Task SellingMoreThanHeld_SellsWhatYouHave()
        {
            // A stale inventory screen is a phone's normal state. Failing the whole sale over
            // it would punish the player for our latency.
            var shop = await AddPoi(SkillType.Trading);
            var scrap = await Give("scrap", 10);

            var result = await _sut.Sell(
                _player.Id, shop.Id, new Dictionary<int, long> { [scrap.Id] = 999 }, Ct);

            Assert.Multiple(async () =>
            {
                Assert.That(result.Sold.Single().Quantity, Is.EqualTo(10));
                Assert.That(await Held(scrap.Id), Is.Zero);
            });
        }

        [Test]
        public async Task SellingAccumulatesCoin_AcrossTrips()
        {
            var shop = await AddPoi(SkillType.Trading);
            var scrap = await Give("scrap", 100);

            var first = await _sut.Sell(
                _player.Id, shop.Id, new Dictionary<int, long> { [scrap.Id] = 10 }, Ct);
            var second = await _sut.Sell(
                _player.Id, shop.Id, new Dictionary<int, long> { [scrap.Id] = 10 }, Ct);

            Assert.That(second.CoinBalance, Is.EqualTo(first.CoinEarned + second.CoinEarned));
        }

        // ── Selling is a place, not a menu (§7.2) ───────────────────────

        [Test]
        public async Task SellingFromOutOfRange_IsRejected()
        {
            // The rule the whole design rests on: coin is earned by walking somewhere.
            var shop = await AddPoi(SkillType.Trading, latOffset: 0.05);
            var scrap = await Give("scrap", 10);

            Assert.That(
                async () => await _sut.Sell(
                    _player.Id, shop.Id, new Dictionary<int, long> { [scrap.Id] = 10 }, Ct),
                Throws.TypeOf<BadRequestException>());

            Assert.That(await Held(scrap.Id), Is.EqualTo(10), "nothing may leave on a failed sale");
        }

        [Test]
        public async Task SellingWithNoVerifiedPosition_IsRejected()
        {
            // The client never supplies a position. Without a completed sync there is nothing
            // trustworthy to check against, so the answer is no.
            var shop = await AddPoi(SkillType.Trading);
            var scrap = await Give("scrap", 10);

            _player.LastSyncAtUtc = null;
            await DbContext.SaveChangesAsync();

            Assert.That(
                async () => await _sut.Sell(
                    _player.Id, shop.Id, new Dictionary<int, long> { [scrap.Id] = 10 }, Ct),
                Throws.TypeOf<BadRequestException>());
        }

        [Test]
        public async Task SellingAtANonShop_IsRejected()
        {
            // A church does not buy your ore.
            var church = await AddPoi(SkillType.Prayer, name: "St Mary's");
            var scrap = await Give("scrap", 10);

            Assert.That(
                async () => await _sut.Sell(
                    _player.Id, church.Id, new Dictionary<int, long> { [scrap.Id] = 10 }, Ct),
                Throws.TypeOf<BadRequestException>());
        }

        // ── The junk shortcut ───────────────────────────────────────────

        [Test]
        public async Task SellJunk_SellsLowTierAndLeavesTheRest()
        {
            var shop = await AddPoi(SkillType.Trading);

            var junk = await DbContext.Materials.FirstAsync(m => m.Tier <= 2 && m.Category != MaterialCategory.Relic);
            var keeper = await DbContext.Materials.FirstAsync(m => m.Tier >= 4);

            await Give(junk.Key, 50);
            await Give(keeper.Key, 50);

            var result = await _sut.SellJunk(_player.Id, shop.Id, Ct);

            Assert.Multiple(async () =>
            {
                Assert.That(result.CoinEarned, Is.GreaterThan(0));
                Assert.That(await Held(junk.Id), Is.Zero, "junk should go");
                Assert.That(await Held(keeper.Id), Is.EqualTo(50), "anything worth saving stays");
            });
        }

        [Test]
        public async Task SellJunk_NeverSellsARelic()
        {
            // Relics are Museum pieces (§5A.1). Losing one to a bulk action is the exact
            // annoyance the guard exists for.
            var shop = await AddPoi(SkillType.Trading);

            var relic = await DbContext.Materials
                .FirstAsync(m => m.Category == MaterialCategory.Relic);

            await Give(relic.Key, 3);

            await _sut.SellJunk(_player.Id, shop.Id, Ct);

            Assert.That(await Held(relic.Id), Is.EqualTo(3));
        }

        [Test]
        public async Task SellJunk_WithNothingToSell_IsHarmless()
        {
            var shop = await AddPoi(SkillType.Trading);

            var result = await _sut.SellJunk(_player.Id, shop.Id, Ct);

            Assert.That(result.CoinEarned, Is.Zero);
            Assert.That(result.Sold, Is.Empty);
        }

        // ── The sell bonus is read (§4.3) ───────────────────────────────

        [Test]
        public async Task BankingLevel_RaisesWhatASaleFetches()
        {
            var shop = await AddPoi(SkillType.Trading);
            await Give("scrap", 10_000);

            var scrap = await DbContext.Materials.FirstAsync(m => m.Key == "scrap");

            var before = await _sut.Sell(
                _player.Id, shop.Id, new Dictionary<int, long> { [scrap.Id] = 1000 }, Ct);

            DbContext.PlayerSkills.Add(new PlayerSkill
            {
                PlayerId = _player.Id,
                SkillType = SkillType.Banking,
                Level = 99,
                UnlockedAtUtc = DateTime.UtcNow,
            });
            await DbContext.SaveChangesAsync();

            var after = await _sut.Sell(
                _player.Id, shop.Id, new Dictionary<int, long> { [scrap.Id] = 1000 }, Ct);

            Assert.That(after.CoinEarned, Is.GreaterThan(before.CoinEarned));
        }

        // ── Banking (§5.4a) ─────────────────────────────────────────────

        [Test]
        public async Task DepositAndWithdraw_MoveCoinBothWays()
        {
            var bank = await AddPoi(SkillType.Banking, name: "The Bank");

            _player.Coin = 1000;
            await DbContext.SaveChangesAsync();

            var afterDeposit = await _sut.Deposit(_player.Id, bank.Id, 600, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(afterDeposit.Coin, Is.EqualTo(400));
                Assert.That(afterDeposit.Deposited, Is.EqualTo(600));
            });

            var afterWithdraw = await _sut.Withdraw(_player.Id, bank.Id, 600, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(afterWithdraw.Coin, Is.EqualTo(1000));
                Assert.That(afterWithdraw.Deposited, Is.Zero);
            });
        }

        [Test]
        public async Task DepositingMoreThanHeld_DepositsWhatYouHave()
        {
            var bank = await AddPoi(SkillType.Banking);

            _player.Coin = 100;
            await DbContext.SaveChangesAsync();

            var account = await _sut.Deposit(_player.Id, bank.Id, 999_999, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(account.Deposited, Is.EqualTo(100));
                Assert.That(account.Coin, Is.Zero);
            });
        }

        [Test]
        public async Task BankingAtANonBank_IsRejected()
        {
            var shop = await AddPoi(SkillType.Trading);

            _player.Coin = 1000;
            await DbContext.SaveChangesAsync();

            Assert.That(
                async () => await _sut.Deposit(_player.Id, shop.Id, 100, Ct),
                Throws.TypeOf<BadRequestException>());
        }

        [Test]
        public async Task BankingFromOutOfRange_IsRejected()
        {
            var bank = await AddPoi(SkillType.Banking, latOffset: 0.05);

            _player.Coin = 1000;
            await DbContext.SaveChangesAsync();

            Assert.That(
                async () => await _sut.Deposit(_player.Id, bank.Id, 100, Ct),
                Throws.TypeOf<BadRequestException>());
        }

        [Test]
        public async Task InterestAccrues_OnADeposit()
        {
            var bank = await AddPoi(SkillType.Banking);

            _player.Coin = 1_000_000;
            await DbContext.SaveChangesAsync();

            await _sut.Deposit(_player.Id, bank.Id, 1_000_000, Ct);

            // Backdate the settlement clock to simulate an absence.
            var player = await DbContext.Players.FirstAsync(p => p.Id == _player.Id);
            player.InterestSettledUtc = DateTime.UtcNow.AddHours(-4);
            await DbContext.SaveChangesAsync();

            var account = await _sut.GetAccount(_player.Id, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(account.InterestJustPaid, Is.GreaterThan(0));
                Assert.That(account.Deposited, Is.GreaterThan(1_000_000));
            });
        }

        [Test]
        public async Task InterestIsNotPaidTwiceForTheSameTime()
        {
            // Settling stamps the clock, so an immediate second read pays nothing. Without
            // this, any client that polled would mint coin.
            var bank = await AddPoi(SkillType.Banking);

            _player.Coin = 1_000_000;
            await DbContext.SaveChangesAsync();

            await _sut.Deposit(_player.Id, bank.Id, 1_000_000, Ct);

            var player = await DbContext.Players.FirstAsync(p => p.Id == _player.Id);
            player.InterestSettledUtc = DateTime.UtcNow.AddHours(-4);
            await DbContext.SaveChangesAsync();

            var first = await _sut.GetAccount(_player.Id, Ct);
            var second = await _sut.GetAccount(_player.Id, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(first.InterestJustPaid, Is.GreaterThan(0));
                Assert.That(second.InterestJustPaid, Is.Zero, "the same hours must not pay twice");
                Assert.That(second.Deposited, Is.EqualTo(first.Deposited));
            });
        }

        [Test]
        public async Task DepositedCoin_IsNotSpendable()
        {
            // The whole trade: interest is paid for illiquidity. If deposited coin still
            // counted as coin in hand, the bonus would be free.
            var bank = await AddPoi(SkillType.Banking);

            _player.Coin = 1000;
            await DbContext.SaveChangesAsync();

            var account = await _sut.Deposit(_player.Id, bank.Id, 1000, Ct);

            Assert.That(account.Coin, Is.Zero, "deposited coin has left your pocket");
        }

        [Test]
        public async Task AnEmptyAccount_EarnsNothingOverAnyAbsence()
        {
            var account = await _sut.GetAccount(_player.Id, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(account.InterestJustPaid, Is.Zero);
                Assert.That(account.InterestPerFullWindow, Is.Zero);
            });
        }
    }
}
