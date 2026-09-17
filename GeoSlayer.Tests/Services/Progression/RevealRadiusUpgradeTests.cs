using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.DTOs.Journey.Requests;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Services;
using GeoSlayer.Domain.Services.Crafting;
using GeoSlayer.Domain.Services.Fog;
using GeoSlayer.Domain.Services.Materials;
using GeoSlayer.Domain.Services.Museum;
using GeoSlayer.Domain.Services.Progression;
using GeoSlayer.Domain.Services.Retention;
using GeoSlayer.Domain.Services.Skills;
using GeoSlayer.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Tests.Services.Progression
{
    /// <summary>
    /// Stage 02 acceptance criterion 7: buying Reveal Radius must <b>measurably change</b>
    /// the cells revealed by an identical synthetic path.
    ///
    /// §3.0a is explicit that an upgrade which displays but does nothing is worse than no
    /// upgrade, so this asserts against persisted cells rather than the returned effect value.
    /// </summary>
    [TestFixture]
    public class RevealRadiusUpgradeTests : DatabaseIntegrationTestBase
    {
        private const double OriginLat = 51.5074;
        private const double OriginLng = -0.1278;
        private const double MetresPerDegreeLat = 111_320.0;

        private static CancellationToken Ct => CancellationToken.None;

        private ProgressionService _progression = null!;
        private MaterialService _materials = null!;
        private SkillTrainingService _skillTraining = null!;
        private CraftingService _crafting = null!;
        private MuseumService _museum = null!;
        private RetentionService _retention = null!;
        private FogService _fog = null!;

        protected override async Task CustomSetUp()
        {
            await TestDatabaseSeedHelper.SeedProgressionDefinitions(DbContext);
            _progression = TestDatabaseSeedHelper.CreateProgressionService(DbContext);
            await TestDatabaseSeedHelper.SeedMaterialDefinitions(DbContext);
            _materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext);
            _crafting = TestDatabaseSeedHelper.CreateCraftingService(DbContext, _progression, _materials);
            _museum = TestDatabaseSeedHelper.CreateMuseumService(DbContext);
            _retention = TestDatabaseSeedHelper.CreateRetentionService(DbContext, _materials, _progression);
            await TestDatabaseSeedHelper.SeedSkillDefinitions(DbContext);
            await TestDatabaseSeedHelper.SeedMuseumDefinitions(DbContext);
            await TestDatabaseSeedHelper.SeedCraftingDefinitions(DbContext);
            _skillTraining = TestDatabaseSeedHelper.CreateSkillTrainingService(
                DbContext, _progression, _materials);

            _fog = new FogService(DbContext, _progression, _materials, _skillTraining, _crafting, _museum, _retention);
        }

        /// <summary>A fresh player with their starting unlocks, so each run is independent.</summary>
        private async Task<Player> NewPlayer(string username)
        {
            var user = await TestDatabaseSeedHelper.SeedTestUser(DbContext, username);
            var player = await TestDatabaseSeedHelper.SeedTestPlayer(DbContext, user);
            await _progression.EnsureStartingUnlocks(player.Id, Ct);
            return player;
        }

        /// <summary>The same straight walk east every time — the "identical synthetic path".</summary>
        private static List<SyncPosition> WalkEast(double metres = 300, double fixIntervalSeconds = 10)
        {
            var speed = 1.4;
            var count = (int)(metres / (speed * fixIntervalSeconds));
            var now = DateTimeOffset.UtcNow;

            var positions = new List<SyncPosition>();

            for (var i = 0; i < count; i++)
            {
                var travelled = i * speed * fixIntervalSeconds;
                var lngOffset = travelled / (MetresPerDegreeLat * Math.Cos(OriginLat * Math.PI / 180.0));

                positions.Add(new SyncPosition
                {
                    Latitude = OriginLat,
                    Longitude = OriginLng + lngOffset,
                    Accuracy = 5,
                    TimestampMs = now.AddSeconds(-(count - i) * fixIntervalSeconds).ToUnixTimeMilliseconds(),
                });
            }

            return positions;
        }

        [Test]
        public async Task RevealRadius_MeasurablyIncreasesCellsRevealedByAnIdenticalPath()
        {
            var path = WalkEast();

            var baseline = await NewPlayer("radius_baseline");
            await _fog.Reveal(baseline.Id, path, Ct);
            await DbContext.SaveChangesAsync();
            var baselineCells = await DbContext.RevealedCells.CountAsync(r => r.PlayerId == baseline.Id);

            // Same path, same everything — except one rank of Reveal Radius.
            var upgraded = await NewPlayer("radius_upgraded");
            var player = await DbContext.Players.FirstAsync(p => p.Id == upgraded.Id);
            player.BonusPointsEarned += 10;
            await DbContext.SaveChangesAsync();

            await _progression.PurchaseUpgrade(upgraded.Id, ProgressionDefaults.UpgradeKeys.RevealRadius, Ct);

            await _fog.Reveal(upgraded.Id, path, Ct);
            await DbContext.SaveChangesAsync();
            var upgradedCells = await DbContext.RevealedCells.CountAsync(r => r.PlayerId == upgraded.Id);

            Assert.Multiple(() =>
            {
                Assert.That(baselineCells, Is.GreaterThan(0), "the baseline walk should reveal something");
                Assert.That(upgradedCells, Is.GreaterThan(baselineCells),
                    "one rank of Reveal Radius must widen the revealed corridor");
            });
        }

        [Test]
        public async Task WithoutTheUpgrade_TwoIdenticalWalksRevealTheSameCount()
        {
            var path = WalkEast();

            var a = await NewPlayer("radius_control_a");
            await _fog.Reveal(a.Id, path, Ct);
            await DbContext.SaveChangesAsync();
            var cellsA = await DbContext.RevealedCells.CountAsync(r => r.PlayerId == a.Id);

            var b = await NewPlayer("radius_control_b");
            await _fog.Reveal(b.Id, path, Ct);
            await DbContext.SaveChangesAsync();
            var cellsB = await DbContext.RevealedCells.CountAsync(r => r.PlayerId == b.Id);

            // Guards the test above: without this, a difference could come from anything.
            Assert.That(cellsB, Is.EqualTo(cellsA));
        }

        // ── Stage 06 criterion 6: gear changes the reveal too ───────────

        [Test]
        public async Task EquippedRevealRadiusGear_MeasurablyIncreasesCellsRevealed()
        {
            var path = WalkEast();

            var baseline = await NewPlayer("gear_baseline");
            await _fog.Reveal(baseline.Id, path, Ct);
            await DbContext.SaveChangesAsync();
            var baselineCells = await DbContext.RevealedCells.CountAsync(r => r.PlayerId == baseline.Id);

            // Same path, same everything — except an equipped Surveyor's Lens.
            var equipped = await NewPlayer("gear_equipped");

            var lens = await DbContext.Items.FirstAsync(i => i.Key == "surveyors_lens");

            DbContext.PlayerItems.Add(new PlayerItem
            {
                PlayerId = equipped.Id,
                ItemId = lens.Id,
                Quantity = 1,
                IsEquipped = true,
                AcquiredUtc = DateTime.UtcNow,
            });
            await DbContext.SaveChangesAsync();

            await _fog.Reveal(equipped.Id, path, Ct);
            await DbContext.SaveChangesAsync();
            var equippedCells = await DbContext.RevealedCells.CountAsync(r => r.PlayerId == equipped.Id);

            Assert.Multiple(() =>
            {
                Assert.That(baselineCells, Is.GreaterThan(0));
                Assert.That(equippedCells, Is.GreaterThan(baselineCells),
                    "an equipped item that changes no behaviour is a bug (§4.3)");
            });
        }

        [Test]
        public async Task UnequippedRevealRadiusGear_ChangesNothing()
        {
            var path = WalkEast();

            var baseline = await NewPlayer("gear_unequipped_control");
            await _fog.Reveal(baseline.Id, path, Ct);
            await DbContext.SaveChangesAsync();
            var baselineCells = await DbContext.RevealedCells.CountAsync(r => r.PlayerId == baseline.Id);

            var owner = await NewPlayer("gear_unequipped");
            var lens = await DbContext.Items.FirstAsync(i => i.Key == "surveyors_lens");

            DbContext.PlayerItems.Add(new PlayerItem
            {
                PlayerId = owner.Id,
                ItemId = lens.Id,
                Quantity = 1,
                IsEquipped = false,
                AcquiredUtc = DateTime.UtcNow,
            });
            await DbContext.SaveChangesAsync();

            await _fog.Reveal(owner.Id, path, Ct);
            await DbContext.SaveChangesAsync();
            var ownerCells = await DbContext.RevealedCells.CountAsync(r => r.PlayerId == owner.Id);

            // Owning is not equipping — the bonus must require the slot.
            Assert.That(ownerCells, Is.EqualTo(baselineCells));
        }
    }
}
