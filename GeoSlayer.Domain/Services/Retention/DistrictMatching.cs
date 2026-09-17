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
}
