using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.DTOs.Idle.Responses;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Exceptions;
using GeoSlayer.Domain.Services.Crafting;
using GeoSlayer.Domain.Services.Idle;
using GeoSlayer.Domain.Services.Materials;
using GeoSlayer.Domain.Services.Progression;
using GeoSlayer.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Tests.Services.Idle
{
    /// <summary>
    /// Stage 05 criteria 2, 3, 4, 6, 7, 8 and 9, against a real database.
    /// </summary>
    [TestFixture]
    public class WorkerServiceIntegrationTests : DatabaseIntegrationTestBase
    {
        private ProgressionService _progression = null!;
        private MaterialService _materials = null!;
        private CraftingService _crafting = null!;
        private WorkerService _sut = null!;
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
            _materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext, TerrainType.Woodland);
            _crafting = TestDatabaseSeedHelper.CreateCraftingService(DbContext, _progression, _materials);

            await _progression.EnsureStartingUnlocks(_player.Id, Ct);

            _sut = new WorkerService(DbContext, _progression, _materials, _crafting);
        }

        /// <summary>Reveal the 3×3 block centred on a cell, plus enough ground for density.</summary>
        private async Task RevealBlock(int centreLat, int centreLng, int extraCells = 60)
        {
            var now = DateTime.UtcNow;

            // Adjacent blocks share cells, so skip any already revealed — the unique index
            // would reject a second insert.
            var existing = (await DbContext.RevealedCells
                    .Where(r => r.PlayerId == _player.Id)
                    .Select(r => new { r.GridLat, r.GridLng })
                    .ToListAsync())
                .Select(r => (r.GridLat, r.GridLng))
                .ToHashSet();

            for (var lat = centreLat - 1; lat <= centreLat + 1; lat++)
            {
                for (var lng = centreLng - 1; lng <= centreLng + 1; lng++)
                {
                    if (!existing.Add((lat, lng)))
                    {
                        continue;
                    }

                    DbContext.RevealedCells.Add(new RevealedCell
                    {
                        PlayerId = _player.Id,
                        GridLat = lat,
                        GridLng = lng,
                        RevealedAtUtc = now,
                    });
                }
            }

            // The density cap is one Claim per 40 revealed cells (§5.1).
            for (var i = 0; i < extraCells; i++)
            {
                if (!existing.Add((centreLat + 500 + i, centreLng + 500)))
                {
                    continue;
                }

                DbContext.RevealedCells.Add(new RevealedCell
                {
                    PlayerId = _player.Id,
                    GridLat = centreLat + 500 + i,
                    GridLng = centreLng + 500,
                    RevealedAtUtc = now,
                });
            }

            await DbContext.SaveChangesAsync();
        }

        private async Task GiveMaterials(string key, int quantity)
        {
            var material = await DbContext.Materials.FirstAsync(m => m.Key == key);

            var row = await DbContext.PlayerMaterials
                .FirstOrDefaultAsync(pm => pm.PlayerId == _player.Id && pm.MaterialId == material.Id);

            if (row is null)
            {
                row = new PlayerMaterial { PlayerId = _player.Id, MaterialId = material.Id };
                DbContext.PlayerMaterials.Add(row);
            }

            row.Quantity += quantity;
            await DbContext.SaveChangesAsync();
        }

        private async Task GiveClaimMaterials()
        {
            await GiveMaterials("wild_grass", 100);
            await GiveMaterials("scrap", 100);
        }

        private async Task SetAdventurerLevel(int level)
        {
            var player = await DbContext.Players.FirstAsync(p => p.Id == _player.Id);
            player.AdventurerLevel = level;
            player.AdventurerXp = XpCurve.XpForLevel(level);
            await DbContext.SaveChangesAsync();
        }

        // ── Criterion 2: claiming ───────────────────────────────────────

        [Test]
        public async Task AFullyRevealedBlock_BecomesClaimable()
        {
            await RevealBlock(10, 10);
            await GiveClaimMaterials();

            var eligibility = await _sut.CheckClaimEligibility(_player.Id, 10, 10, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(eligibility.RevealedCells, Is.EqualTo(9));
                Assert.That(eligibility.IsEligible, Is.True, eligibility.Reason);
            });
        }

        [Test]
        public async Task APartiallyRevealedBlock_IsNotClaimable()
        {
            // Only the centre cell.
            DbContext.RevealedCells.Add(new RevealedCell
            {
                PlayerId = _player.Id,
                GridLat = 20,
                GridLng = 20,
                RevealedAtUtc = DateTime.UtcNow,
            });
            await DbContext.SaveChangesAsync();
            await GiveClaimMaterials();

            var eligibility = await _sut.CheckClaimEligibility(_player.Id, 20, 20, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(eligibility.IsEligible, Is.False);
                Assert.That(eligibility.Reason, Does.Contain("of 9 cells"));
            });
        }

        [Test]
        public async Task ClaimingDeductsMaterialsAndPersists()
        {
            await RevealBlock(30, 30);
            await GiveClaimMaterials();

            var grass = await DbContext.Materials.FirstAsync(m => m.Key == "wild_grass");
            var before = await DbContext.PlayerMaterials
                .Where(pm => pm.PlayerId == _player.Id && pm.MaterialId == grass.Id)
                .Select(pm => pm.Quantity)
                .FirstAsync();

            var claim = await _sut.CreateClaim(_player.Id, 30, 30, "Home", Ct);

            var after = await DbContext.PlayerMaterials
                .Where(pm => pm.PlayerId == _player.Id && pm.MaterialId == grass.Id)
                .Select(pm => pm.Quantity)
                .FirstAsync();

            var persisted = await DbContext.Claims.CountAsync(c => c.PlayerId == _player.Id);

            Assert.Multiple(() =>
            {
                Assert.That(claim.Name, Is.EqualTo("Home"));
                Assert.That(persisted, Is.EqualTo(1));
                Assert.That(after, Is.LessThan(before), "claiming should cost materials");
            });
        }

        [Test]
        public async Task ClaimingWithoutMaterials_IsRejected()
        {
            await RevealBlock(40, 40);

            await Assert.ThatAsync(
                () => _sut.CreateClaim(_player.Id, 40, 40, null, Ct),
                Throws.TypeOf<BadRequestException>());
        }

        [Test]
        public async Task TheDensityCap_PreventsClaimingTheWholeNeighbourhood()
        {
            // 9 cells for the block and nothing else — well under 40 per Claim.
            await RevealBlock(50, 50, extraCells: 0);
            await GiveClaimMaterials();

            var eligibility = await _sut.CheckClaimEligibility(_player.Id, 50, 50, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(eligibility.IsEligible, Is.False);
                Assert.That(eligibility.Reason, Does.Contain("Explore more ground"));
            });
        }

        [Test]
        public async Task AClaimsTerrainProfile_ComesFromItsCells()
        {
            await RevealBlock(60, 60);
            await GiveClaimMaterials();

            var claim = await _sut.CreateClaim(_player.Id, 60, 60, null, Ct);

            // The fake classifier reports Woodland for every cell.
            Assert.Multiple(() =>
            {
                Assert.That(claim.TerrainProfile.HasFlag(TerrainType.Woodland), Is.True);
                Assert.That(claim.Terrains, Contains.Item("Woodland"));
            });
        }

        [Test]
        public async Task AnOverlappingClaim_IsRejected()
        {
            // Reveal both blocks, so the overlap rule is what rejects the second rather than
            // an unrevealed cell.
            await RevealBlock(70, 70);
            await RevealBlock(71, 70, extraCells: 0);
            await GiveClaimMaterials();
            await SetAdventurerLevel(10);

            await _sut.CreateClaim(_player.Id, 70, 70, null, Ct);

            var eligibility = await _sut.CheckClaimEligibility(_player.Id, 71, 70, Ct);

            Assert.That(eligibility.Reason, Does.Contain("Overlaps"));
        }

        // ── Workers ─────────────────────────────────────────────────────

        [Test]
        public async Task APlayerStartsWithOneWorkerSlot()
        {
            var worker = await _sut.HireWorker(_player.Id, Ct);

            Assert.That(worker.Id, Is.GreaterThan(0));

            // The second exceeds capacity until a Worker Slot rank is bought.
            await Assert.ThatAsync(
                () => _sut.HireWorker(_player.Id, Ct),
                Throws.TypeOf<BadRequestException>());
        }

        [Test]
        public async Task BuyingAWorkerSlot_RaisesCapacity()
        {
            await _sut.HireWorker(_player.Id, Ct);

            var player = await DbContext.Players.FirstAsync(p => p.Id == _player.Id);
            player.BonusPointsEarned += 10;
            await DbContext.SaveChangesAsync();

            await _progression.PurchaseUpgrade(_player.Id, ProgressionDefaults.UpgradeKeys.WorkerSlot, Ct);

            var second = await _sut.HireWorker(_player.Id, Ct);

            Assert.That(second.Id, Is.GreaterThan(0));
        }

        // ── Criteria 3 & 4: terrain multiplies, never gates ─────────────

        [Test]
        public async Task AWorkerCanTrainAnyUnlockedSkill_OnAnyClaim()
        {
            await RevealBlock(80, 80);
            await GiveClaimMaterials();

            var claim = await _sut.CreateClaim(_player.Id, 80, 80, null, Ct);
            var worker = await _sut.HireWorker(_player.Id, Ct);

            // Foraging on a woodland claim is a match; the point is that no terrain check
            // blocks the assignment itself.
            var assigned = await _sut.AssignWorker(
                _player.Id, worker.Id, claim.Id, SkillType.Foraging, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(assigned.ClaimId, Is.EqualTo(claim.Id));
                Assert.That(assigned.AssignedSkill, Is.EqualTo(SkillType.Foraging));
                Assert.That(assigned.XpPerHour, Is.GreaterThan(0));
            });
        }

        [Test]
        public async Task AssigningALockedSkill_IsRejected()
        {
            await RevealBlock(90, 90);
            await GiveClaimMaterials();

            var claim = await _sut.CreateClaim(_player.Id, 90, 90, null, Ct);
            var worker = await _sut.HireWorker(_player.Id, Ct);

            // Mining is not unlocked at Adventurer 1. This is a *skill* gate, not a terrain
            // gate — the distinction §5.2 turns on.
            await Assert.ThatAsync(
                () => _sut.AssignWorker(_player.Id, worker.Id, claim.Id, SkillType.Mining, Ct),
                Throws.TypeOf<BadRequestException>());
        }

        [Test]
        public async Task AssigningToAnotherPlayersClaim_IsRejected()
        {
            await RevealBlock(100, 100);
            await GiveClaimMaterials();
            var claim = await _sut.CreateClaim(_player.Id, 100, 100, null, Ct);

            var otherUser = await TestDatabaseSeedHelper.SeedTestUser(DbContext, "intruder");
            var other = await TestDatabaseSeedHelper.SeedTestPlayer(DbContext, otherUser);
            await _progression.EnsureStartingUnlocks(other.Id, Ct);

            var theirWorker = await _sut.HireWorker(other.Id, Ct);

            await Assert.ThatAsync(
                () => _sut.AssignWorker(other.Id, theirWorker.Id, claim.Id, SkillType.Foraging, Ct),
                Throws.TypeOf<BadRequestException>());
        }

        // ── Criteria 5, 6, 7: accrual through the service ───────────────

        [Test]
        public async Task AnAssignedWorker_AccruesXpOverTime()
        {
            var (claim, worker) = await StationedWorker(110);

            await BackdateCollection(worker.Id, TimeSpan.FromHours(3));

            var result = await _sut.CollectOfflineAccrual(_player.Id, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(result.HasAccrual, Is.True);
                Assert.That(result.Skills, Is.Not.Empty);
                Assert.That(result.Skills[0].XpEarned, Is.GreaterThan(0));
                Assert.That(result.HoursAccrued, Is.EqualTo(3).Within(0.1));
            });
        }

        [Test]
        public async Task AccrualCaps_AtTheConfiguredMaximum()
        {
            var (_, worker) = await StationedWorker(120);

            await BackdateCollection(worker.Id, TimeSpan.FromHours(30));

            var result = await _sut.CollectOfflineAccrual(_player.Id, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(result.HoursAccrued, Is.EqualTo(OfflineAccrual.BaseOfflineCapHours).Within(0.1));
                Assert.That(result.WasCapped, Is.True);
            });
        }

        [Test]
        public async Task CollectingTwice_DoesNotPayTwice()
        {
            var (_, worker) = await StationedWorker(130);
            await BackdateCollection(worker.Id, TimeSpan.FromHours(3));

            var first = await _sut.CollectOfflineAccrual(_player.Id, Ct);
            var second = await _sut.CollectOfflineAccrual(_player.Id, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(first.HasAccrual, Is.True);
                Assert.That(second.HasAccrual, Is.False, "the collection point should have advanced");
            });
        }

        [Test]
        public async Task NothingAccrued_ReportsNoAccrualSoTheScreenStaysQuiet()
        {
            await StationedWorker(140);

            // No elapsed time since the worker was assigned.
            var result = await _sut.CollectOfflineAccrual(_player.Id, Ct);

            Assert.That(result.HasAccrual, Is.False, "never nag when nothing happened");
        }

        [Test]
        public async Task AnUnassignedWorker_AccruesNothing()
        {
            await _sut.HireWorker(_player.Id, Ct);

            var result = await _sut.CollectOfflineAccrual(_player.Id, Ct);

            Assert.That(result.HasAccrual, Is.False);
        }

        // ── Criterion 7: idle uses the reduced Adventurer ratio ─────────

        [Test]
        public async Task IdleXp_UsesTheReducedAdventurerRatio()
        {
            var (_, worker) = await StationedWorker(150);
            await BackdateCollection(worker.Id, TimeSpan.FromHours(4));

            var result = await _sut.CollectOfflineAccrual(_player.Id, Ct);

            var skillXp = result.Skills.Sum(s => s.XpEarned);

            // Walking pays floor(xp * 0.25); idle pays a quarter of that again (§3.3).
            var walkingCut = (long)Math.Floor(skillXp * ProgressionDefaults.GlobalXpRatio);

            Assert.Multiple(() =>
            {
                Assert.That(skillXp, Is.GreaterThan(0));
                Assert.That(result.AdventurerXpEarned, Is.LessThan(walkingCut),
                    "idle must not drive the unlock ladder at walking pace");
            });
        }

        // ── Criterion 6: stack caps and Dust ────────────────────────────

        [Test]
        public async Task AWorkerAtStackCap_KeepsProducingAndOverflowsToDust()
        {
            var (_, worker) = await StationedWorker(160);

            // Fill every Foraging stack the worker could produce into.
            var foragingMaterials = await DbContext.Materials
                .Where(m => m.SkillType == SkillType.Foraging)
                .ToListAsync();

            foreach (var material in foragingMaterials)
            {
                // Upsert: the claim cost already created a row for wild_grass, so a blind
                // insert would violate the unique index.
                var row = await DbContext.PlayerMaterials
                    .FirstOrDefaultAsync(pm => pm.PlayerId == _player.Id && pm.MaterialId == material.Id);

                if (row is null)
                {
                    row = new PlayerMaterial { PlayerId = _player.Id, MaterialId = material.Id };
                    DbContext.PlayerMaterials.Add(row);
                }

                row.Quantity = material.StackCap;
            }

            await DbContext.SaveChangesAsync();
            await BackdateCollection(worker.Id, TimeSpan.FromHours(4));

            // Must not throw, and must still pay XP — workers never hard-stall (§7.4).
            var result = await _sut.CollectOfflineAccrual(_player.Id, Ct);

            Assert.That(result.Skills.Sum(s => s.XpEarned), Is.GreaterThan(0),
                "a full inventory must not stop the worker");
        }

        // ── Worker upkeep (§5.2, deferred from Stage 05 to Stage 09) ────

        [Test]
        public async Task Workers_ConsumeFoodAsUpkeep()
        {
            var (_, worker) = await StationedWorker(170);
            await GiveFood("dried_rations", 20);
            await BackdateCollection(worker.Id, TimeSpan.FromHours(3));

            var before = await FoodHeld("dried_rations");
            var result = await _sut.CollectOfflineAccrual(_player.Id, Ct);
            var after = await FoodHeld("dried_rations");

            // FoodRequired rounds up, and the elapsed window is a few microseconds over 3h
            // because assignment stamps LastCollectedAtUtc before the test backdates it. So
            // 4 is correct, not 3 — assert the rule rather than a figure that depends on
            // sub-second timing.
            Assert.Multiple(() =>
            {
                Assert.That(result.UpkeepConsumed.FoodRequired, Is.EqualTo(4),
                    "3h a fraction over, rounded up");
                Assert.That(result.UpkeepConsumed.FoodConsumed, Is.EqualTo(result.UpkeepConsumed.FoodRequired));
                Assert.That(after, Is.EqualTo(before - result.UpkeepConsumed.FoodConsumed));
                Assert.That(result.WorkersWentUnfed, Is.False);
            });
        }

        [Test]
        public async Task UnfedWorkers_StillKeepWhatTheyEarned()
        {
            // §7.4 outranks the sink: an unfed worker idles, it does not lose the night.
            // Voiding hours already accrued would be exactly the "punished for sleeping"
            // failure the design forbids.
            var (_, worker) = await StationedWorker(180);
            await BackdateCollection(worker.Id, TimeSpan.FromHours(3));

            var result = await _sut.CollectOfflineAccrual(_player.Id, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(result.WorkersWentUnfed, Is.True, "there is no food");
                Assert.That(result.Skills.Sum(s => s.XpEarned), Is.GreaterThan(0),
                    "but the XP already earned must stand");
                Assert.That(result.HasAccrual, Is.True);
            });
        }

        [Test]
        public async Task UpkeepEatsTheCheapestFoodFirst()
        {
            // A player's Ambrosia should not be eaten while rations sit in the bag.
            var (_, worker) = await StationedWorker(190);
            await GiveFood("dried_rations", 10);
            await GiveFood("hearty_pie", 10);

            await BackdateCollection(worker.Id, TimeSpan.FromHours(2));
            await _sut.CollectOfflineAccrual(_player.Id, Ct);

            // Rounded up to 3 for a window a fraction over 2h.
            Assert.Multiple(async () =>
            {
                Assert.That(await FoodHeld("dried_rations"), Is.EqualTo(7), "tier 1 eaten first");
                Assert.That(await FoodHeld("hearty_pie"), Is.EqualTo(10), "tier 3 untouched");
            });
        }

        [Test]
        public async Task PartialFood_FeedsWhatItCanAndFlagsTheShortfall()
        {
            var (_, worker) = await StationedWorker(200);
            await GiveFood("dried_rations", 1);
            await BackdateCollection(worker.Id, TimeSpan.FromHours(4));

            var result = await _sut.CollectOfflineAccrual(_player.Id, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(result.UpkeepConsumed.FoodRequired, Is.GreaterThanOrEqualTo(4));
                Assert.That(result.UpkeepConsumed.FoodConsumed, Is.EqualTo(1), "only one unit was held");
                Assert.That(result.WorkersWentUnfed, Is.True, "so the app can nudge the player to cook");
            });
        }

        /// <summary>Give the player a cooked material, upserting like the other helpers.</summary>
        private async Task GiveFood(string key, int quantity)
        {
            var material = await DbContext.Materials.FirstAsync(m => m.Key == key);

            var row = await DbContext.PlayerMaterials
                .FirstOrDefaultAsync(pm => pm.PlayerId == _player.Id && pm.MaterialId == material.Id);

            if (row is null)
            {
                row = new PlayerMaterial { PlayerId = _player.Id, MaterialId = material.Id };
                DbContext.PlayerMaterials.Add(row);
            }

            row.Quantity += quantity;
            await DbContext.SaveChangesAsync();
        }

        private async Task<long> FoodHeld(string key)
        {
            var material = await DbContext.Materials.FirstAsync(m => m.Key == key);

            return await DbContext.PlayerMaterials
                .Where(pm => pm.PlayerId == _player.Id && pm.MaterialId == material.Id)
                .Select(pm => pm.Quantity)
                .FirstOrDefaultAsync();
        }

        // ── Criterion 8: no ticking job ─────────────────────────────────

        [Test]
        public void NoHangfireRecurringJob_TicksWorkers()
        {
            // §5.3 is explicit: lazy evaluation, no Hangfire job. A ticking model costs
            // O(players × workers) forever including for everyone asleep. This asserts the
            // decision rather than relying on someone remembering it.
            var root = FindRepositoryRoot();

            var offenders = new List<string>();

            foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains("/obj/") || file.Contains("/bin/"))
                {
                    continue;
                }

                if (file.Contains("Tests"))
                {
                    continue;
                }

                foreach (var (line, index) in File.ReadAllLines(file).Select((l, i) => (l, i)))
                {
                    if (line.Contains("AddOrUpdate") || line.Contains("RecurringJob"))
                    {
                        offenders.Add($"{Path.GetFileName(file)}:{index + 1}  {line.Trim()}");
                    }
                }
            }

            Assert.That(offenders, Is.Empty,
                "workers must be computed lazily, not ticked:\n" + string.Join("\n", offenders));
        }

        // ── Helpers ─────────────────────────────────────────────────────

        private async Task<(ClaimDto Claim, WorkerDto Worker)> StationedWorker(int centre)
        {
            await RevealBlock(centre, centre);
            await GiveClaimMaterials();

            var claim = await _sut.CreateClaim(_player.Id, centre, centre, null, Ct);
            var worker = await _sut.HireWorker(_player.Id, Ct);

            var assigned = await _sut.AssignWorker(
                _player.Id, worker.Id, claim.Id, SkillType.Foraging, Ct);

            return (claim, assigned);
        }

        /// <summary>Move a worker's collection point back, to simulate an absence.</summary>
        private async Task BackdateCollection(int workerId, TimeSpan by)
        {
            var worker = await DbContext.Workers.FirstAsync(w => w.Id == workerId);
            worker.LastCollectedAtUtc -= by;
            await DbContext.SaveChangesAsync();
        }

        private static string FindRepositoryRoot()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "GeoSlayer.sln")))
            {
                dir = dir.Parent;
            }

            return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
        }
    }
}
