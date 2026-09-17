using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeoSlayer.Domain.Database.Models
{
    /// <summary>
    /// A tuning number that can be changed without a deploy (Stage 18).
    ///
    /// <para>DESIGN.md promises repeatedly that balance is "seeded data, so it can be retuned
    /// without a deploy". That was half true: drops, XP ladders, materials, recipes and
    /// encounters all live in tables, but a handful of constants stayed in C# — coin
    /// multipliers, the Athletics XP rate, Banking's sell bonus, the deposit interest rate.
    /// Changing any of those meant a redeploy. This table closes that gap.</para>
    ///
    /// <para><b>Numbers, not rules.</b> The formula that turns a tier into a price stays in
    /// <c>CoinPricing</c> where it is pure and cheaply testable; only the multipliers it
    /// reads live here. A setting that encoded logic would move the rule somewhere it cannot
    /// be tested without a database, which is the opposite of the point.</para>
    ///
    /// <para>Values are stored as text and parsed by the accessor, the same shape
    /// <see cref="EnvironmentalSetting"/> already uses. <see cref="Default"/> is kept beside
    /// the current value so an admin can see what they have changed, and so a malformed row
    /// degrades to a sane number rather than taking a screen down.</para>
    /// </summary>
    public class GameSetting
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>Stable identifier, from <c>GameSettingKeys</c>.</summary>
        [Required, MaxLength(128)]
        public string Key { get; set; } = null!;

        /// <summary>The current value, as text.</summary>
        [Required, MaxLength(64)]
        public string Value { get; set; } = null!;

        /// <summary>
        /// What this was seeded as.
        ///
        /// <para>Kept so the admin interface can show what has drifted from the shipped
        /// balance, and so a value can be reset without looking it up in source.</para>
        /// </summary>
        [Required, MaxLength(64)]
        public string Default { get; set; } = null!;

        /// <summary>Grouping for the admin interface, e.g. "Economy", "Skills".</summary>
        [Required, MaxLength(64)]
        public string Category { get; set; } = null!;

        /// <summary>
        /// What this number does, written for whoever is tuning it.
        ///
        /// <para>Not a code comment — the reader is someone deciding whether to change it,
        /// and the useful thing to tell them is what it affects and what breaks if it goes
        /// too far.</para>
        /// </summary>
        [Required, MaxLength(512)]
        public string Description { get; set; } = null!;

        /// <summary>Lowest sane value, enforced on save.</summary>
        public double MinValue { get; set; }

        /// <summary>Highest sane value, enforced on save.</summary>
        public double MaxValue { get; set; }
    }
}
