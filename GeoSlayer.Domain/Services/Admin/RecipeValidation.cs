using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Services.Economy;

namespace GeoSlayer.Domain.Services.Admin
{
    /// <summary>
    /// The rules a recipe must satisfy (Stage 18 task 7).
    ///
    /// <para>Split into two kinds deliberately. <see cref="Reject"/> covers what is
    /// <b>structurally broken</b> — a recipe with no output, or one that consumes what it
    /// produces — and refuses the save. <see cref="Warn"/> covers what is merely
    /// <b>probably wrong</b>, and says so without blocking.</para>
    ///
    /// <para>That line matters: an admin tuning balance needs to be able to make a recipe
    /// temporarily unattractive while they work. Refusing every questionable number would
    /// make the interface fight them. Refusing a recipe that cannot function is different —
    /// that is a crash waiting to happen somewhere else in the game.</para>
    /// </summary>
    public static class RecipeValidation
    {
        /// <summary>Why this recipe cannot be saved, or null when it is structurally sound.</summary>
        public static string? Reject(
            Recipe candidate,
            IReadOnlyCollection<RecipeInput> inputs,
            IReadOnlyCollection<Recipe> others)
        {
            if (string.IsNullOrWhiteSpace(candidate.Key))
            {
                return "A recipe needs a key.";
            }

            if (string.IsNullOrWhiteSpace(candidate.Name))
            {
                return "A recipe needs a name.";
            }

            if (others.Any(r => string.Equals(r.Key, candidate.Key, StringComparison.OrdinalIgnoreCase)))
            {
                return $"Another recipe already uses the key '{candidate.Key}'.";
            }

            // A recipe producing nothing is a timer that consumes materials and gives back
            // nothing — indistinguishable from a bug when a player hits it.
            if (candidate.OutputMaterialId is null && candidate.OutputItemId is null)
            {
                return "A recipe must produce either a material or an item.";
            }

            if (candidate.OutputMaterialId is not null && candidate.OutputItemId is not null)
            {
                return "A recipe produces one thing — a material or an item, not both.";
            }

            if (candidate.OutputQuantity < 1)
            {
                return "Output quantity must be at least 1.";
            }

            if (candidate.DurationSeconds <= 0)
            {
                return "Duration must be above zero — §4.2 makes crafting time-gated, and an " +
                       "instant craft is a tap, not a decision.";
            }

            if (candidate.LevelRequired < 1)
            {
                return "Level required must be at least 1.";
            }

            if (candidate.XpReward < 0)
            {
                return "XP reward cannot be negative.";
            }

            if (inputs.Count == 0)
            {
                return "A recipe needs at least one input, or it produces something from nothing.";
            }

            if (inputs.Any(i => i.Quantity < 1))
            {
                return "Every input needs a quantity of at least 1.";
            }

            // A duplicate input row makes the cost ambiguous — one of them silently wins.
            var duplicated = inputs
                .GroupBy(i => i.MaterialId)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicated is not null)
            {
                return "The same material is listed twice as an input. Combine them into one row.";
            }

            // Consuming your own output is an infinite loop with a timer attached.
            if (candidate.OutputMaterialId is int output && inputs.Any(i => i.MaterialId == output))
            {
                return "A recipe cannot consume the material it produces.";
            }

            return null;
        }

        /// <summary>
        /// Things worth telling an admin about, none of which block the save.
        /// </summary>
        /// <param name="inputCost">Total coin value of the inputs.</param>
        /// <param name="outputValue">Coin value of what comes out.</param>
        public static IReadOnlyList<string> Warn(
            Recipe candidate, long inputCost, long outputValue)
        {
            var warnings = new List<string>();

            // §5D.1's produced-goods rule, checked rather than assumed. A recipe that sells
            // for less than its parts is a way to lose money, and nobody will craft it — but
            // an admin mid-tune should be allowed to save it and come back.
            if (outputValue > 0 && inputCost > 0 && outputValue <= inputCost)
            {
                warnings.Add(
                    $"This loses money: inputs are worth {inputCost}c and the output {outputValue}c. " +
                    "§5D.1 expects produced goods to beat their parts, or crafting is a loss.");
            }

            // An hour of crafting for a handful of XP reads as broken rather than balanced.
            if (candidate.DurationSeconds >= 3600 && candidate.XpReward < 50)
            {
                warnings.Add(
                    $"{Math.Round(candidate.DurationSeconds / 60)} minutes for " +
                    $"{candidate.XpReward} XP is a long wait for very little.");
            }

            // §4.2's time gate is what makes crafting a decision you make on the way out.
            // A few seconds is a tap.
            if (candidate.DurationSeconds < 30)
            {
                warnings.Add(
                    "Under 30 seconds is close to instant — §4.2 makes crafting time-gated so " +
                    "you queue it and walk away.");
            }

            return warnings;
        }

        /// <summary>
        /// What a set of inputs is worth in coin.
        ///
        /// <para>Uses the same derived pricing the shop does (§5D.1), so the warning above
        /// cannot disagree with what a player would actually be paid.</para>
        /// </summary>
        public static long CostOf(IEnumerable<(Material Material, int Quantity)> inputs) =>
            inputs.Sum(i => CoinPricing.UnitPrice(i.Material.Tier, i.Material.Category) * i.Quantity);
    }
}
