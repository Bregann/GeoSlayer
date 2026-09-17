using GeoSlayer.Domain.Database.Models;

namespace GeoSlayer.Domain.Services.Admin
{
    /// <summary>
    /// Rules for the progression definitions an admin can edit (Stage 18 task 8).
    ///
    /// <para>The sharpest one is the cost curve. <see cref="UpgradeDefinition.Costs"/> parses
    /// a comma-separated string with <c>int.Parse</c>, which <b>throws</b> on anything
    /// malformed — and that property is read whenever the upgrades screen loads. A single
    /// admin typo would therefore crash that screen for <i>every</i> player, not just the
    /// person who made it.</para>
    ///
    /// <para>That is the shape of most of what is here: these definitions are read on hot
    /// paths by every account, so a bad save is not a local mistake.</para>
    /// </summary>
    public static class ProgressionValidation
    {
        /// <summary>Why this upgrade cannot be saved, or null.</summary>
        public static string? RejectUpgrade(
            UpgradeDefinition candidate,
            IReadOnlyCollection<UpgradeDefinition> others)
        {
            if (string.IsNullOrWhiteSpace(candidate.Key))
            {
                return "An upgrade needs a key.";
            }

            if (string.IsNullOrWhiteSpace(candidate.Name))
            {
                return "An upgrade needs a name.";
            }

            // The key is what PlayerUpgrade rows and ProgressionDefaults.UpgradeKeys both
            // reference — a collision silently repoints everyone's purchased ranks.
            if (others.Any(u => string.Equals(u.Key, candidate.Key, StringComparison.OrdinalIgnoreCase)))
            {
                return $"Another upgrade already uses the key '{candidate.Key}'.";
            }

            if (candidate.MaxRank < 1)
            {
                return "Max rank must be at least 1, or the upgrade cannot be bought at all.";
            }

            if (candidate.MinAdventurerLevel < 1)
            {
                return "Minimum Adventurer level must be at least 1.";
            }

            var curveError = RejectCostCurve(candidate.CostCurve, candidate.MaxRank);

            if (curveError is not null)
            {
                return curveError;
            }

            return null;
        }

        /// <summary>
        /// Why a cost curve is unusable, or null.
        ///
        /// <para>Separated so it can be checked without constructing a definition, and
        /// because it is the rule most likely to be hit by an ordinary typo.</para>
        /// </summary>
        public static string? RejectCostCurve(string? curve, int maxRank)
        {
            if (string.IsNullOrWhiteSpace(curve))
            {
                return "A cost curve is required — it is the Bonus Point price of each rank.";
            }

            var parts = curve.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length == 0)
            {
                return "A cost curve needs at least one value.";
            }

            var costs = new List<int>();

            foreach (var part in parts)
            {
                // int.Parse throws, and Costs is read on every upgrades-screen load. A typo
                // here would take the screen down for everyone.
                if (!int.TryParse(part, out var cost))
                {
                    return $"'{part}' is not a whole number. A cost curve is comma-separated " +
                           "integers, like \"1,2,4,7,11\".";
                }

                if (cost < 1)
                {
                    return $"Rank costs must be at least 1 Bonus Point; '{part}' is not.";
                }

                costs.Add(cost);
            }

            if (costs.Count != maxRank)
            {
                return $"The curve has {costs.Count} value{(costs.Count == 1 ? "" : "s")} but " +
                       $"max rank is {maxRank}. A rank with no price cannot be bought, and a " +
                       "price with no rank is never charged.";
            }

            // §3.0a describes these as escalating. A curve that gets cheaper means the
            // cheapest way to a high rank is to buy the expensive ones first, which is a
            // puzzle rather than a choice.
            for (var i = 1; i < costs.Count; i++)
            {
                if (costs[i] < costs[i - 1])
                {
                    return $"Rank {i + 1} costs {costs[i]} but rank {i} costs {costs[i - 1]}. " +
                           "§3.0a makes upgrade costs escalate — a curve that dips rewards " +
                           "buying out of order.";
                }
            }

            return null;
        }

        /// <summary>Why this unlock rung cannot be saved, or null.</summary>
        public static string? RejectUnlock(
            UnlockDefinition candidate,
            IReadOnlyCollection<UnlockDefinition> others)
        {
            if (candidate.AdventurerLevel < 1)
            {
                return "An unlock sits at Adventurer level 1 or above.";
            }

            if (string.IsNullOrWhiteSpace(candidate.Payload))
            {
                return "An unlock needs a payload — the skill or system it grants.";
            }

            if (string.IsNullOrWhiteSpace(candidate.DisplayName))
            {
                return "An unlock needs a display name; it is what the celebration shows.";
            }

            // Matches the unique index on (AdventurerLevel, Payload). Catching it here means
            // a readable message rather than a database constraint violation.
            var duplicate = others.Any(u =>
                u.AdventurerLevel == candidate.AdventurerLevel
                && string.Equals(u.Payload, candidate.Payload, StringComparison.OrdinalIgnoreCase));

            if (duplicate)
            {
                return $"'{candidate.Payload}' is already unlocked at level " +
                       $"{candidate.AdventurerLevel}.";
            }

            // The same payload at two levels means the lower one grants it and the higher
            // one silently does nothing — ApplyUnlocks skips what is already owned.
            var elsewhere = others.FirstOrDefault(u =>
                string.Equals(u.Payload, candidate.Payload, StringComparison.OrdinalIgnoreCase)
                && u.AdventurerLevel != candidate.AdventurerLevel);

            if (elsewhere is not null)
            {
                return $"'{candidate.Payload}' already unlocks at level " +
                       $"{elsewhere.AdventurerLevel}. A second rung for the same payload never " +
                       "fires — whichever comes first grants it.";
            }

            return null;
        }

        /// <summary>
        /// Things worth saying about the resulting unlock ladder, none of them blocking.
        /// </summary>
        public static IReadOnlyList<string> WarnUnlocks(IReadOnlyCollection<UnlockDefinition> resulting)
        {
            var warnings = new List<string>();

            // §3.1's cold-start rule: a player whose first walk paints cells but drops no
            // materials is using a map-painting utility, not playing an RPG.
            var atLevelOne = resulting.Count(u => u.AdventurerLevel <= 1);

            if (atLevelOne < 2)
            {
                warnings.Add(
                    "Fewer than two things unlock at level 1. §3.1's cold-start note wants " +
                    "both Exploration and Foraging from minute one — a first walk that drops " +
                    "nothing is a map-painting utility, not an RPG.");
            }

            return warnings;
        }
    }
}
