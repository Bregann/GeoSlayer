namespace GeoSlayer.Domain.Enums;

/// <summary>
/// Where an item equips (DESIGN.md §4.3).
///
/// Slots are what make gear <i>conditional</i> rather than merely additive: two items
/// competing for one slot is a choice, where two stacking bonuses is just a bigger number.
/// </summary>
public enum ItemSlot
{
    /// <summary>Not equippable — a tool carried, or a building placed.</summary>
    None,

    Head,
    Body,
    Feet,
    Trinket,

    /// <summary>Gathering tools. Gate access to higher-tier nodes (§4.3).</summary>
    Tool,
}
