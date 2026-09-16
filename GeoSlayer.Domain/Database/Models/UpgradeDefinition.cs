using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeoSlayer.Domain.Database.Models;

/// <summary>
/// One upgrade in the Bonus Point tree (DESIGN.md §3.0a), seeded rather than hardcoded.
///
/// Stage 02 deliberately ships four upgrades only — "four balanced beats fifteen
/// unbalanced". Later stages add more as the systems they affect arrive.
/// </summary>
public class UpgradeDefinition
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required, MaxLength(64)]
    public string Key { get; set; } = null!;

    [Required, MaxLength(128)]
    public string Name { get; set; } = null!;

    [Required, MaxLength(64)]
    public string Category { get; set; } = null!;

    public int MaxRank { get; set; }

    /// <summary>
    /// Bonus Point cost of each rank, cheapest first, as comma-separated integers
    /// (e.g. <c>"1,2,4,7,11"</c>). Length must equal <see cref="MaxRank"/>.
    ///
    /// A literal list rather than a formula because DESIGN.md §3.0a specifies escalating
    /// costs per upgrade with no shared shape, and a list is what a designer can retune.
    /// </summary>
    [Required, MaxLength(256)]
    public string CostCurve { get; set; } = null!;

    /// <summary>
    /// Magnitude of one rank's effect. Unit depends on the upgrade: cells for
    /// Reveal Radius, a fraction for percentage upgrades, hours for Offline Cap.
    /// </summary>
    public double EffectPerRank { get; set; }

    /// <summary>Adventurer level required before this appears, so the tree reveals gradually.</summary>
    public int MinAdventurerLevel { get; set; } = 1;

    [Required, MaxLength(512)]
    public string Description { get; set; } = null!;

    /// <summary>Per-rank costs parsed from <see cref="CostCurve"/>.</summary>
    [NotMapped]
    public int[] Costs =>
        CostCurve.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 .Select(int.Parse)
                 .ToArray();

    /// <summary>
    /// Bonus Point cost to go from <paramref name="currentRank"/> to the next rank,
    /// or <c>null</c> when already at <see cref="MaxRank"/>.
    /// </summary>
    public int? CostOfNextRank(int currentRank)
    {
        var costs = Costs;
        if (currentRank >= MaxRank || currentRank >= costs.Length) return null;
        return costs[currentRank];
    }

    /// <summary>Total points sunk to reach <paramref name="rank"/> — the respec refund.</summary>
    public int TotalCostThroughRank(int rank)
    {
        var costs = Costs;
        var total = 0;
        for (var i = 0; i < rank && i < costs.Length; i++) total += costs[i];
        return total;
    }
}
