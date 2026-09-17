using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Services.Economy
{
    /// <summary>
    /// Which POIs trade, and which bank (DESIGN.md §5.4, §5.4a).
    ///
    /// <para>Declared as seed data rather than compared in <c>EconomyService</c>, for the
    /// reason <c>NoServiceCode_BranchesOnASpecificSkill</c> exists: a
    /// <c>skill == SkillType.X</c> in a service is the line that gets copy-pasted eleven more
    /// times. The guard caught exactly that here, and the fix is better than an exemption —
    /// one place names the skills, and the service asks it.</para>
    ///
    /// <para>A second kind of venue is a row here, not a branch there.</para>
    /// </summary>
    public static class EconomySeedData
    {
        /// <summary>A kind of place the economy does business at.</summary>
        /// <param name="Skill">The POI skill that marks it.</param>
        /// <param name="RefusalSuffix">
        /// Completes "<c>{poi.Name} …</c>" when the player picked the wrong sort of place.
        /// Carried with the venue so the message cannot drift from the rule.
        /// </param>
        public record Venue(SkillType Skill, string RefusalSuffix);

        /// <summary>
        /// Where materials are sold.
        ///
        /// <para>Trading POIs: markets, supermarkets, malls, corner shops. Deliberately the
        /// densest category in the game — §4.1a asks that geography change how well you play,
        /// never whether you can, and almost everyone has a shop within a walk.</para>
        /// </summary>
        public static readonly Venue Shop =
            new(SkillType.Trading, "does not buy anything — find a shop or a market.");

        /// <summary>Where coin is deposited and withdrawn: banks, ATMs, post offices.</summary>
        public static readonly Venue Bank =
            new(SkillType.Banking, "is not a bank.");
    }
}
