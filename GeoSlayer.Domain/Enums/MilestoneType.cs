namespace GeoSlayer.Domain.Enums;

/// <summary>
/// Discrete Adventurer XP grants for things that aren't skill training (DESIGN.md §3.0b).
///
/// The list is deliberately weighted toward <i>novelty</i> — new cell, new POI, new
/// region — rather than repetition, so ranging widely unlocks systems faster than
/// grinding one spot.
/// </summary>
public enum MilestoneType
{
    /// <summary>Reveal a new cell. "Small, flat" — fires constantly.</summary>
    NewCell,

    /// <summary>First-ever visit to a given POI. Moderate; rewards exploring, not farming.</summary>
    FirstPoiVisit,

    /// <summary>First time reaching a new region/city. Large.</summary>
    NewRegion,

    /// <summary>Claim territory. Large.</summary>
    ClaimTerritory,

    /// <summary>Complete a craft. Moderate.</summary>
    CraftComplete,

    /// <summary>Unlock a new skill. Large — momentum into the next one.</summary>
    SkillUnlock,
}
