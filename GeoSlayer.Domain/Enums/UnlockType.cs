namespace GeoSlayer.Domain.Enums
{
    /// <summary>What a rung of the unlock ladder grants (DESIGN.md §3.1).</summary>
    public enum UnlockType
    {
        /// <summary>A skill becomes trainable — creates the <c>PlayerSkill</c> row.</summary>
        Skill,

        /// <summary>A whole system (Claims, Crafting, Buildings…) becomes available.</summary>
        System,
    }
}
