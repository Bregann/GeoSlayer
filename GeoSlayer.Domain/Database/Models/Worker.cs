using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Database.Models;

/// <summary>
/// The idle engine, and the accessibility floor for every skill (DESIGN.md §5.2).
///
/// <para>A working Worker produces two things at once: materials, and skill XP in
/// <see cref="AssignedSkill"/>. That dual output is what makes workers load-bearing
/// rather than decorative — they are the answer to "what if the geography near me is
/// bad".</para>
/// </summary>
public class Worker
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int PlayerId { get; set; }

    /// <summary>The Claim this worker is stationed on, or null when unassigned.</summary>
    public int? ClaimId { get; set; }

    [Required, MaxLength(64)]
    public string Name { get; set; } = null!;

    /// <summary>Higher tiers produce faster. Tier 1 is the starting worker.</summary>
    public int Tier { get; set; } = 1;

    /// <summary>
    /// What this worker trains. <b>Any unlocked skill on any Claim</b> — terrain changes
    /// the rate, never the eligibility (§5.2).
    /// </summary>
    public SkillType? AssignedSkill { get; set; }

    public DateTime StartedAtUtc { get; set; }

    /// <summary>
    /// When accrual was last collected. Offline yield is computed lazily from this on the
    /// next sync (§5.3) — there is deliberately no ticking job.
    /// </summary>
    public DateTime LastCollectedAtUtc { get; set; }

    [ForeignKey(nameof(PlayerId))]
    public virtual Player Player { get; set; } = null!;

    [ForeignKey(nameof(ClaimId))]
    public virtual Claim? Claim { get; set; }
}
