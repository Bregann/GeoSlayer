using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Database.Models;

/// <summary>
/// A craftable thing (DESIGN.md §4.2), seeded from JSON.
///
/// <para>Crafting is <b>time-gated, not tap-gated</b>: a recipe takes real minutes to
/// hours, so you queue it and walk away. That is the bridge between the exploration and
/// idle halves — you craft <i>because</i> you are about to stop playing.</para>
/// </summary>
public class Recipe
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required, MaxLength(64)]
    public string Key { get; set; } = null!;

    [Required, MaxLength(128)]
    public string Name { get; set; } = null!;

    [Required, MaxLength(512)]
    public string Description { get; set; } = null!;

    /// <summary>
    /// The skill this trains. With <see cref="LevelRequired"/> this is a double gate: the
    /// skill must be unlocked via the ladder, <i>then</i> at level (§4.2).
    /// </summary>
    public SkillType SkillType { get; set; }

    public int LevelRequired { get; set; } = 1;

    /// <summary>Real seconds to complete. Higher tiers take longer (§4.1a, inverted).</summary>
    public double DurationSeconds { get; set; }

    public double XpReward { get; set; }

    /// <summary>Material produced, if this recipe outputs a material.</summary>
    public int? OutputMaterialId { get; set; }

    /// <summary>Item produced, if this recipe outputs an item.</summary>
    public int? OutputItemId { get; set; }

    public int OutputQuantity { get; set; } = 1;

    public virtual ICollection<RecipeInput> Inputs { get; set; } = new List<RecipeInput>();

    [ForeignKey(nameof(OutputMaterialId))]
    public virtual Material? OutputMaterial { get; set; }

    [ForeignKey(nameof(OutputItemId))]
    public virtual Item? OutputItem { get; set; }
}
