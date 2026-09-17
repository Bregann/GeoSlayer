namespace GeoSlayer.Domain.Services.Idle;

/// <summary>
/// How long an expedition takes and what it yields (DESIGN.md §5.4).
///
/// <para>Pure and static, so the distance curve can be asserted directly rather than
/// inferred from a fixture.</para>
/// </summary>
public static class ExpeditionMath
{
    /// <summary>Even a POI on the doorstep occupies the worker for a while.</summary>
    public const double MinimumHours = 2;

    /// <summary>
    /// Cap on duration. A POI on another continent should be a long trip, not an
    /// abandoned worker — §5.4 wants "high-value, low-frequency", not "gone for a month".
    /// </summary>
    public const double MaximumHours = 48;

    /// <summary>Hours added per kilometre of real-world distance.</summary>
    public const double HoursPerKilometre = 0.5;

    /// <summary>
    /// Duration for a round trip to a POI <paramref name="distanceMetres"/> away.
    /// </summary>
    public static TimeSpan Duration(double distanceMetres)
    {
        var km = Math.Max(0, distanceMetres) / 1000.0;
        var hours = Math.Clamp(MinimumHours + km * HoursPerKilometre, MinimumHours, MaximumHours);

        return TimeSpan.FromHours(hours);
    }

    /// <summary>
    /// Material units an expedition returns.
    ///
    /// <para>Scales with distance, so a far-off POI is worth the worker's absence — that
    /// is the entire reason to send one somewhere distant rather than keeping it on a
    /// Claim.</para>
    /// </summary>
    public static int MaterialYield(double distanceMetres, int workerTier)
    {
        var km = Math.Max(0, distanceMetres) / 1000.0;

        // Square-root rather than linear: a 100 km trip should beat a 10 km one, but not
        // by ten times, or nothing else would ever be worth doing.
        var distanceFactor = 1 + Math.Sqrt(km);

        var tierFactor = 1 + Math.Max(0, workerTier - 1) * OfflineAccrual.PerTierBonus;

        return Math.Max(1, (int)Math.Round(4 * distanceFactor * tierFactor));
    }

    /// <summary>Skill XP an expedition returns, on the same curve as its materials.</summary>
    public static long XpYield(double distanceMetres, int workerTier)
    {
        var hours = Duration(distanceMetres).TotalHours;
        var tierFactor = 1 + Math.Max(0, workerTier - 1) * OfflineAccrual.PerTierBonus;

        // Paid at the ordinary idle rate for the time occupied, so an expedition is not a
        // faster way to level than leaving the worker on a Claim — it is a way to reach
        // *materials* you otherwise could not.
        return (long)Math.Floor(OfflineAccrual.BaseXpPerHour * hours * tierFactor);
    }
}
