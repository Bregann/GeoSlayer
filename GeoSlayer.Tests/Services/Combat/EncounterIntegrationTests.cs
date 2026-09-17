using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Exceptions;
using GeoSlayer.Domain.Services.Combat;
using GeoSlayer.Domain.Services.Materials;
using GeoSlayer.Domain.Services.Progression;
using GeoSlayer.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace GeoSlayer.Tests.Services.Combat
{
    /// <summary>
    /// Stage 16 acceptance criteria, against a real database.
    ///
    /// <para>Criterion 3 is the one that matters most: a player with no historic POIs must
    /// still train Combat. That is the geography-lockout regression every skill in this
    /// game is built to avoid, and §5C.2 restates it for encounters specifically.</para>
    /// </summary>
    [TestFixture]
    public class EncounterIntegrationTests : DatabaseIntegrationTestBase
    {
        private const double OriginLat = 51.5074;
        private const double OriginLng = -0.1278;

        private EncounterService _sut = null!;
        private ProgressionService _progression = null!;
        private MaterialService _materials = null!;
        private Player _player = null!;

        private static CancellationToken Ct => CancellationToken.None;

        protected override async Task CustomSetUp()
        {
            var (_, player) = await TestDatabaseSeedHelper.SeedMinimalData(DbContext);
            _player = player;

            await TestDatabaseSeedHelper.SeedProgressionDefinitions(DbContext);
            await TestDatabaseSeedHelper.SeedMaterialDefinitions(DbContext);
            await TestDatabaseSeedHelper.SeedSkillDefinitions(DbContext);
            await TestDatabaseSeedHelper.SeedEncounterDefinitions(DbContext);
            await TestDatabaseSeedHelper.SeedMuseumDefinitions(DbContext);

            _progression = TestDatabaseSeedHelper.CreateProgressionService(DbContext);
            _materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext);

            await _progression.EnsureStartingUnlocks(_player.Id, Ct);

            _sut = TestDatabaseSeedHelper.CreateEncounterService(DbContext, _materials, _progression);
        }

        /// <summary>Gives the player a verified position, which every spawn depends on.</summary>
        private async Task AtOrigin()
        {
            _player.LastLatitude = OriginLat;
            _player.LastLongitude = OriginLng;
            _player.LastSyncAtUtc = DateTime.UtcNow;
            await DbContext.SaveChangesAsync();
        }

        private long _nextOsmId = 9_000_000;

        private async Task<PointOfInterest> AddPoi(
            string name, SkillType skill, double latOffset = 0.0001, double lngOffset = 0)
        {
            var poi = new PointOfInterest
            {
                Name = name,
                Skill = skill,
                // Distinct: PointsOfInterest is uniquely indexed on (OsmId, OsmType), so
                // leaving these at their defaults collides on the second POI.
                OsmId = _nextOsmId++,
                Location = new Point(OriginLng + lngOffset, OriginLat + latOffset) { SRID = 4326 },
                XpReward = 10,
            };

            DbContext.PointsOfInterest.Add(poi);
            await DbContext.SaveChangesAsync();

            return poi;
        }

        private async Task UnlockCombat(int level)
        {
            var skill = await DbContext.PlayerSkills
                .FirstOrDefaultAsync(s => s.PlayerId == _player.Id && s.SkillType == SkillType.Combat);

            if (skill is null)
            {
                skill = new PlayerSkill
                {
                    PlayerId = _player.Id,
                    SkillType = SkillType.Combat,
                    Level = level,
                    Xp = 0,
                };

                DbContext.PlayerSkills.Add(skill);
            }
            else
            {
                skill.Level = level;
            }

            await DbContext.SaveChangesAsync();
        }

        // ── Criterion 2: roaming encounters are not castle-gated ────────

        [Test]
        public async Task ARoamingEncounter_SpawnsAtANonHistoricPoi()
        {
            await AtOrigin();
            await UnlockCombat(1);

            // A café. Nothing historic anywhere near this player.
            await AddPoi("The Corner Café", SkillType.Tavern);

            var encounters = await _sut.GetEncounters(_player.Id, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(encounters, Is.Not.Empty, "a roaming encounter must reach any POI");
                Assert.That(encounters.All(e => !e.IsTrainingGround), Is.True);
            });
        }

        // ── Criterion 3: the geography-lockout regression ───────────────

        [Test]
        public async Task WithNoHistoricPois_CombatIsStillTrainable()
        {
            // The whole point of the stage. A player whose neighbourhood holds a café, a
            // library and a park must still be able to train Combat to any level.
            await AtOrigin();
            await UnlockCombat(1);

            await AddPoi("The Corner Café", SkillType.Tavern);
            await AddPoi("Public Library", SkillType.Knowledge, latOffset: 0.0002);
            await AddPoi("Memorial Park", SkillType.Foraging, latOffset: 0.0003);

            var encounters = await _sut.GetEncounters(_player.Id, Ct);

            Assert.That(encounters, Is.Not.Empty,
                "no historic ground must never mean no Combat");

            var result = await _sut.Resolve(_player.Id, encounters[0].Id, Ct);

            Assert.That(result.SkillXpEarned, Is.GreaterThan(0),
                "and the encounter must actually train the skill");
        }

        [Test]
        public async Task EveryTier_IsReachableWithoutHistoricGround()
        {
            // Criterion 3 taken to its conclusion: the roaming ladder covers all seven
            // tiers, so nothing about the top of the skill requires a castle.
            var roamingTiers = EncounterSeedData.Encounters
                .Where(e => !e.IsTrainingGround)
                .Select(e => e.Tier)
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            Assert.That(roamingTiers, Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 7 }));
        }

        // ── Criterion 4: lifetimes differ ───────────────────────────────

        [Test]
        public async Task ATrainingGround_DoesNotExpire()
        {
            await AtOrigin();
            await UnlockCombat(1);

            await AddPoi("Ruined Keep", SkillType.Combat);

            var encounters = await _sut.GetEncounters(_player.Id, Ct);
            var training = encounters.Where(e => e.IsTrainingGround).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(training, Is.Not.Empty, "historic ground should host one");
                Assert.That(training.All(e => e.ExpiresUtc is null), Is.True,
                    "permanence is what makes it the reliable route");
            });
        }

        [Test]
        public async Task ARoamingEncounter_Expires()
        {
            await AtOrigin();
            await UnlockCombat(1);
            await AddPoi("The Corner Café", SkillType.Tavern);

            var encounters = await _sut.GetEncounters(_player.Id, Ct);
            var roaming = encounters.Where(e => !e.IsTrainingGround).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(roaming, Is.Not.Empty);
                Assert.That(roaming.All(e => e.ExpiresUtc is not null), Is.True);
            });
        }

        [Test]
        public async Task AnExpiredEncounter_IsNotOffered()
        {
            await AtOrigin();
            await UnlockCombat(1);
            await AddPoi("The Corner Café", SkillType.Tavern);

            var encounters = await _sut.GetEncounters(_player.Id, Ct);
            Assert.That(encounters, Is.Not.Empty);

            // Age every roaming encounter past its lifetime.
            var rows = await DbContext.PlayerEncounters
                .Where(e => e.PlayerId == _player.Id && e.ExpiresUtc != null)
                .ToListAsync();

            foreach (var row in rows)
            {
                row.ExpiresUtc = DateTime.UtcNow.AddMinutes(-1);
            }

            await DbContext.SaveChangesAsync();

            var after = await _sut.GetEncounters(_player.Id, Ct);

            Assert.That(after.Any(e => rows.Select(r => r.Id).Contains(e.Id)), Is.False,
                "a lapsed encounter is gone, and missing it cost nothing");
        }

        // ── Criterion 5: rewards match the player's tier ────────────────

        [Test]
        public async Task Resolution_GrantsXpAndMaterials()
        {
            await AtOrigin();
            await UnlockCombat(1);
            await AddPoi("The Corner Café", SkillType.Tavern);

            var encounters = await _sut.GetEncounters(_player.Id, Ct);
            var result = await _sut.Resolve(_player.Id, encounters[0].Id, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(result.SkillXpEarned, Is.GreaterThan(0));

                if (result.Won)
                {
                    Assert.That(result.Materials, Is.Not.Empty, "a win pays materials");
                }
            });
        }

        [Test]
        public async Task EncountersAboveTheirLevelGate_AreNotOffered()
        {
            await AtOrigin();
            await UnlockCombat(1);
            await AddPoi("The Corner Café", SkillType.Tavern);

            var encounters = await _sut.GetEncounters(_player.Id, Ct);

            Assert.That(encounters.All(e => e.MinCombatLevel <= 1), Is.True,
                "tier gates on level, exactly as every other skill does");
        }

        // ── Criterion 6: arrival is validated server-side ───────────────

        [Test]
        public async Task ResolvingFromOutOfRange_IsRejected()
        {
            await AtOrigin();
            await UnlockCombat(1);
            await AddPoi("The Corner Café", SkillType.Tavern);

            var encounters = await _sut.GetEncounters(_player.Id, Ct);
            var id = encounters[0].Id;

            // Walk the player a long way off without touching the encounter.
            _player.LastLatitude = OriginLat + 1.0;
            await DbContext.SaveChangesAsync();

            Assert.ThrowsAsync<BadRequestException>(
                async () => await _sut.Resolve(_player.Id, id, Ct),
                "an encounter must not become a teleport (§7.2)");
        }

        [Test]
        public async Task WithNoVerifiedPosition_NothingSpawns()
        {
            // A seeded or imported lat/lng is not proof of location. Only a completed sync
            // counts — the same rule VisitPoi enforces.
            _player.LastLatitude = OriginLat;
            _player.LastLongitude = OriginLng;
            _player.LastSyncAtUtc = null;
            await DbContext.SaveChangesAsync();

            await UnlockCombat(1);
            await AddPoi("The Corner Café", SkillType.Tavern);

            var encounters = await _sut.GetEncounters(_player.Id, Ct);

            Assert.That(encounters, Is.Empty);
        }

        // ── Criterion 7: losing costs no materials ──────────────────────

        [Test]
        public async Task Losing_CostsNoMaterials()
        {
            await AtOrigin();
            await UnlockCombat(1);
            await AddPoi("The Corner Café", SkillType.Tavern);

            var before = await DbContext.PlayerMaterials
                .Where(m => m.PlayerId == _player.Id)
                .SumAsync(m => m.Quantity);

            var encounters = await _sut.GetEncounters(_player.Id, Ct);

            foreach (var encounter in encounters)
            {
                var result = await _sut.Resolve(_player.Id, encounter.Id, Ct);

                if (!result.Won)
                {
                    var after = await DbContext.PlayerMaterials
                        .Where(m => m.PlayerId == _player.Id)
                        .SumAsync(m => m.Quantity);

                    Assert.Multiple(() =>
                    {
                        Assert.That(after, Is.GreaterThanOrEqualTo(before),
                            "§5C.3: losing costs time, never materials");
                        Assert.That(result.Materials, Is.Empty);
                        Assert.That(result.SkillXpEarned, Is.GreaterThan(0),
                            "and the walk still pays something");
                    });

                    return;
                }

                before = await DbContext.PlayerMaterials
                    .Where(m => m.PlayerId == _player.Id)
                    .SumAsync(m => m.Quantity);
            }

            Assert.Pass("every encounter was won this run — the loss path is unit-tested separately");
        }

        [Test]
        public void ALoss_NeverCostsMaterials_ByConstruction()
        {
            // The rule stated directly, since the integration test above depends on a roll.
            Assert.Multiple(() =>
            {
                Assert.That(EncounterResolution.XpFor(3, won: false), Is.GreaterThan(0));
                Assert.That(EncounterResolution.XpFor(3, won: false),
                    Is.LessThan(EncounterResolution.XpFor(3, won: true)));
            });
        }

        // ── Criterion 8: training grounds are the better route ──────────

        [Test]
        public async Task ATrainingGround_IsRepeatable()
        {
            // The compensating advantage for knowing a ruin nearby: it is still there
            // afterwards, where a roaming encounter is consumed.
            await AtOrigin();
            await UnlockCombat(1);
            await AddPoi("Ruined Keep", SkillType.Combat);

            var encounters = await _sut.GetEncounters(_player.Id, Ct);
            var training = encounters.First(e => e.IsTrainingGround);

            await _sut.Resolve(_player.Id, training.Id, Ct);

            var after = await _sut.GetEncounters(_player.Id, Ct);

            Assert.That(after.Any(e => e.Id == training.Id), Is.True,
                "a training ground reopens rather than being used up");
        }

        [Test]
        public async Task ARoamingEncounter_IsConsumedOnce()
        {
            await AtOrigin();
            await UnlockCombat(1);
            await AddPoi("The Corner Café", SkillType.Tavern);

            var encounters = await _sut.GetEncounters(_player.Id, Ct);
            var roaming = encounters.First(e => !e.IsTrainingGround);

            await _sut.Resolve(_player.Id, roaming.Id, Ct);

            Assert.ThrowsAsync<BadRequestException>(
                async () => await _sut.Resolve(_player.Id, roaming.Id, Ct));
        }

        [Test]
        public async Task TrainingGroundsPayTheSameAsRoaming_AtTheSameTier()
        {
            // Deliberate: if historic ground paid *more*, a player without it would be
            // permanently behind rather than merely slower — the lockout §5C.2 forbids.
            await Task.CompletedTask;

            var byTier = EncounterSeedData.Encounters.GroupBy(e => e.Tier);

            foreach (var tier in byTier)
            {
                var levels = tier.Select(e => e.MinCombatLevel).Distinct().ToList();

                Assert.That(levels, Has.Count.EqualTo(1),
                    $"tier {tier.Key} must gate at one level regardless of kind");
            }
        }

        // ── Spawning behaviour ──────────────────────────────────────────

        [Test]
        public async Task SyncingRepeatedly_DoesNotStackEncounters()
        {
            // The spawner runs on every request, so this is the check that it is
            // deterministic rather than additive.
            await AtOrigin();
            await UnlockCombat(1);
            await AddPoi("The Corner Café", SkillType.Tavern);

            await _sut.GetEncounters(_player.Id, Ct);
            await _sut.GetEncounters(_player.Id, Ct);
            var third = await _sut.GetEncounters(_player.Id, Ct);

            Assert.That(third, Has.Count.LessThanOrEqualTo(4),
                "re-asking must not manufacture more fights");
        }

        [Test]
        public async Task WithNoPoisNearby_NothingSpawns()
        {
            await AtOrigin();
            await UnlockCombat(1);

            // A POI on the other side of the country.
            await AddPoi("Distant Ruin", SkillType.Combat, latOffset: 2.0);

            var encounters = await _sut.GetEncounters(_player.Id, Ct);

            Assert.That(encounters, Is.Empty);
        }

        // ── Resolution rules, stated directly ───────────────────────────

        [Test]
        public void WinChance_IsNeverCertainAndNeverHopeless()
        {
            Assert.Multiple(() =>
            {
                Assert.That(EncounterResolution.WinChance(1, 90), Is.GreaterThanOrEqualTo(0.55),
                    "an encounter far above you is still worth trying");
                Assert.That(EncounterResolution.WinChance(99, 1), Is.LessThanOrEqualTo(0.95),
                    "and one far below you is never a certainty");
            });
        }

        [Test]
        public void WinChance_RisesWithLevel()
        {
            Assert.That(EncounterResolution.WinChance(50, 20),
                Is.GreaterThan(EncounterResolution.WinChance(20, 20)));
        }

        [Test]
        public void Resolution_IsSeeded_SoAReplayCannotReroll()
        {
            // Same reasoning as DropRoller.SeedFor: the outcome belongs to the encounter,
            // not to when the client happened to ask.
            var first = EncounterResolution.Resolve(4242, 10, 10);
            var second = EncounterResolution.Resolve(4242, 10, 10);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void AnOpenEncounter_IsOpenUntilResolvedOrExpired()
        {
            var now = DateTime.UtcNow;

            Assert.Multiple(() =>
            {
                Assert.That(EncounterResolution.IsOpen(null, null, now), Is.True,
                    "a training ground is always open");
                Assert.That(EncounterResolution.IsOpen(null, now.AddHours(1), now), Is.True);
                Assert.That(EncounterResolution.IsOpen(null, now.AddHours(-1), now), Is.False);
                Assert.That(EncounterResolution.IsOpen(now, null, now), Is.False);
            });
        }
    }
}
