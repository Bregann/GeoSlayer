using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Services.Idle;

namespace GeoSlayer.Tests.Services.Idle;

/// <summary>
/// Stage 05 criteria 3, 4 and 5 — the accrual rules from DESIGN.md §5.2–5.3.
///
/// Pure, so the terrain rule §5.2 calls "load-bearing" can be asserted directly rather
/// than inferred from a fixture.
/// </summary>
[TestFixture]
public class OfflineAccrualTests
{
    private static readonly DateTime Now = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);

    // ── Criterion 5: elapsed time and the cap ───────────────────────

    [Test]
    public void ThreeHours_AccruesThreeHoursWorth()
    {
        var accrual = OfflineAccrual.Compute(
            Now.AddHours(-3), Now, capHours: 4, tier: 1,
            TerrainType.Open, TerrainType.Open);

        Assert.Multiple(() =>
        {
            Assert.That(accrual.Elapsed.TotalHours, Is.EqualTo(3).Within(0.001));
            Assert.That(accrual.SkillXp, Is.EqualTo(OfflineAccrual.BaseXpPerHour * 3).Within(0.001));
            Assert.That(accrual.WasCapped, Is.False);
        });
    }

    [Test]
    public void ThirtyHours_CapsAtTheConfiguredMaximum()
    {
        var accrual = OfflineAccrual.Compute(
            Now.AddHours(-30), Now, capHours: 4, tier: 1,
            TerrainType.Open, TerrainType.Open);

        Assert.Multiple(() =>
        {
            Assert.That(accrual.Elapsed.TotalHours, Is.EqualTo(4).Within(0.001));
            Assert.That(accrual.SkillXp, Is.EqualTo(OfflineAccrual.BaseXpPerHour * 4).Within(0.001));
            Assert.That(accrual.WasCapped, Is.True, "the app should be able to suggest a longer cap");
        });
    }

    [Test]
    public void TheCapExtends_WithTheOfflineCapUpgrade()
    {
        // Base 4h plus two ranks of +2h.
        var accrual = OfflineAccrual.Compute(
            Now.AddHours(-30), Now, capHours: 8, tier: 1,
            TerrainType.Open, TerrainType.Open);

        Assert.That(accrual.Elapsed.TotalHours, Is.EqualTo(8).Within(0.001));
    }

    [Test]
    public void ABackwardsClock_AccruesNothingRatherThanNegative()
    {
        var accrual = OfflineAccrual.Compute(
            Now.AddHours(2), Now, capHours: 4, tier: 1,
            TerrainType.Open, TerrainType.Open);

        Assert.Multiple(() =>
        {
            Assert.That(accrual.Elapsed, Is.EqualTo(TimeSpan.Zero));
            Assert.That(accrual.SkillXp, Is.Zero);
        });
    }

    [Test]
    public void AZeroCap_AccruesNothing()
    {
        var accrual = OfflineAccrual.Compute(
            Now.AddHours(-10), Now, capHours: 0, tier: 1,
            TerrainType.Open, TerrainType.Open);

        Assert.That(accrual.SkillXp, Is.Zero);
    }

    // ── Criteria 3 & 4: terrain multiplies, never gates ─────────────

    [Test]
    public void AMismatchedClaim_StillProducesAtBaseRate()
    {
        // The rule §5.2 calls load-bearing. A worker fishing on rocky ground must still
        // produce — anything else is the geographic lockout rebuilt.
        var accrual = OfflineAccrual.Compute(
            Now.AddHours(-4), Now, capHours: 4, tier: 1,
            claimTerrain: TerrainType.Rocky,
            skillTerrains: TerrainType.Water);

        Assert.Multiple(() =>
        {
            Assert.That(accrual.SkillXp, Is.GreaterThan(0), "a mismatch must never mean zero");
            Assert.That(accrual.MaterialUnits, Is.GreaterThan(0));
            Assert.That(accrual.TerrainMultiplier, Is.EqualTo(1.0), "base rate");
        });
    }

    [Test]
    public void AMatchingClaim_ProducesAtTheMultiplierRate()
    {
        var matched = OfflineAccrual.Compute(
            Now.AddHours(-4), Now, capHours: 4, tier: 1,
            TerrainType.Woodland, TerrainType.Woodland);

        var mismatched = OfflineAccrual.Compute(
            Now.AddHours(-4), Now, capHours: 4, tier: 1,
            TerrainType.Rocky, TerrainType.Woodland);

        Assert.Multiple(() =>
        {
            Assert.That(matched.TerrainMultiplier, Is.EqualTo(OfflineAccrual.MatchingTerrainMultiplier));
            Assert.That(matched.SkillXp, Is.GreaterThan(mismatched.SkillXp));
            Assert.That(matched.SkillXp / mismatched.SkillXp,
                Is.EqualTo(OfflineAccrual.MatchingTerrainMultiplier).Within(0.001));
        });
    }

    [Test]
    public void APartialTerrainMatch_Counts()
    {
        // A Claim spanning woodland and water should suit a woodland skill.
        Assert.That(
            OfflineAccrual.TerrainMatches(
                TerrainType.Woodland | TerrainType.Water, TerrainType.Woodland),
            Is.True);
    }

    [Test]
    public void OpenTerrain_IsNeverAMatch()
    {
        // Open means "nothing identifiable", so it must not count as a bonus — otherwise
        // unclassified ground would silently be the best terrain in the game.
        Assert.Multiple(() =>
        {
            Assert.That(OfflineAccrual.TerrainMatches(TerrainType.Open, TerrainType.Woodland), Is.False);
            Assert.That(OfflineAccrual.TerrainMatches(TerrainType.Woodland, TerrainType.Open), Is.False);
        });
    }

    [Test]
    public void EveryTerrainCombination_ProducesSomething()
    {
        var terrains = new[]
        {
            TerrainType.Open, TerrainType.Woodland, TerrainType.Water, TerrainType.Farmland,
            TerrainType.Urban, TerrainType.Industrial, TerrainType.Rocky, TerrainType.Coastal,
        };

        foreach (var claim in terrains)
        {
            foreach (var skill in terrains)
            {
                var accrual = OfflineAccrual.Compute(
                    Now.AddHours(-4), Now, capHours: 4, tier: 1, claim, skill);

                Assert.That(accrual.SkillXp, Is.GreaterThan(0),
                    $"a worker on {claim} training a {skill} skill produced nothing");
            }
        }
    }

    // ── Tiers ───────────────────────────────────────────────────────

    [Test]
    public void HigherTiers_ProduceMore()
    {
        var tier1 = OfflineAccrual.Compute(Now.AddHours(-4), Now, 4, 1, TerrainType.Open, TerrainType.Open);
        var tier3 = OfflineAccrual.Compute(Now.AddHours(-4), Now, 4, 3, TerrainType.Open, TerrainType.Open);

        Assert.That(tier3.SkillXp, Is.GreaterThan(tier1.SkillXp));
    }

    // ── The balance guardrail (§5.2) ────────────────────────────────

    [Test]
    public void WorkerXpRate_SitsWellBelowWalking()
    {
        // §5.2: "if a player can rationally decide to stop walking because the workers
        // have it covered, the rate is wrong."
        //
        // A modest 2.5 km walk through woodland reveals on the order of 100 cells at
        // 3 XP each. A full 4-hour idle cycle on ideal terrain must not approach that.
        const double modestWalkXp = 100 * 3;

        var bestIdle = OfflineAccrual.Compute(
            Now.AddHours(-4), Now, capHours: 4, tier: 1,
            TerrainType.Woodland, TerrainType.Woodland);

        Assert.That(bestIdle.SkillXp, Is.LessThan(modestWalkXp / 4),
            $"idle pays {bestIdle.SkillXp:F0} against {modestWalkXp:F0} for a short walk — too close");
    }

    // ── Cap timestamp, for one-shot notifications (§5.3) ────────────

    [Test]
    public void CapReachedAt_IsTheCollectionPointPlusTheCap()
    {
        var reached = OfflineAccrual.CapReachedAtUtc(Now, 4);

        Assert.That(reached, Is.EqualTo(Now.AddHours(4)));
    }
}
