using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Services.Progression
{
    /// <summary>
    /// Starting values for the progression constants and the seeded tables.
    ///
    /// <para><b>These are tuning values, not design.</b> DESIGN.md §3.0a and §3.1 give the
    /// upgrade tree and ladder as "indicative" and say explicitly that they are seeded data
    /// so they can be retuned without a deploy. Where DESIGN.md gives a number (GLOBAL_XP_RATIO
    /// 0.25, the idle 0.25× cut, the ladder levels, the four upgrades and their effects) it is
    /// reproduced exactly. Where it gives only a word ("small, flat", "moderate", "large") the
    /// value here is derived from the ratios it states — see <see cref="MilestoneXp"/>.</para>
    ///
    /// These are defaults only: they seed <c>EnvironmentalSettings</c> on first run, and the
    /// live value is always read from the database.
    /// </summary>
    public static class ProgressionDefaults
    {
        /// <summary>Fraction of skill XP that also lands as Adventurer XP. §3.0b gives ~0.25.</summary>
        public const double GlobalXpRatio = 0.25;

        /// <summary>Idle sources pay 0.25× the normal cut. §3.3 gives this explicitly.</summary>
        public const double IdleXpRatioMultiplier = 0.25;

        /// <summary>
        /// Coin for the first respec (§3.0a, §5D.4).
        ///
        /// <para><b>Was Bonus Points.</b> §3.0a asks for respec "for a material or currency
        /// cost", and was written before a currency existed — so the fee was charged in the
        /// very resource it refunds, which reads as odd: spend 3 points to get 12 back. Coin
        /// makes the trade legible, and comes from something a player can go and earn rather
        /// than from the thing they are trying to fix.</para>
        ///
        /// <para>5,000c is deliberately high. A player at respec age — level 15 to 25,
        /// gathering tier 1 to 3 — earns roughly 90c to 1,200c per walking hour, so this is a
        /// few hours' work: a real decision, never a wall. §3.0a's "cheap the first time"
        /// referred to a points economy where 1 was nearly free; the intent was that the
        /// first correction should not be punishing, and a few hours' walking is not.</para>
        /// </summary>
        public const long RespecBaseCost = 5_000;

        /// <summary>
        /// How the respec fee escalates.
        ///
        /// <para>Doubling rather than the old linear step. §3.0a wants escalation, and
        /// doubling prices out someone treating respec as a free rebuild button while barely
        /// touching a player fixing one early mistake: 5,000 then 10,000 then 20,000.</para>
        /// </summary>
        public const double RespecCostMultiplier = 2.0;

        /// <summary>
        /// Adventurer XP per milestone (§3.0b).
        ///
        /// DESIGN.md gives these qualitatively, so they are anchored to the one rate it does
        /// pin down: a revealed cell is 2 skill XP (§3.3), which at a 0.25 ratio is 0 or 1
        /// Adventurer XP from the flat cut alone. The scale below takes "small, flat" = 1 as
        /// that anchor and steps up an order of magnitude per tier, keeping §3.0b's stated
        /// ordering — new cell &lt; POI/craft &lt; region/claim/unlock.
        /// </summary>
        public static readonly IReadOnlyDictionary<MilestoneType, long> MilestoneXp =
            new Dictionary<MilestoneType, long>
            {
                // "small, flat" — fires on every new cell, so it must stay tiny.
                [MilestoneType.NewCell] = 1,

                // "moderate — rewards exploring, not farming". ~20× a cell, matching the
                // POI-to-terrain ratio §3.3 gives for skill XP.
                [MilestoneType.FirstPoiVisit] = 20,
                [MilestoneType.CraftComplete] = 20,

                // "large" tier. Level 2 costs 83 XP on the RS curve, so a large milestone is
                // a visible fraction of an early level without being a free one.
                [MilestoneType.ClaimTerritory] = 50,
                [MilestoneType.NewRegion] = 50,

                // "large — momentum into the next one".
                [MilestoneType.SkillUnlock] = 100,
            };

        /// <summary>Upgrade keys, so callers reading effects don't stringly-type them.</summary>
        public static class UpgradeKeys
        {
            public const string WorkerSlot = "worker_slot";
            public const string RevealRadius = "reveal_radius";
            public const string Scholar = "scholar";
            public const string OfflineCap = "offline_cap";
            public const string ClaimSlot = "claim_slot";
            public const string CraftSlot = "craft_slot";
        }
    }
}
