namespace GeoSlayer.Domain.Enums
{
    /// <summary>
    /// Clue scroll tiers (DESIGN.md §5B.1), scaling step count, travel distance and reward
    /// quality.
    ///
    /// <para><b>One active scroll per tier</b> — that rule is what stops hoarding and keeps
    /// each scroll meaningful.</para>
    /// </summary>
    public enum ClueTier
    {
        /// <summary>A short walk. Two steps, close to home.</summary>
        Wandering,

        /// <summary>An afternoon. Three steps, a wider radius.</summary>
        Roaming,

        /// <summary>A day out. Four steps.</summary>
        Pilgrim,

        /// <summary>A weekend. Five steps, the widest radius and the best rewards.</summary>
        Odyssey,
    }
}
