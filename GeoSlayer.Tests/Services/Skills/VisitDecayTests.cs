using GeoSlayer.Domain.Services.Skills;

namespace GeoSlayer.Tests.Services.Skills;

/// <summary>
/// Stage 04 criterion 6 — the decay curve from DESIGN.md §3.4.
///
/// §3.4 calls this "the single most important anti-degeneracy rule", so it is tested
/// directly against the formula rather than inferred from a database fixture.
/// </summary>
[TestFixture]
public class VisitDecayTests
{
    [Test]
    public void FirstVisit_PaysFull()
    {
        Assert.That(VisitDecay.Multiplier(0), Is.EqualTo(1.0));
    }

    [Test]
    public void SecondVisit_PaysAboutTwoThirds()
    {
        // §3.4 states "second 67%".
        Assert.That(VisitDecay.Multiplier(1), Is.EqualTo(1.0 / 1.5).Within(0.0001));
        Assert.That(VisitDecay.Multiplier(1), Is.EqualTo(0.667).Within(0.001));
    }

    [Test]
    public void FifthVisit_PaysAboutTwentyNinePercent()
    {
        // §3.4 states "fifth ~29%".
        Assert.That(VisitDecay.Multiplier(4), Is.EqualTo(0.333).Within(0.001));
        Assert.That(VisitDecay.Multiplier(5), Is.EqualTo(0.286).Within(0.001));
    }

    [Test]
    public void Decay_FloorsAtFivePercent()
    {
        // "Floors at 5% so your local pub is never literally worthless but is never a
        // farm either."
        foreach (var count in new[] { 40, 100, 1_000, int.MaxValue / 2 })
            Assert.That(VisitDecay.Multiplier(count), Is.EqualTo(VisitDecay.Floor));
    }

    [Test]
    public void Decay_IsMonotonicallyDecreasing()
    {
        // New ground must always beat old ground — a curve that ever rises would make
        // camping one POI optimal again.
        for (var n = 1; n < 200; n++)
        {
            Assert.That(VisitDecay.Multiplier(n), Is.LessThanOrEqualTo(VisitDecay.Multiplier(n - 1)),
                $"decay rose between visit {n - 1} and {n}");
        }
    }

    [Test]
    public void ANegativeCount_DoesNotPayMoreThanFull()
    {
        // Defensive: a corrupt or unset count must not become a bonus.
        Assert.That(VisitDecay.Multiplier(-5), Is.EqualTo(1.0));
    }

    // ── Charge regeneration (§3.4) ──────────────────────────────────

    [Test]
    public void OneChargeIsRegainedPerDay()
    {
        Assert.Multiple(() =>
        {
            Assert.That(VisitDecay.DecayedCount(5, TimeSpan.FromHours(23)), Is.EqualTo(5), "under a day regains nothing");
            Assert.That(VisitDecay.DecayedCount(5, TimeSpan.FromHours(24)), Is.EqualTo(4));
            Assert.That(VisitDecay.DecayedCount(5, TimeSpan.FromHours(72)), Is.EqualTo(2));
        });
    }

    [Test]
    public void EnoughTimeAway_RestoresAPoiToFull()
    {
        // "Regain 1 visit charge per 24h, max back to full over a couple of weeks."
        var count = VisitDecay.DecayedCount(5, TimeSpan.FromDays(14));

        Assert.Multiple(() =>
        {
            Assert.That(count, Is.Zero);
            Assert.That(VisitDecay.Multiplier(count), Is.EqualTo(1.0),
                "a place not visited for a fortnight should feel worth returning to");
        });
    }

    [Test]
    public void DecayedCount_NeverGoesNegative()
    {
        Assert.That(VisitDecay.DecayedCount(2, TimeSpan.FromDays(365)), Is.Zero);
    }

    [Test]
    public void DecayedCount_HandlesNegativeElapsedWithoutInflating()
    {
        // Clock skew must not be usable to inflate the count back up.
        Assert.That(VisitDecay.DecayedCount(3, TimeSpan.FromHours(-48)), Is.EqualTo(3));
    }

    // ── XP ──────────────────────────────────────────────────────────

    [Test]
    public void XpForVisit_AppliesTheMultiplier()
    {
        Assert.Multiple(() =>
        {
            Assert.That(VisitDecay.XpForVisit(100, 0), Is.EqualTo(100));
            Assert.That(VisitDecay.XpForVisit(100, 1), Is.EqualTo(66));   // floor(100/1.5)
            Assert.That(VisitDecay.XpForVisit(100, 5), Is.EqualTo(28));   // floor(100/3.5)
        });
    }

    [Test]
    public void XpForVisit_NeverReachesZero()
    {
        // A visit granting literally nothing reads as a bug; §3.4 wants "never
        // worthless", not "eventually zero".
        Assert.That(VisitDecay.XpForVisit(1, 10_000), Is.EqualTo(1));
    }
}
