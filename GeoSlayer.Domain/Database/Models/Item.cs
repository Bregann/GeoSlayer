using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Database.Models;

/// <summary>
/// Gear, a tool, or a building (DESIGN.md §4.3), seeded.
///
/// <para>Gear deliberately touches the same stats as Bonus Points — they are different
/// <i>acquisition</i> routes to similar power. Gear bonuses are kept <b>larger but
/// conditional</b> (slot-competing) so they do not merely duplicate the points tree; §4.3
/// is explicit that a gear item and an upgrade granting the same flat bonus means one is
/// redundant.</para>
/// </summary>
public class Item
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required, MaxLength(64)]
    public string Key { get; set; } = null!;

    [Required, MaxLength(128)]
    public string Name { get; set; } = null!;

    [Required, MaxLength(512)]
    public string Description { get; set; } = null!;

    public ItemKind Kind { get; set; }

    public ItemSlot Slot { get; set; }

    public ItemModifier Modifier { get; set; }

    /// <summary>
    /// Magnitude of the modifier. Unit depends on <see cref="Modifier"/> — cells, metres,
    /// hours, or a fraction.
    /// </summary>
    public double ModifierValue { get; set; }

    /// <summary>
    /// A second effect on the same item, if any (Stage 11).
    ///
    /// <para>Tools need two: <see cref="ItemModifier.ToolTier"/> gates which tiers are
    /// reachable, and <see cref="ItemModifier.GatherSpeedPercent"/> reduces gather time
    /// within a tier. Two separate items would compete for the single Tool slot, which
    /// would make the speed bonus unequippable.</para>
    /// </summary>
    public ItemModifier? SecondaryModifier { get; set; }

    public double SecondaryModifierValue { get; set; }

    public int Tier { get; set; } = 1;
}
