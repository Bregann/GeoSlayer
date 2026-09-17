namespace GeoSlayer.Domain.Enums
{
    /// <summary>
    /// What an item changes (DESIGN.md §4.3).
    ///
    /// <para>Each value must be <b>read by the system it names</b>. §4.3 is explicit that an
    /// equipped item changing no behaviour is a bug, so adding a value here without a reader
    /// is worse than not adding it.</para>
    /// </summary>
    public enum ItemModifier
    {
        /// <summary>Fractional bonus to all skill XP, e.g. 0.15 for +15%.</summary>
        SkillXpPercent,

        /// <summary>Additional cells of reveal radius.</summary>
        RevealRadius,

        /// <summary>Additional metres of POI interact range.</summary>
        PoiRangeMetres,

        /// <summary>Additional hours of offline accrual.</summary>
        OfflineCapHours,

        /// <summary>
        /// Fractional bonus to what materials sell for (§5.4).
        ///
        /// <para>Was <c>StackCapPercent</c> until caps were removed. The sources are
        /// unchanged — a Storehouse, a completed Museum wing, Banking level — and so is the
        /// feel: all three reward the player who gathers more than they immediately need.
        /// Repointed rather than deleted, because a modifier nothing reads is the bug §4.3
        /// names.</para>
        /// </summary>
        SellPricePercent,

        /// <summary>Fractional bonus to worker output.</summary>
        WorkerRatePercent,

        /// <summary>Highest gathering tier this tool permits (§4.3 tools gate access).</summary>
        ToolTier,

        /// <summary>
        /// Fractional reduction in gather time within a tier (§4.3).
        ///
        /// <para>This is what makes a better tool worth crafting once you already have one
        /// that reaches your tier — access alone would make every tool above your level
        /// pointless. 0.2 means 20% faster, which shows up as more units per cell.</para>
        /// </summary>
        GatherSpeedPercent,

        /// <summary>
        /// Effective Combat levels added when resolving an encounter (§5C.3, Stage 16).
        ///
        /// <para>Expressed in <b>levels</b> rather than a win-chance fraction so it composes
        /// with the existing margin rule instead of sitting on top of it: gear makes you
        /// fight as though you were higher level, which is the same axis training moves.
        /// A percentage would have had to be clamped separately and would have let gear
        /// outrun the 95% ceiling that keeps a fight uncertain.</para>
        /// </summary>
        CombatPowerLevels,
    }
}
