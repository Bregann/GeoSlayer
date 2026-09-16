using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Database.Models;

/// <summary>
/// Display and classification metadata for one skill (Stage 04 task 1), seeded.
///
/// Exists so the skills screen and later stages can describe a skill without a C# switch.
/// Adding a skill should be seed data, never a new branch.
/// </summary>
public class SkillDefinition
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public SkillType SkillType { get; set; }

    [Required, MaxLength(64)]
    public string Name { get; set; } = null!;

    [Required, MaxLength(512)]
    public string Description { get; set; } = null!;

    [Required, MaxLength(16)]
    public string Icon { get; set; } = null!;

    /// <summary>
    /// Adventurer level this arrives at. Mirrors the ladder in <c>UnlockDefinition</c>
    /// for display purposes; the ladder remains the authority on what actually unlocks.
    /// </summary>
    public int UnlockLevel { get; set; } = 1;

    public SkillCategory Category { get; set; }
}
