using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Services.Retention;

/// <summary>
/// Matching contiguous Claims against District definitions (DESIGN.md §5.5).
///
/// <para>Pure and static, so the contiguity and variety rules can be asserted without a
/// database. The variety requirement is the whole point: without it the optimal play is
/// nine identical cells of your best terrain.</para>
/// </summary>
public static class DistrictMatching
{
    /// <summary>A Claim reduced to what District matching needs.</summary>
    public readonly record struct ClaimNode(int Id, int CentreGridLat, int CentreGridLng, TerrainType Terrain);

    /// <summary>
    /// Groups of Claims that touch each other.
    ///
    /// <para>Adjacency is generous — within two cells of centre, which is one Claim's
    /// width apart for the 3×3 shape. Requiring exact edge contact would make a District
    /// nearly impossible to assemble from real geography.</para>
    /// </summary>
    public static List<List<ClaimNode>> ContiguousGroups(IReadOnlyList<ClaimNode> claims, int adjacency = 4)
    {
        var remaining = claims.ToList();
        var groups = new List<List<ClaimNode>>();

        while (remaining.Count > 0)
        {
            var group = new List<ClaimNode>();
            var frontier = new Queue<ClaimNode>();

            frontier.Enqueue(remaining[0]);
            remaining.RemoveAt(0);

            while (frontier.Count > 0)
            {
                var node = frontier.Dequeue();
                group.Add(node);

                for (var i = remaining.Count - 1; i >= 0; i--)
                {
                    var other = remaining[i];

                    var touching =
                        Math.Abs(other.CentreGridLat - node.CentreGridLat) <= adjacency
                        && Math.Abs(other.CentreGridLng - node.CentreGridLng) <= adjacency;

                    if (!touching) continue;

                    frontier.Enqueue(other);
                    remaining.RemoveAt(i);
                }
            }

            groups.Add(group);
        }

        return groups;
    }

    /// <summary>
    /// Whether a group satisfies a District definition.
    ///
    /// <para>Every required terrain must appear <b>somewhere</b> in the group — a group of
    /// identical Claims can never satisfy a multi-terrain District, which is exactly the
    /// behaviour §5.5 wants.</para>
    /// </summary>
    public static bool Satisfies(IReadOnlyList<ClaimNode> group, DistrictDefinition definition)
    {
        if (group.Count < definition.MinimumClaims) return false;

        var combined = group.Aggregate(TerrainType.Open, (acc, c) => acc | c.Terrain);

        return (combined & definition.RequiredTerrains) == definition.RequiredTerrains;
    }

    /// <summary>
    /// The best bonus a player's Claims earn.
    ///
    /// Returns the single highest rather than stacking every match, so a large varied
    /// holding does not multiply out of control.
    /// </summary>
    public static (DistrictDefinition? Definition, double Bonus) BestMatch(
        IReadOnlyList<ClaimNode> claims,
        IReadOnlyList<DistrictDefinition> definitions)
    {
        DistrictDefinition? best = null;

        foreach (var group in ContiguousGroups(claims))
        {
            foreach (var definition in definitions)
            {
                if (!Satisfies(group, definition)) continue;
                if (best is null || definition.OutputBonus > best.OutputBonus) best = definition;
            }
        }

        return (best, best?.OutputBonus ?? 0);
    }

    /// <summary>What a group still needs to satisfy a definition.</summary>
    public readonly record struct Shortfall(
        DistrictDefinition Definition,
        List<TerrainType> MissingTerrains,
        int MissingClaims);

    /// <summary>
    /// The District a player is closest to forming, when they have none.
    ///
    /// <para>"No District" is otherwise a dead end. §5.5 requires varied terrain on
    /// purpose, so a player holding nine identical Claims has no way to discover that the
    /// variety is the problem — the status just reads empty. This names the nearest target
    /// and what it is short of.</para>
    ///
    /// <para>Closest means fewest missing terrains, then fewest missing Claims, then the
    /// larger bonus. Terrain is weighted first because claiming new ground of a terrain you
    /// lack is the harder ask, so it is the more useful thing to tell someone.</para>
    /// </summary>
    public static Shortfall? NearestMiss(
        IReadOnlyList<ClaimNode> claims,
        IReadOnlyList<DistrictDefinition> definitions)
    {
        // Measured against the best single group rather than the whole holding: Claims on
        // opposite sides of a city can never combine, so counting them together would
        // promise a District that is unreachable where the player actually is.
        var groups = ContiguousGroups(claims);

        Shortfall? nearest = null;

        foreach (var definition in definitions)
        {
            foreach (var group in groups.DefaultIfEmpty([]))
            {
                if (Satisfies(group, definition)) continue;

                var combined = group.Aggregate(TerrainType.Open, (acc, c) => acc | c.Terrain);

                var missing = Enum.GetValues<TerrainType>()
                    .Where(t => t != TerrainType.Open
                             && definition.RequiredTerrains.HasFlag(t)
                             && !combined.HasFlag(t))
                    .ToList();

                var shortBy = Math.Max(0, definition.MinimumClaims - group.Count);
                var candidate = new Shortfall(definition, missing, shortBy);

                if (nearest is null || IsCloser(candidate, nearest.Value)) nearest = candidate;
            }
        }

        return nearest;
    }

    private static bool IsCloser(Shortfall candidate, Shortfall incumbent)
    {
        if (candidate.MissingTerrains.Count != incumbent.MissingTerrains.Count)
            return candidate.MissingTerrains.Count < incumbent.MissingTerrains.Count;

        if (candidate.MissingClaims != incumbent.MissingClaims)
            return candidate.MissingClaims < incumbent.MissingClaims;

        return candidate.Definition.OutputBonus > incumbent.Definition.OutputBonus;
    }
}
