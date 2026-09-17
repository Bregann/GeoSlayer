using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Exceptions;
using GeoSlayer.Domain.Services.Fog;
using GeoSlayer.Domain.Services.Materials;
using GeoSlayer.Domain.Services.Progression;
using GeoSlayer.Domain.Services.Skills;
using GeoSlayer.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace GeoSlayer.Tests.Services.Skills
{
    /// <summary>
    /// Stage 04 criteria 2, 3, 4, 5, 7 and 8, against a real database.
    /// </summary>
    [TestFixture]
    public class SkillTrainingIntegrationTests : DatabaseIntegrationTestBase
    {
        private const double OriginLat = 51.5074;
        private const double OriginLng = -0.1278;

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

            _progression = TestDatabaseSeedHelper.CreateProgressionService(DbContext);
            await _progression.EnsureStartingUnlocks(_player.Id, Ct);
        }

        private SkillTrainingService ServiceFor(TerrainType terrain)
        {
            var materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext, terrain);
            return TestDatabaseSeedHelper.CreateSkillTrainingService(DbContext, _progression, materials);
        }

        private async Task<long> SkillXp(SkillType skill) =>
            await DbContext.PlayerSkills
                .Where(s => s.PlayerId == _player.Id && s.SkillType == skill)
                .Select(s => s.Xp)
                .FirstOrDefaultAsync();

        /// <summary>Put the player at a known position, as a sync would.</summary>
        private async Task SetPosition(double lat, double lng)
        {
            var player = await DbContext.Players.FirstAsync(p => p.Id == _player.Id);
            player.LastLatitude = lat;
            player.LastLongitude = lng;
            player.LastSyncAtUtc = DateTime.UtcNow;
            await DbContext.SaveChangesAsync();
        }

        private async Task<PointOfInterest> SeedPoi(
            double lat, double lng, SkillType skill = SkillType.Foraging, int xpReward = 40)
        {
            var poi = new PointOfInterest
            {
                OsmId = Random.Shared.NextInt64(1, long.MaxValue),
                OsmType = "node",
                Name = "Test Garden",
                Skill = skill,
                Location = new Point(lng, lat) { SRID = 4326 },
                XpReward = xpReward,
            };

            DbContext.PointsOfInterest.Add(poi);
            await DbContext.SaveChangesAsync();
            return poi;
        }

        /// <summary>Metres east, converted to a longitude delta at this latitude.</summary>
        private static double LngOffset(double metres) =>
            metres / (111_320.0 * Math.Cos(OriginLat * Math.PI / 180.0));

        // ── Criterion 2: Foraging unlocked at level 1 ───────────────────

        [Test]
        public async Task ANewAccount_HasForagingUnlockedAtLevelOne()
        {
            var foraging = await DbContext.PlayerSkills
                .FirstOrDefaultAsync(s => s.PlayerId == _player.Id && s.SkillType == SkillType.Foraging);

            Assert.Multiple(() =>
            {
                Assert.That(foraging, Is.Not.Null, "Foraging ships at level 1 — the cold-start fix");
                Assert.That(foraging!.Level, Is.EqualTo(1));
            });
        }

        // ── Criterion 3: woodland trains Foraging and Adventurer ────────

        [Test]
        public async Task WalkingWoodland_GrantsForagingAndAdventurerXp()
        {
            var sut = ServiceFor(TerrainType.Woodland);

            var results = await sut.TrainFromCells(
                _player.Id, [new GridCell(10, 10), new GridCell(11, 10)], Ct);

            var foraging = results.FirstOrDefault(r => r.SkillType == SkillType.Foraging);
            var player = await DbContext.Players.FirstAsync(p => p.Id == _player.Id);

            Assert.Multiple(() =>
            {
                Assert.That(foraging, Is.Not.Null);
                // Woodland is 3 XP/cell across 2 cells.
                Assert.That(foraging!.SkillXpEarned, Is.EqualTo(6));
                Assert.That(player.AdventurerXp, Is.GreaterThan(0), "the flat cut should also land");
            });
        }

        [Test]
        public async Task TheAdventurerCut_IsTheConfiguredRatio()
        {
            var sut = ServiceFor(TerrainType.Woodland);

            // 10 cells of woodland = 30 Foraging XP, plus Exploration's 2/cell = 20.
            var cells = Enumerable.Range(0, 10).Select(i => new GridCell(100 + i, 100)).ToList();
            var results = await sut.TrainFromCells(_player.Id, cells, Ct);

            foreach (var result in results)
            {
                // GLOBAL_XP_RATIO is 0.25 (§3.0b).
                Assert.That(result.AdventurerXpEarned, Is.EqualTo((long)Math.Floor(result.SkillXpEarned * 0.25)),
                    $"{result.Name} paid the wrong Adventurer cut");
            }
        }

        // ── Criterion 4: urban trains at base rate, not zero ────────────

        [Test]
        public async Task WalkingUrban_TrainsForagingAtBaseRateNotZero()
        {
            var sut = ServiceFor(TerrainType.Urban);

            var results = await sut.TrainFromCells(_player.Id, [new GridCell(20, 20)], Ct);
            var foraging = results.FirstOrDefault(r => r.SkillType == SkillType.Foraging);

            Assert.Multiple(() =>
            {
                Assert.That(foraging, Is.Not.Null, "urban must still train Foraging");
                Assert.That(foraging!.SkillXpEarned, Is.EqualTo(1), "at base rate");
            });
        }

        [Test]
        public async Task EveryTerrain_TrainsForagingSomething()
        {
            // The geography rule (§5.2): terrain multiplies, never gates. There is no terrain
            // on which a player simply cannot train.
            foreach (var terrain in new[]
                     {
                         TerrainType.Open, TerrainType.Woodland, TerrainType.Water,
                         TerrainType.Farmland, TerrainType.Urban, TerrainType.Industrial,
                         TerrainType.Rocky, TerrainType.Coastal,
                     })
            {
                var before = await SkillXp(SkillType.Foraging);

                var sut = ServiceFor(terrain);
                await sut.TrainFromCells(_player.Id, [new GridCell((int)terrain + 500, 900)], Ct);

                var after = await SkillXp(SkillType.Foraging);

                Assert.That(after, Is.GreaterThan(before), $"{terrain} trained nothing");
            }
        }

        [Test]
        public async Task WoodlandOutpacesUrban_ButBothTrain()
        {
            var urban = ServiceFor(TerrainType.Urban);
            var urbanResult = await urban.TrainFromCells(_player.Id, [new GridCell(30, 30)], Ct);

            var woodland = ServiceFor(TerrainType.Woodland);
            var woodlandResult = await woodland.TrainFromCells(_player.Id, [new GridCell(31, 30)], Ct);

            var urbanXp = urbanResult.First(r => r.SkillType == SkillType.Foraging).SkillXpEarned;
            var woodlandXp = woodlandResult.First(r => r.SkillType == SkillType.Foraging).SkillXpEarned;

            Assert.That(woodlandXp, Is.GreaterThan(urbanXp),
                "terrain should matter for speed, without gating access");
        }

        [Test]
        public async Task ALockedSkill_DoesNotTrain()
        {
            // Mining is not unlocked at Adventurer 1, and has no terrain mapping seeded.
            var sut = ServiceFor(TerrainType.Rocky);

            var results = await sut.TrainFromCells(_player.Id, [new GridCell(40, 40)], Ct);

            Assert.That(results.Any(r => r.SkillType == SkillType.Mining), Is.False);
        }

        // ── Criterion 5: POI visits pay roughly 20x a cell ──────────────

        [Test]
        public async Task VisitingAPoi_GrantsAboutTwentyTimesACell()
        {
            await SetPosition(OriginLat, OriginLng);
            var poi = await SeedPoi(OriginLat, OriginLng);

            var sut = ServiceFor(TerrainType.Open);
            var result = await sut.VisitPoi(_player.Id, poi.Id, Ct);

            // §3.3: a POI is worth ~20x a terrain cell. Foraging base rate is 1 XP/cell, and
            // the seeded POI reward is 40 — comfortably in the "worth a detour" band.
            Assert.Multiple(() =>
            {
                Assert.That(result.SkillXpEarned, Is.EqualTo(40));
                Assert.That(result.SkillXpEarned, Is.GreaterThanOrEqualTo(20),
                    "a POI must be worth a detour compared with walking a cell");
                Assert.That(result.IsFirstVisit, Is.True);
                Assert.That(result.DecayMultiplier, Is.EqualTo(1.0));
            });
        }

        [Test]
        public async Task VisitingAPoi_TrainsThePoisOwnSkill()
        {
            await SetPosition(OriginLat, OriginLng);

            // Exploration, not Foraging — proving the skill comes from the POI, not from code.
            var poi = await SeedPoi(OriginLat, OriginLng, SkillType.Exploration, 30);

            var sut = ServiceFor(TerrainType.Open);
            var result = await sut.VisitPoi(_player.Id, poi.Id, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(result.Skill, Is.EqualTo(SkillType.Exploration));
                Assert.That(result.SkillXpEarned, Is.EqualTo(30));
            });
        }

        // ── Criterion 6: repeat visits decay ────────────────────────────

        [Test]
        public async Task RepeatVisits_Decay()
        {
            await SetPosition(OriginLat, OriginLng);
            var poi = await SeedPoi(OriginLat, OriginLng, SkillType.Foraging, 100);

            var sut = ServiceFor(TerrainType.Open);

            var first = await sut.VisitPoi(_player.Id, poi.Id, Ct);

            // Step past the anti-spam cooldown without waiting in real time.
            await BackdateLastVisit(poi.Id, TimeSpan.FromMinutes(5));
            var second = await sut.VisitPoi(_player.Id, poi.Id, Ct);

            await BackdateLastVisit(poi.Id, TimeSpan.FromMinutes(5));
            var third = await sut.VisitPoi(_player.Id, poi.Id, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(first.SkillXpEarned, Is.EqualTo(100));
                Assert.That(second.SkillXpEarned, Is.EqualTo(66), "second visit is 67%");
                Assert.That(third.SkillXpEarned, Is.EqualTo(50), "third is 50%");
                Assert.That(third.SkillXpEarned, Is.LessThan(second.SkillXpEarned));
            });
        }

        /// <summary>Move a visit's timestamp back, so the cooldown does not block a test.</summary>
        private async Task BackdateLastVisit(int poiId, TimeSpan by)
        {
            var visit = await DbContext.PlayerPoiVisits
                .FirstAsync(v => v.PlayerId == _player.Id && v.PoiId == poiId);

            visit.LastVisitUtc -= by;
            await DbContext.SaveChangesAsync();
        }

        [Test]
        public async Task VisitingTooQuickly_IsRejected()
        {
            await SetPosition(OriginLat, OriginLng);
            var poi = await SeedPoi(OriginLat, OriginLng);

            var sut = ServiceFor(TerrainType.Open);
            await sut.VisitPoi(_player.Id, poi.Id, Ct);

            // Anti-spam (§7.2): tapping repeatedly must not farm a POI.
            await Assert.ThatAsync(
                () => sut.VisitPoi(_player.Id, poi.Id, Ct),
                Throws.TypeOf<BadRequestException>());
        }

        // ── Criterion 7: range is enforced server-side ──────────────────

        [Test]
        public async Task VisitingFromOutOfRange_IsRejected()
        {
            await SetPosition(OriginLat, OriginLng);

            // 500 m east — far outside the 50 m interact radius.
            var poi = await SeedPoi(OriginLat, OriginLng + LngOffset(500));

            var sut = ServiceFor(TerrainType.Open);

            await Assert.ThatAsync(
                () => sut.VisitPoi(_player.Id, poi.Id, Ct),
                Throws.TypeOf<BadRequestException>());
        }

        [Test]
        public async Task OutOfRangeVisit_RecordsNothing()
        {
            await SetPosition(OriginLat, OriginLng);
            var poi = await SeedPoi(OriginLat, OriginLng + LngOffset(500));

            var sut = ServiceFor(TerrainType.Open);

            try { await sut.VisitPoi(_player.Id, poi.Id, Ct); } catch (BadRequestException) { }

            var visits = await DbContext.PlayerPoiVisits.CountAsync(v => v.PlayerId == _player.Id);
            var xp = await SkillXp(SkillType.Foraging);

            Assert.Multiple(() =>
            {
                Assert.That(visits, Is.Zero, "a rejected visit must not be logged");
                Assert.That(xp, Is.Zero, "nor pay XP");
            });
        }

        [Test]
        public async Task VisitingWithNoVerifiedPosition_IsRejected()
        {
            // A fresh player has never synced, so there is no position to check against.
            // Trusting the client here would be a free teleport to any POI on the map.
            var poi = await SeedPoi(OriginLat, OriginLng);

            var sut = ServiceFor(TerrainType.Open);

            await Assert.ThatAsync(
                () => sut.VisitPoi(_player.Id, poi.Id, Ct),
                Throws.TypeOf<BadRequestException>());
        }

        [Test]
        public async Task AVisitJustInsideRange_IsAccepted()
        {
            await SetPosition(OriginLat, OriginLng);

            // 40 m — inside the 50 m radius.
            var poi = await SeedPoi(OriginLat, OriginLng + LngOffset(40));

            var sut = ServiceFor(TerrainType.Open);
            var result = await sut.VisitPoi(_player.Id, poi.Id, Ct);

            Assert.That(result.SkillXpEarned, Is.GreaterThan(0));
        }

        // ── Criterion 8: the visit log ──────────────────────────────────

        [Test]
        public async Task PlayerPoiVisit_RecordsFirstVisitAndCount()
        {
            await SetPosition(OriginLat, OriginLng);
            var poi = await SeedPoi(OriginLat, OriginLng);

            var sut = ServiceFor(TerrainType.Open);

            var before = DateTime.UtcNow;
            await sut.VisitPoi(_player.Id, poi.Id, Ct);

            await BackdateLastVisit(poi.Id, TimeSpan.FromMinutes(5));
            await sut.VisitPoi(_player.Id, poi.Id, Ct);

            var visit = await DbContext.PlayerPoiVisits
                .FirstAsync(v => v.PlayerId == _player.Id && v.PoiId == poi.Id);

            Assert.Multiple(() =>
            {
                Assert.That(visit.TotalVisits, Is.EqualTo(2), "lifetime visits never decay");
                Assert.That(visit.FirstVisitUtc, Is.GreaterThanOrEqualTo(before.AddSeconds(-5)));
                Assert.That(visit.FirstVisitUtc, Is.LessThanOrEqualTo(visit.LastVisitUtc));
            });
        }

        [Test]
        public async Task FirstVisitFlag_IsOnlyTrueOnce()
        {
            await SetPosition(OriginLat, OriginLng);
            var poi = await SeedPoi(OriginLat, OriginLng);

            var sut = ServiceFor(TerrainType.Open);

            var first = await sut.VisitPoi(_player.Id, poi.Id, Ct);
            await BackdateLastVisit(poi.Id, TimeSpan.FromMinutes(5));
            var second = await sut.VisitPoi(_player.Id, poi.Id, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(first.IsFirstVisit, Is.True);
                Assert.That(second.IsFirstVisit, Is.False);
            });
        }

        [Test]
        public async Task AVisitReturnsASessionToken()
        {
            // §3.1d plans POI minigames; the session shape means they can grow into this
            // endpoint rather than needing a replacement.
            await SetPosition(OriginLat, OriginLng);
            var poi = await SeedPoi(OriginLat, OriginLng);

            var sut = ServiceFor(TerrainType.Open);
            var result = await sut.VisitPoi(_player.Id, poi.Id, Ct);

            Assert.That(result.SessionToken, Is.Not.EqualTo(Guid.Empty));
        }
    }
}
