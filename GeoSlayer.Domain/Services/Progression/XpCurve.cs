namespace GeoSlayer.Domain.Services.Progression;

/// <summary>
/// The RuneScape experience curve: cumulative XP required to reach each level.
///
/// Every later system grants XP against this curve, so it is fixed here in Stage 01
/// rather than later — changing it once players have balances means a migration and a
/// rescale of every reward in the game.
///
/// XP for level L = floor( (1/4) · Σ(n=1..L-1) floor(n + 300·2^(n/7)) ).
/// Level 1 = 0, level 2 = 83, level 50 = 101,333, level 99 = 13,034,431.
///
/// Uncapped: there is no level 99 ceiling.  The table covers the common range and the
/// formula continues beyond it.
/// </summary>
public static class XpCurve
{
    /// <summary>Levels held in the precomputed table.  Beyond this the formula runs directly.</summary>
    public const int TableMaxLevel = 200;

    /// <summary>
    /// Cumulative XP needed for each level, indexed by level.  Index 0 is unused;
    /// index 1 is 0.  Built once at startup.
    /// </summary>
    private static readonly long[] Cumulative = BuildTable();

    private static long[] BuildTable()
    {
        var table = new long[TableMaxLevel + 1];

        double points = 0;

        // table[1] = 0 — a new player starts at level 1 with no XP.
        for (var level = 1; level < TableMaxLevel; level++)
        {
            points += Math.Floor(level + 300 * Math.Pow(2, level / 7.0));
            table[level + 1] = (long)Math.Floor(points / 4);
        }

        return table;
    }

    /// <summary>
    /// Total XP required to reach <paramref name="level"/> from scratch.
    /// Levels at or below 1 need nothing.
    /// </summary>
    public static long XpForLevel(int level)
    {
        if (level <= 1) return 0;
        if (level <= TableMaxLevel) return Cumulative[level];

        // Past the table, continue the same sum.  Deliberately not cached: reaching
        // level 200 is not a thing that happens on a hot path.
        double points = 0;
        for (var n = 1; n < level; n++)
            points += Math.Floor(n + 300 * Math.Pow(2, n / 7.0));

        return (long)Math.Floor(points / 4);
    }

    /// <summary>
    /// The level a player with <paramref name="totalXp"/> has reached.  Uncapped.
    /// </summary>
    public static int LevelForXp(long totalXp)
    {
        if (totalXp <= 0) return 1;

        // Binary search the table — the common case.
        if (totalXp < Cumulative[TableMaxLevel])
        {
            var low = 1;
            var high = TableMaxLevel;

            while (low < high)
            {
                var mid = (low + high + 1) / 2;

                if (Cumulative[mid] <= totalXp)
                    low = mid;
                else
                    high = mid - 1;
            }

            return low;
        }

        // Past the table, walk forward.  A player here has earned it.
        var level = TableMaxLevel;
        while (XpForLevel(level + 1) <= totalXp)
            level++;

        return level;
    }

    /// <summary>XP still needed to reach the next level from <paramref name="totalXp"/>.</summary>
    public static long XpToNextLevel(long totalXp)
    {
        var next = LevelForXp(totalXp) + 1;
        return XpForLevel(next) - totalXp;
    }
}
