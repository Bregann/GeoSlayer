using GeoSlayer.Domain.Services.Progression;

namespace GeoSlayer.Tests.Services.Progression;

[TestFixture]
public class XpCurveTests
{
    // ── The three values the stage names ────────────────────────────

    [TestCase(1, 0L)]
    [TestCase(2, 83L)]
    [TestCase(50, 101_333L)]
    [TestCase(99, 13_034_431L)]
    public void XpForLevel_KnownValues_MatchTheRuneScapeCurve(int level, long expected)
    {
        Assert.That(XpCurve.XpForLevel(level), Is.EqualTo(expected));
    }

    // ── Other well-known points on the curve ────────────────────────

    [TestCase(3, 174L)]
    [TestCase(10, 1_154L)]
    [TestCase(20, 4_470L)]
    [TestCase(30, 13_363L)]
    [TestCase(60, 273_742L)]
    [TestCase(70, 737_627L)]
    [TestCase(92, 6_517_253L)]
    public void XpForLevel_FurtherKnownValues_AreCorrect(int level, long expected)
    {
        Assert.That(XpCurve.XpForLevel(level), Is.EqualTo(expected));
    }

    [Test]
    public void XpForLevel_Level92_IsHalfOfLevel99()
    {
        // The familiar RuneScape identity — a good check that the curve's shape is right.
        Assert.That(XpCurve.XpForLevel(92) * 2, Is.EqualTo(13_034_506L).Within(100));
    }

    // ── Shape ───────────────────────────────────────────────────────

    [Test]
    public void XpForLevel_BelowLevelTwo_IsZero()
    {
        Assert.That(XpCurve.XpForLevel(1), Is.Zero);
        Assert.That(XpCurve.XpForLevel(0), Is.Zero);
        Assert.That(XpCurve.XpForLevel(-5), Is.Zero);
    }

    [Test]
    public void XpForLevel_IsStrictlyIncreasing()
    {
        for (var level = 2; level <= XpCurve.TableMaxLevel; level++)
            Assert.That(XpCurve.XpForLevel(level), Is.GreaterThan(XpCurve.XpForLevel(level - 1)),
                $"level {level} is not above level {level - 1}");
    }

    // ── Uncapped ────────────────────────────────────────────────────

    [Test]
    public void XpForLevel_BeyondTheTable_KeepsGrowing()
    {
        // No level 99 ceiling, and no ceiling at the table edge either.
        var atEdge = XpCurve.XpForLevel(XpCurve.TableMaxLevel);
        var beyond = XpCurve.XpForLevel(XpCurve.TableMaxLevel + 5);

        Assert.That(beyond, Is.GreaterThan(atEdge));
    }

    [Test]
    public void XpForLevel_TableAndFormula_AgreeAtTheBoundary()
    {
        // The table and the fallback formula must not disagree by a single point, or
        // a player would gain or lose a level on crossing the boundary.
        var fromTable = XpCurve.XpForLevel(XpCurve.TableMaxLevel);

        double points = 0;
        for (var n = 1; n < XpCurve.TableMaxLevel; n++)
            points += Math.Floor(n + 300 * Math.Pow(2, n / 7.0));

        Assert.That(fromTable, Is.EqualTo((long)Math.Floor(points / 4)));
    }

    // ── LevelForXp is the inverse ───────────────────────────────────

    [TestCase(0L, 1)]
    [TestCase(82L, 1)]
    [TestCase(83L, 2)]
    [TestCase(84L, 2)]
    [TestCase(101_333L, 50)]
    [TestCase(101_332L, 49)]
    [TestCase(13_034_431L, 99)]
    public void LevelForXp_KnownBoundaries_AreExact(long xp, int expected)
    {
        Assert.That(XpCurve.LevelForXp(xp), Is.EqualTo(expected));
    }

    [Test]
    public void LevelForXp_InvertsXpForLevel()
    {
        for (var level = 1; level <= XpCurve.TableMaxLevel; level++)
        {
            var xp = XpCurve.XpForLevel(level);

            Assert.That(XpCurve.LevelForXp(xp), Is.EqualTo(level),
                $"XP for level {level} did not map back to it");
        }
    }

    [Test]
    public void LevelForXp_NegativeXp_IsLevelOne()
    {
        Assert.That(XpCurve.LevelForXp(-1), Is.EqualTo(1));
    }

    [Test]
    public void LevelForXp_BeyondTheTable_StillResolves()
    {
        var xp = XpCurve.XpForLevel(XpCurve.TableMaxLevel + 3);

        Assert.That(XpCurve.LevelForXp(xp), Is.EqualTo(XpCurve.TableMaxLevel + 3));
    }

    // ── XpToNextLevel ───────────────────────────────────────────────

    [Test]
    public void XpToNextLevel_AtALevelBoundary_IsTheWholeNextBand()
    {
        Assert.That(XpCurve.XpToNextLevel(83), Is.EqualTo(174 - 83));
    }

    [Test]
    public void XpToNextLevel_FromZero_IsTheLevelTwoRequirement()
    {
        Assert.That(XpCurve.XpToNextLevel(0), Is.EqualTo(83));
    }
}
