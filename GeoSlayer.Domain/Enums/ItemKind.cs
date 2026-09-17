namespace GeoSlayer.Domain.Enums
{
    /// <summary>The three output classes of crafting (DESIGN.md §4.3).</summary>
    public enum ItemKind
    {
        /// <summary>Equipment with modifiers. Multiplies walk yield.</summary>
        Gear,

        /// <summary>Gates access to higher-tier gathering. Gives crafting a reason.</summary>
        Tool,

        /// <summary>Placed on a Claim; drives the idle layer.</summary>
        Building,
    }
}
