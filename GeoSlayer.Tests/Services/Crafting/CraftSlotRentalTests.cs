using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Exceptions;
using GeoSlayer.Domain.Services.Crafting;
using GeoSlayer.Domain.Services.Progression;
using GeoSlayer.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Tests.Services.Crafting
{
    /// <summary>
    /// Renting a craft slot with coin (DESIGN.md §5D.4).
    ///
    /// <para>The property these tests exist to protect: a rental must be <b>clearly worse</b>
    /// than the permanent Craft Slot upgrade, and must never accumulate into one. §4.3's rule
    /// is that two routes to the same bonus makes one redundant — renting is for the player
    /// who wants one more thing cooking tonight, not a way to buy the upgrade with coin.</para>
    /// </summary>
    [TestFixture]
    public class CraftSlotRentalTests : DatabaseIntegrationTestBase
    {
        private CraftingService _sut = null!;
        private ProgressionService _progression = null!;
        private Player _player = null!;

        private static CancellationToken Ct => CancellationToken.None;

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

            var materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext, TerrainType.Urban);
            _sut = TestDatabaseSeedHelper.CreateCraftingService(DbContext, _progression, materials);
        }

        private async Task GiveCoin(long amount)
        {
            var player = await DbContext.Players.FirstAsync(p => p.Id == _player.Id);
            player.Coin = amount;
            await DbContext.SaveChangesAsync();
        }

        private async Task<long> CoinHeld() =>
            (await DbContext.Players.AsNoTracking().FirstAsync(p => p.Id == _player.Id)).Coin;

        private async Task<int> RentalsHeld() =>
            (await DbContext.Players.AsNoTracking().FirstAsync(p => p.Id == _player.Id))
                .RentedCraftSlots;

        // ── Renting ─────────────────────────────────────────────────────

        [Test]
        public async Task ASlotCanBeRented()
        {
            await GiveCoin(10_000);

            var result = await _sut.RentCraftSlot(_player.Id, Ct);

            Assert.Multiple(async () =>
            {
                Assert.That(result.RentedCraftSlots, Is.EqualTo(1));
                Assert.That(await CoinHeld(), Is.EqualTo(10_000 - CraftingService.SlotRentalCost));
            });
        }

        [Test]
        public async Task RentingWithoutEnoughCoin_IsRefused()
        {
            await GiveCoin(1);

            Assert.That(
                async () => await _sut.RentCraftSlot(_player.Id, Ct),
                Throws.TypeOf<BadRequestException>());

            Assert.That(await RentalsHeld(), Is.Zero, "nothing is granted on a refusal");
        }

        [Test]
        public async Task RentingDoesNotRaiseThePermanentLimit()
        {
            // The §4.3 property. A rental that raised QueueLimit would let one purchase permit
            // every subsequent craft until something finished — a permanent upgrade bought
            // with coin.
            await GiveCoin(10_000);

            var before = (await _sut.GetRecipes(_player.Id, Ct)).QueueLimit;

            await _sut.RentCraftSlot(_player.Id, Ct);

            var after = await _sut.GetRecipes(_player.Id, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(after.QueueLimit, Is.EqualTo(before), "the limit is unchanged");
                Assert.That(after.RentedCraftSlots, Is.EqualTo(1), "the rental is held separately");
            });
        }

        [Test]
        public async Task RentalsAccumulate_ButEachStillBuysOnlyOneCraft()
        {
            // Stockpiling is allowed; what is not allowed is a stockpile behaving like a
            // permanent upgrade. Each rental is spent by exactly one craft.
            await GiveCoin(100_000);

            await _sut.RentCraftSlot(_player.Id, Ct);
            await _sut.RentCraftSlot(_player.Id, Ct);

            var result = await _sut.GetRecipes(_player.Id, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(result.RentedCraftSlots, Is.EqualTo(2));
                Assert.That(result.QueueLimit, Is.EqualTo(1), "still one permanent slot");
            });
        }

        [Test]
        public async Task TheRentalPriceIsReported()
        {
            // So the app can name the price rather than the player discovering it on refusal.
            var result = await _sut.GetRecipes(_player.Id, Ct);

            Assert.That(result.SlotRentalCost, Is.EqualTo(CraftingService.SlotRentalCost));
            Assert.That(result.SlotRentalCost, Is.GreaterThan(0));
        }

        [Test]
        public void TheRentalIsPoorValueAgainstTheUpgrade()
        {
            // Stated as a test because it is the design rule, not a preference. The permanent
            // upgrade costs 3 Bonus Points for rank 1 and lasts forever; a rental buys one
            // craft. If renting were competitive, §4.3 says one of the two is redundant.
            var upgrade = ProgressionSeedData.Upgrades
                .First(u => u.Key == ProgressionDefaults.UpgradeKeys.CraftSlot);

            Assert.That(upgrade.MaxRank, Is.GreaterThan(0));
            Assert.That(CraftingService.DefaultSlotRentalCost, Is.GreaterThanOrEqualTo(500),
                "a cheap rental would undercut the upgrade entirely");
        }
    }
}
