using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.DTOs.Journey.Requests;
using GeoSlayer.Domain.Services;
using GeoSlayer.Domain.Services.Crafting;
using GeoSlayer.Domain.Services.Materials;
using GeoSlayer.Domain.Services.Museum;
using GeoSlayer.Domain.Services.Retention;
using GeoSlayer.Domain.Services.Progression;
using GeoSlayer.Domain.Services.Skills;
using GeoSlayer.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Tests.Services.Fog
{
    /// <summary>
    /// FogService against a real database.
    ///
    /// The pure-function tests cover the reveal geometry; these cover what only a database
    /// can answer — that cells actually persist, that re-revealing is a no-op against the
    /// unique index rather than a duplicate-key crash, and that XP lands on the player row.
    ///
    /// Requires Docker (Testcontainers).
    /// </summary>
    [TestFixture]
    public class FogServiceIntegrationTests : DatabaseIntegrationTestBase
    {
        private FogService _sut = null!;
        private Player _player = null!;

        private const double OriginLat = 51.5074;
        private const double OriginLng = -0.1278;
        private const double MetresPerDegreeLat = 111_320.0;

        private ProgressionService _progression = null!;
        private MaterialService _materials = null!;
        private SkillTrainingService _skillTraining = null!;
        private CraftingService _crafting = null!;
        private MuseumService _museum = null!;
        private RetentionService _retention = null!;

        protected override async Task CustomSetUp()
        {
            var (_, player) = await TestDatabaseSeedHelper.SeedMinimalData(DbContext);
            _player = player;

            // Stage 02: the ladder and tree must exist, and the player needs their level-1
            // unlocks or Exploration XP has nowhere to land.
            await TestDatabaseSeedHelper.SeedProgressionDefinitions(DbContext);
            _progression = TestDatabaseSeedHelper.CreateProgressionService(DbContext);
            await _progression.EnsureStartingUnlocks(_player.Id, CancellationToken.None);

            await TestDatabaseSeedHelper.SeedMaterialDefinitions(DbContext);
            _materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext);
            _crafting = TestDatabaseSeedHelper.CreateCraftingService(DbContext, _progression, _materials);
            _museum = TestDatabaseSeedHelper.CreateMuseumService(DbContext);
            _retention = TestDatabaseSeedHelper.CreateRetentionService(DbContext, _materials, _progression);
            await TestDatabaseSeedHelper.SeedSkillDefinitions(DbContext);
            await TestDatabaseSeedHelper.SeedMuseumDefinitions(DbContext);
            _skillTraining = TestDatabaseSeedHelper.CreateSkillTrainingService(
                DbContext, _progression, _materials);

            _sut = new FogService(DbContext, _progression, _materials, _skillTraining, _crafting, _museum, _retention);
        }

        private static double LngOffset(double metres, double atLat) =>
            metres / (MetresPerDegreeLat * Math.Cos(atLat * Math.PI / 180.0));

        /// <summary>A straight walk east at 1.4 m/s, ending "now" so the clock-skew check passes.</summary>
        private static List<SyncPosition> WalkEast(double metres, double fixIntervalSeconds = 10)
        {
            const double speed = 1.4;
            var positions = new List<SyncPosition>();
            var step = speed * fixIntervalSeconds;

            var totalSeconds = metres / speed;
            var startMs = DateTimeOffset.UtcNow.AddSeconds(-totalSeconds).ToUnixTimeMilliseconds();

            for (double travelled = 0; ; travelled = Math.Min(travelled + step, metres))
            {
                positions.Add(new SyncPosition
                {
                    Latitude = OriginLat,
                    Longitude = OriginLng + LngOffset(travelled, OriginLat),
                    TimestampMs = startMs + (long)(travelled / speed * 1000),
                    Accuracy = 8,
                });

                if (travelled >= metres) break;
            }

            return positions;
        }

        /// <summary>
        /// The sync cooldown compares against `LastSyncAtUtc`, so a test doing two syncs
        /// in quick succession has to backdate the first or the second is rejected.
        /// </summary>
        private async Task ClearSyncCooldown()
        {
            var player = await DbContext.Players.FirstAsync(p => p.Id == _player.Id);
            player.LastSyncAtUtc = DateTime.UtcNow.AddMinutes(-1);
            await DbContext.SaveChangesAsync();
        }

        // ── Cells persist ───────────────────────────────────────────────

        [Test]
        public async Task Reveal_AWalk_PersistsCellsToTheDatabase()
        {
            var result = await _sut.Reveal(_player.Id, WalkEast(300), CancellationToken.None);
            await DbContext.SaveChangesAsync();

            var stored = await DbContext.RevealedCells.CountAsync(r => r.PlayerId == _player.Id);

            Assert.That(result.NewCells, Is.Not.Empty);
            Assert.That(stored, Is.EqualTo(result.NewCells.Count),
                "every returned cell should have been written");
        }

        [Test]
        public async Task Reveal_StoredCells_AreReturnedByGetAllRevealed()
        {
            await _sut.Reveal(_player.Id, WalkEast(300), CancellationToken.None);
            await DbContext.SaveChangesAsync();

            var all = await _sut.GetAllRevealed(_player.Id, CancellationToken.None);
            var stored = await DbContext.RevealedCells.CountAsync(r => r.PlayerId == _player.Id);

            Assert.That(all, Has.Count.EqualTo(stored));
        }

        // ── Acceptance criterion 8, at the database level ───────────────

        [Test]
        public async Task Reveal_ReplayingAnIdenticalPath_RevealsNothingAndGrantsNoXp()
        {
            var path = WalkEast(300);

            var first = await _sut.Reveal(_player.Id, path, CancellationToken.None);
            await DbContext.SaveChangesAsync();

            var xpAfterFirst = (await DbContext.Players.FirstAsync(p => p.Id == _player.Id)).AdventurerXp;
            var cellsAfterFirst = await DbContext.RevealedCells.CountAsync(r => r.PlayerId == _player.Id);

            await ClearSyncCooldown();

            // The same walk again — the unique index must make this a no-op, not a crash.
            var second = await _sut.Reveal(_player.Id, path, CancellationToken.None);
            await DbContext.SaveChangesAsync();

            var xpAfterSecond = (await DbContext.Players.FirstAsync(p => p.Id == _player.Id)).AdventurerXp;
            var cellsAfterSecond = await DbContext.RevealedCells.CountAsync(r => r.PlayerId == _player.Id);

            Assert.Multiple(() =>
            {
                Assert.That(first.NewCells, Is.Not.Empty, "the first walk should reveal something");
                Assert.That(second.NewCells, Is.Empty, "the replay revealed new cells");
                Assert.That(second.XpEarned, Is.Zero, "the replay granted XP");
                Assert.That(cellsAfterSecond, Is.EqualTo(cellsAfterFirst), "the replay wrote rows");
                Assert.That(xpAfterSecond, Is.EqualTo(xpAfterFirst), "the replay moved the XP total");
            });
        }

        [Test]
        public async Task Reveal_OverlappingPath_OnlyRevealsTheNewGround()
        {
            await _sut.Reveal(_player.Id, WalkEast(300), CancellationToken.None);
            await DbContext.SaveChangesAsync();

            await ClearSyncCooldown();

            // Walk the same street, then keep going — only the extension is new.
            var second = await _sut.Reveal(_player.Id, WalkEast(600), CancellationToken.None);
            await DbContext.SaveChangesAsync();

            var total = await DbContext.RevealedCells.CountAsync(r => r.PlayerId == _player.Id);

            Assert.That(second.NewCells, Is.Not.Empty, "the extra 300 m should be new ground");
            Assert.That(total, Is.EqualTo(
                await DbContext.RevealedCells.Select(r => new { r.GridLat, r.GridLng })
                    .Distinct().CountAsync()),
                "duplicate cells were written");
        }

        [Test]
        public void RevealedCells_DuplicateForTheSamePlayer_IsRejectedByTheUniqueIndex()
        {
            // The index is the last line of defence behind the service's own filtering.
            DbContext.RevealedCells.Add(new RevealedCell
            {
                PlayerId = _player.Id,
                GridLat = 100,
                GridLng = 200,
                RevealedAtUtc = DateTime.UtcNow,
            });
            DbContext.RevealedCells.Add(new RevealedCell
            {
                PlayerId = _player.Id,
                GridLat = 100,
                GridLng = 200,
                RevealedAtUtc = DateTime.UtcNow,
            });

            Assert.ThrowsAsync<DbUpdateException>(async () => await DbContext.SaveChangesAsync());
        }

        [Test]
        public async Task RevealedCells_SameCellForDifferentPlayers_IsAllowed()
        {
            // Two players walking the same street both reveal it — the index is per player.
            var otherUser = await TestDatabaseSeedHelper.SeedTestUser(DbContext, "second");
            var otherPlayer = await TestDatabaseSeedHelper.SeedTestPlayer(DbContext, otherUser);

            DbContext.RevealedCells.Add(new RevealedCell
            {
                PlayerId = _player.Id,
                GridLat = 100,
                GridLng = 200,
                RevealedAtUtc = DateTime.UtcNow,
            });
            DbContext.RevealedCells.Add(new RevealedCell
            {
                PlayerId = otherPlayer.Id,
                GridLat = 100,
                GridLng = 200,
                RevealedAtUtc = DateTime.UtcNow,
            });

            await DbContext.SaveChangesAsync();

            Assert.That(await DbContext.RevealedCells.CountAsync(r => r.GridLat == 100), Is.EqualTo(2));
        }

        // ── XP and levelling land on the player row ─────────────────────

        [Test]
        public async Task Reveal_GrantsXpOnThePlayerRow()
        {
            var result = await _sut.Reveal(_player.Id, WalkEast(300), CancellationToken.None);
            await DbContext.SaveChangesAsync();

            var player = await DbContext.Players.FirstAsync(p => p.Id == _player.Id);

            // Stage 02 splits the pools: XpEarned is Exploration (skill) XP, while the player
            // row holds Adventurer XP — the 0.25 cut plus the per-cell milestone (§3.0b).
            Assert.That(result.XpEarned, Is.GreaterThan(0));
            Assert.That(player.AdventurerXp, Is.GreaterThan(0));
            Assert.That(player.AdventurerXp, Is.LessThan(result.XpEarned),
                "Adventurer XP is a fraction of skill XP, not equal to it");

            // Stage 04 made a cell train *every* skill whose terrain mapping matches, so
            // XpEarned is now the sum across skills rather than Exploration's alone.
            // Exploration is therefore a component of it, not equal to it.
            var skills = await DbContext.PlayerSkills
                .Where(sk => sk.PlayerId == _player.Id)
                .ToListAsync();

            var exploration = skills.Single(sk => sk.SkillType == SkillType.Exploration);

            Assert.That(exploration.Xp, Is.GreaterThan(0), "Exploration XP should land on its skill row");
            Assert.That(skills.Sum(sk => sk.Xp), Is.EqualTo(result.XpEarned),
                "XpEarned should be the total across every skill the walk trained");
        }

        [Test]
        public async Task Reveal_LevelIsDerivedFromCumulativeXp()
        {
            await _sut.Reveal(_player.Id, WalkEast(300), CancellationToken.None);
            await DbContext.SaveChangesAsync();

            var player = await DbContext.Players.FirstAsync(p => p.Id == _player.Id);

            // Stage 01 made XP cumulative and level derived — they must agree on reload.
            Assert.That(player.AdventurerLevel, Is.EqualTo(XpCurve.LevelForXp(player.AdventurerXp)));
        }

        // ── Anti-cheat against real persisted state ─────────────────────

        [Test]
        public async Task Reveal_TwoSyncsInsideTheCooldown_RevealsNothingOnTheSecond()
        {
            await _sut.Reveal(_player.Id, WalkEast(300), CancellationToken.None);
            await DbContext.SaveChangesAsync();

            // Deliberately no ClearSyncCooldown — this is the cooldown under test.
            var second = await _sut.Reveal(_player.Id, WalkEast(600), CancellationToken.None);

            Assert.That(second.NewCells, Is.Empty);
        }

        [Test]
        public async Task Reveal_DrivingTrace_PersistsNoCells()
        {
            // 15 m/s, straight — a vehicle. Reveals nothing, and writes nothing.
            var start = DateTimeOffset.UtcNow.AddSeconds(-200).ToUnixTimeMilliseconds();
            var path = Enumerable.Range(0, 40).Select(i => new SyncPosition
            {
                Latitude = OriginLat,
                Longitude = OriginLng + LngOffset(15 * 5 * i, OriginLat),
                TimestampMs = start + i * 5_000,
                Accuracy = 8,
            }).ToList();

            var result = await _sut.Reveal(_player.Id, path, CancellationToken.None);
            await DbContext.SaveChangesAsync();

            Assert.That(result.NewCells, Is.Empty);
            Assert.That(await DbContext.RevealedCells.CountAsync(r => r.PlayerId == _player.Id), Is.Zero);
        }

        [Test]
        public async Task Reveal_RejectedBatch_StillUpdatesLastKnownPosition()
        {
            // A rejected sync must still move the player's tracked position, or alternating
            // good and bad syncs could be used to sidestep the speed cap.
            var start = DateTimeOffset.UtcNow.AddSeconds(-200).ToUnixTimeMilliseconds();
            var path = Enumerable.Range(0, 40).Select(i => new SyncPosition
            {
                Latitude = OriginLat,
                Longitude = OriginLng + LngOffset(15 * 5 * i, OriginLat),
                TimestampMs = start + i * 5_000,
                Accuracy = 8,
            }).ToList();

            await _sut.Reveal(_player.Id, path, CancellationToken.None);
            await DbContext.SaveChangesAsync();

            var player = await DbContext.Players.FirstAsync(p => p.Id == _player.Id);

            Assert.That(player.LastLongitude, Is.EqualTo(path[^1].Longitude).Within(1e-9));
            Assert.That(player.LastSyncAtUtc, Is.Not.Null);
        }

        // ── Isolation between players ───────────────────────────────────

        [Test]
        public async Task GetAllRevealed_ReturnsOnlyTheGivenPlayersCells()
        {
            var otherUser = await TestDatabaseSeedHelper.SeedTestUser(DbContext, "second");
            var otherPlayer = await TestDatabaseSeedHelper.SeedTestPlayer(DbContext, otherUser);

            await _sut.Reveal(_player.Id, WalkEast(300), CancellationToken.None);
            await DbContext.SaveChangesAsync();

            var mine = await _sut.GetAllRevealed(_player.Id, CancellationToken.None);
            var theirs = await _sut.GetAllRevealed(otherPlayer.Id, CancellationToken.None);

            Assert.That(mine, Is.Not.Empty);
            Assert.That(theirs, Is.Empty, "one player's walk leaked into another's map");
        }
    }
}
