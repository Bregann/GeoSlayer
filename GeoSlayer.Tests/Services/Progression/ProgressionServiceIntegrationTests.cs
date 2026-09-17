using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Exceptions;
using GeoSlayer.Domain.Services.Progression;
using GeoSlayer.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Tests.Services.Progression
{
    /// <summary>
    /// Stage 02 acceptance criteria 3–8, against a real database.
    /// </summary>
    [TestFixture]
    public class ProgressionServiceIntegrationTests : DatabaseIntegrationTestBase
    {
        private ProgressionService _sut = null!;
        private Player _player = null!;

        private static CancellationToken Ct => CancellationToken.None;

        protected override async Task CustomSetUp()
        {
            var (_, player) = await TestDatabaseSeedHelper.SeedMinimalData(DbContext);
            _player = player;

            await TestDatabaseSeedHelper.SeedProgressionDefinitions(DbContext);
            _sut = TestDatabaseSeedHelper.CreateProgressionService(DbContext);
            await _sut.EnsureStartingUnlocks(_player.Id, Ct);
        }

        /// <summary>Give the player points to spend without walking there.</summary>
        private async Task GrantPoints(int points)
        {
            var player = await DbContext.Players.FirstAsync(p => p.Id == _player.Id);
            player.BonusPointsEarned += points;
            await DbContext.SaveChangesAsync();
        }

        private async Task SetAdventurerLevel(int level)
        {
            var player = await DbContext.Players.FirstAsync(p => p.Id == _player.Id);
            player.AdventurerLevel = level;
            player.AdventurerXp = XpCurve.XpForLevel(level);
            await DbContext.SaveChangesAsync();
        }

        // ── Criterion 4: starting unlocks ───────────────────────────────

        [Test]
        public async Task NewAccount_StartsWithExplorationAndForagingOnly()
        {
            var skills = await DbContext.PlayerSkills
                .Where(s => s.PlayerId == _player.Id)
                .Select(s => s.SkillType)
                .ToListAsync();

            Assert.That(skills, Is.EquivalentTo(new[] { SkillType.Exploration, SkillType.Foraging }));
        }

        [Test]
        public async Task EnsureStartingUnlocks_IsIdempotent()
        {
            await _sut.EnsureStartingUnlocks(_player.Id, Ct);
            await _sut.EnsureStartingUnlocks(_player.Id, Ct);

            var count = await DbContext.PlayerSkills.CountAsync(s => s.PlayerId == _player.Id);

            // The unique index would throw before this assertion if rows were duplicated.
            Assert.That(count, Is.EqualTo(2));
        }

        // ── Criterion 3: dual payout ────────────────────────────────────

        [Test]
        public async Task GrantXp_RaisesBothTheSkillAndAdventurerXp()
        {
            var result = await _sut.GrantXp(_player.Id, SkillType.Exploration, 100, XpSource.Walk, Ct);

            var skill = await DbContext.PlayerSkills
                .FirstAsync(s => s.PlayerId == _player.Id && s.SkillType == SkillType.Exploration);

            var player = await DbContext.Players.FirstAsync(p => p.Id == _player.Id);

            Assert.Multiple(() =>
            {
                Assert.That(skill.Xp, Is.EqualTo(100), "skill XP should be the full amount");
                Assert.That(player.AdventurerXp, Is.EqualTo(25), "Adventurer XP should be the 0.25 cut");
                Assert.That(result.SkillXpEarned, Is.EqualTo(100));
                Assert.That(result.AdventurerXpEarned, Is.EqualTo(25));
            });
        }

        [Test]
        public async Task GrantXp_IdleSource_PaysTheReducedAdventurerRatio()
        {
            // §3.3: idle pays 0.25x the normal cut, so 100 * 0.25 * 0.25 = 6 (floored).
            await _sut.GrantXp(_player.Id, SkillType.Foraging, 100, XpSource.Idle, Ct);

            var player = await DbContext.Players.FirstAsync(p => p.Id == _player.Id);

            Assert.That(player.AdventurerXp, Is.EqualTo(6));
        }

        [Test]
        public async Task GrantXp_ForALockedSkill_DoesNotUnlockIt()
        {
            // Mining unlocks at level 12 — XP arriving early must not create the row.
            await _sut.GrantXp(_player.Id, SkillType.Mining, 500, XpSource.Walk, Ct);

            var exists = await DbContext.PlayerSkills
                .AnyAsync(s => s.PlayerId == _player.Id && s.SkillType == SkillType.Mining);

            Assert.That(exists, Is.False, "the ladder is the only way a skill unlocks");
        }

        [Test]
        public async Task GrantXp_NegativeAmount_IsRejected()
        {
            await Assert.ThatAsync(
                () => _sut.GrantXp(_player.Id, SkillType.Exploration, -50, XpSource.Walk, Ct),
                Throws.TypeOf<BadRequestException>());
        }

        // ── Criterion 6: one bonus point per level ──────────────────────

        [Test]
        public async Task EachAdventurerLevel_GrantsExactlyOneBonusPoint()
        {
            // Enough Adventurer XP to cross several levels in one grant.
            var target = XpCurve.XpForLevel(5);
            await _sut.GrantXp(_player.Id, null, target * 4, XpSource.Walk, Ct);

            var player = await DbContext.Players.FirstAsync(p => p.Id == _player.Id);

            Assert.That(player.BonusPointsEarned, Is.EqualTo(player.AdventurerLevel - 1),
                "one point per level gained above the starting level 1");
        }

        // ── Criterion 5: unlocks fire at their threshold ────────────────

        [Test]
        public async Task ReachingLevel3_UnlocksFishingAndReturnsAnEvent()
        {
            var target = XpCurve.XpForLevel(3);
            var result = await _sut.GrantXp(_player.Id, null, target * 4, XpSource.Walk, Ct);

            var hasFishing = await DbContext.PlayerSkills
                .AnyAsync(s => s.PlayerId == _player.Id && s.SkillType == SkillType.Fishing);

            Assert.Multiple(() =>
            {
                Assert.That(hasFishing, Is.True, "Fishing unlocks at Adventurer level 3");
                Assert.That(result.Unlocks.Any(u => u.Payload == nameof(SkillType.Fishing)), Is.True,
                    "the unlock event should be returned so the app can celebrate it");
                Assert.That(result.AdventurerLevelledUp, Is.True);
            });
        }

        [Test]
        public async Task ASingleLargeGrant_AppliesEveryLadderRungItCrosses()
        {
            // Jump straight past 3 (Fishing) and 5 (Woodcutting + Claims).
            var target = XpCurve.XpForLevel(5);
            var result = await _sut.GrantXp(_player.Id, null, target * 4, XpSource.Walk, Ct);

            var skills = await DbContext.PlayerSkills
                .Where(s => s.PlayerId == _player.Id)
                .Select(s => s.SkillType)
                .ToListAsync();

            Assert.Multiple(() =>
            {
                Assert.That(skills, Contains.Item(SkillType.Fishing));
                Assert.That(skills, Contains.Item(SkillType.Woodcutting));
                Assert.That(result.Unlocks.Any(u => u.Payload == "Claims"), Is.True,
                    "systems unlock alongside skills");
            });
        }

        // ── Milestones (§3.0b) ──────────────────────────────────────────

        [Test]
        public async Task GrantMilestone_AddsAdventurerXpWithoutASkillCut()
        {
            await _sut.GrantMilestone(_player.Id, MilestoneType.SkillUnlock, 1, Ct);

            var player = await DbContext.Players.FirstAsync(p => p.Id == _player.Id);

            Assert.That(player.AdventurerXp,
                Is.EqualTo(ProgressionDefaults.MilestoneXp[MilestoneType.SkillUnlock]));
        }

        // ── Criterion 7 & upgrades ──────────────────────────────────────

        [Test]
        public async Task PurchaseUpgrade_SpendsPointsAndRaisesRank()
        {
            await GrantPoints(5);

            var result = await _sut.PurchaseUpgrade(_player.Id, ProgressionDefaults.UpgradeKeys.WorkerSlot, Ct);
            var upgrade = result.Upgrades.First(u => u.Key == ProgressionDefaults.UpgradeKeys.WorkerSlot);

            Assert.Multiple(() =>
            {
                Assert.That(upgrade.Rank, Is.EqualTo(1));
                Assert.That(result.BonusPointsSpent, Is.EqualTo(1), "rank 1 of Worker Slot costs 1");
                Assert.That(result.BonusPointsAvailable, Is.EqualTo(4));
            });
        }

        [Test]
        public async Task PurchaseUpgrade_WithoutEnoughPoints_IsRejected()
        {
            // Reveal Radius rank 1 costs 3; the player has 1.
            await GrantPoints(1);

            await Assert.ThatAsync(
                () => _sut.PurchaseUpgrade(_player.Id, ProgressionDefaults.UpgradeKeys.RevealRadius, Ct),
                Throws.TypeOf<BadRequestException>());
        }

        [Test]
        public async Task PurchaseUpgrade_BelowMinimumLevel_IsRejected()
        {
            // Offline Cap requires Adventurer level 3; the player is level 1.
            await GrantPoints(20);

            await Assert.ThatAsync(
                () => _sut.PurchaseUpgrade(_player.Id, ProgressionDefaults.UpgradeKeys.OfflineCap, Ct),
                Throws.TypeOf<BadRequestException>());
        }

        [Test]
        public async Task PurchaseUpgrade_BeyondMaxRank_IsRejected()
        {
            await GrantPoints(100);

            // Reveal Radius has 3 ranks.
            for (var i = 0; i < 3; i++)
            {
                await _sut.PurchaseUpgrade(_player.Id, ProgressionDefaults.UpgradeKeys.RevealRadius, Ct);
            }

            await Assert.ThatAsync(
                () => _sut.PurchaseUpgrade(_player.Id, ProgressionDefaults.UpgradeKeys.RevealRadius, Ct),
                Throws.TypeOf<BadRequestException>());
        }

        [Test]
        public async Task GetUpgradeEffect_ScalesWithRank()
        {
            await GrantPoints(10);
            await _sut.PurchaseUpgrade(_player.Id, ProgressionDefaults.UpgradeKeys.Scholar, Ct);
            await _sut.PurchaseUpgrade(_player.Id, ProgressionDefaults.UpgradeKeys.Scholar, Ct);

            var effect = await _sut.GetUpgradeEffect(_player.Id, ProgressionDefaults.UpgradeKeys.Scholar, Ct);

            Assert.That(effect, Is.EqualTo(0.10).Within(0.0001), "two ranks of +5%");
        }

        [Test]
        public async Task Scholar_MeasurablyIncreasesSkillXp()
        {
            await GrantPoints(10);

            var before = await _sut.GrantXp(_player.Id, SkillType.Exploration, 100, XpSource.Walk, Ct);

            await _sut.PurchaseUpgrade(_player.Id, ProgressionDefaults.UpgradeKeys.Scholar, Ct);

            var after = await _sut.GrantXp(_player.Id, SkillType.Exploration, 100, XpSource.Walk, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(before.SkillXpEarned, Is.EqualTo(100));
                Assert.That(after.SkillXpEarned, Is.EqualTo(105), "+5% must change actual XP, not just display");
            });
        }

        // ── Criterion 8: respec ─────────────────────────────────────────

        [Test]
        public async Task Respec_RefundsEveryPointAndClearsRanks()
        {
            await GrantPoints(10);
            await _sut.PurchaseUpgrade(_player.Id, ProgressionDefaults.UpgradeKeys.WorkerSlot, Ct);
            await _sut.PurchaseUpgrade(_player.Id, ProgressionDefaults.UpgradeKeys.WorkerSlot, Ct);

            var result = await _sut.Respec(_player.Id, Ct);

            var ranks = await DbContext.PlayerUpgrades.CountAsync(u => u.PlayerId == _player.Id);

            Assert.Multiple(() =>
            {
                Assert.That(ranks, Is.Zero, "respec should clear purchased ranks");

                // 3 points were spent; the first respec costs 1, so 9 of 10 come back.
                Assert.That(result.BonusPointsSpent, Is.EqualTo(1), "only the respec fee remains spent");
                Assert.That(result.BonusPointsAvailable, Is.EqualTo(9));
            });
        }

        [Test]
        public async Task Respec_CostEscalatesWithEachUse()
        {
            await GrantPoints(20);
            await _sut.PurchaseUpgrade(_player.Id, ProgressionDefaults.UpgradeKeys.WorkerSlot, Ct);

            var first = await _sut.Respec(_player.Id, Ct);
            Assert.That(first.RespecCost, Is.EqualTo(2), "the second respec costs more than the first");

            await _sut.PurchaseUpgrade(_player.Id, ProgressionDefaults.UpgradeKeys.WorkerSlot, Ct);
            var second = await _sut.Respec(_player.Id, Ct);

            Assert.That(second.RespecCost, Is.EqualTo(3));
        }

        // ── Criterion 9: the roadmap ────────────────────────────────────

        [Test]
        public async Task GetSkills_ReturnsUnlockedSkillsAndTheLockedLadder()
        {
            var dto = await _sut.GetSkills(_player.Id, Ct);

            var lockedFishing = dto.Locked.FirstOrDefault(l => l.Payload == nameof(SkillType.Fishing));

            Assert.Multiple(() =>
            {
                Assert.That(dto.Unlocked.Select(u => u.SkillType),
                    Is.EquivalentTo(new[] { SkillType.Exploration, SkillType.Foraging }));

                Assert.That(lockedFishing, Is.Not.Null, "locked skills must be visible as a roadmap");
                Assert.That(lockedFishing!.UnlocksAtAdventurerLevel, Is.EqualTo(3),
                    "and named with the level they arrive at");
            });
        }

        [Test]
        public async Task GetSkills_OnceUnlocked_ASkillLeavesTheLockedList()
        {
            await SetAdventurerLevel(3);
            await _sut.EnsureStartingUnlocks(_player.Id, Ct);

            var dto = await _sut.GetSkills(_player.Id, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(dto.Unlocked.Select(u => u.SkillType), Contains.Item(SkillType.Fishing));
                Assert.That(dto.Locked.Any(l => l.Payload == nameof(SkillType.Fishing)), Is.False);
            });
        }

        [Test]
        public async Task GetSkills_ShowsTheNextMaterialTier()
        {
            // SKILL-TEMPLATE.md: always something in view. A progress bar with no stated
            // destination is just a number going up.
            //
            // Needs the skill ladders, not just the terrain pools: Stage 15 detached the pool
            // materials from skills, so the "next tier" comes from SkillSeedData now.
            await TestDatabaseSeedHelper.SeedMaterialDefinitions(DbContext);
            await TestDatabaseSeedHelper.SeedSkillDefinitions(DbContext);

            var dto = await _sut.GetSkills(_player.Id, Ct);
            var foraging = dto.Unlocked.First(s => s.SkillType == SkillType.Foraging);

            Assert.Multiple(() =>
            {
                Assert.That(foraging.NextTierName, Is.Not.Null);
                Assert.That(foraging.NextTierLevel, Is.GreaterThan(foraging.Level));
            });
        }

        [Test]
        public async Task GetSkills_HasNoNextTierOnceEveryTierIsUnlocked()
        {
            await TestDatabaseSeedHelper.SeedMaterialDefinitions(DbContext);
            await TestDatabaseSeedHelper.SeedSkillDefinitions(DbContext);

            // Above the top tier's level 90 requirement.
            var row = await DbContext.PlayerSkills
                .FirstAsync(s => s.PlayerId == _player.Id && s.SkillType == SkillType.Foraging);

            row.Level = 99;
            await DbContext.SaveChangesAsync();

            var dto = await _sut.GetSkills(_player.Id, Ct);
            var foraging = dto.Unlocked.First(s => s.SkillType == SkillType.Foraging);

            Assert.That(foraging.NextTierName, Is.Null);
        }

        [Test]
        public async Task GetUpgrades_MarksLevelGatedUpgradesUnavailable()
        {
            var dto = await _sut.GetUpgrades(_player.Id, Ct);

            var offlineCap = dto.Upgrades.First(u => u.Key == ProgressionDefaults.UpgradeKeys.OfflineCap);

            Assert.Multiple(() =>
            {
                Assert.That(offlineCap.IsAvailable, Is.False, "gated at Adventurer level 3");
                Assert.That(offlineCap.MinAdventurerLevel, Is.EqualTo(3));
            });
        }
    }
}
