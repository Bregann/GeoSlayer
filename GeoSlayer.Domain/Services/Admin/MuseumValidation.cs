using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Services.Admin
{
    /// <summary>
    /// Rules for Museum entry definitions (Stage 18 task 8).
    ///
    /// <para>The Museum is the one system in the game whose whole premise is that it is
    /// <b>optional</b>. §5A wants it "pursued for its own sake, not because it is mandatory",
    /// and Stage 12 task 3 says to keep the completion bonuses small for exactly that reason:
    /// a wing is dozens of finds, and if filling one paid well it would stop being a
    /// choice.</para>
    ///
    /// <para>That makes it unusually easy to break from an admin screen. Nothing crashes if a
    /// wing's bonus is doubled — the game simply becomes one where the Museum is compulsory,
    /// which is a design regression nobody would notice in a bug report.</para>
    /// </summary>
    public static class MuseumValidation
    {
        /// <summary>
        /// Wings that can never be completed, and therefore must never promise a bonus.
        ///
        /// <para>Cartography's entries are created on discovery — regions appear as players
        /// find them — so the wing has no fixed size. A completion bonus for it would be a
        /// promise that cannot be kept.</para>
        /// </summary>
        public static IReadOnlySet<MuseumWing> UncompletableWings { get; } =
            new HashSet<MuseumWing> { MuseumWing.Cartography };

        /// <summary>
        /// The largest a wing bonus may be, as a fraction.
        ///
        /// <para>Stage 12 task 3's "keep small" made concrete. 10% is a nudge; anything much
        /// larger and a completionist is meaningfully ahead of someone who ignored the
        /// Museum, which is the thing §5A does not want.</para>
        /// </summary>
        public const double MaxSetBonus = 0.10;

        /// <summary>Why this entry cannot be saved, or null.</summary>
        public static string? Reject(
            MuseumEntryDefinition candidate,
            IReadOnlyCollection<MuseumEntryDefinition> others)
        {
            if (string.IsNullOrWhiteSpace(candidate.Key))
            {
                return "A Museum entry needs a key.";
            }

            if (string.IsNullOrWhiteSpace(candidate.Name))
            {
                return "A Museum entry needs a name.";
            }

            // The key is what PlayerMuseumEntry rows reference, and what RecordFind matches
            // on — a collision would credit a find to the wrong plinth.
            if (others.Any(e => string.Equals(e.Key, candidate.Key, StringComparison.OrdinalIgnoreCase)))
            {
                return $"Another entry already uses the key '{candidate.Key}'.";
            }

            if (string.IsNullOrWhiteSpace(candidate.UnlockCondition))
            {
                return "An entry needs an unlock condition — it is what the empty plinth says, "
                     + "and §5A.1 stakes the whole system on an empty plinth reading as a pull "
                     + "rather than as noise.";
            }

            return null;
        }

        /// <summary>
        /// Why a wing's completion bonus is unacceptable, or null.
        /// </summary>
        public static string? RejectSetBonus(MuseumWing wing, double value)
        {
            if (UncompletableWings.Contains(wing))
            {
                return $"{wing} has no fixed size — its entries are created as players "
                     + "discover them — so it can never be completed. A bonus for finishing "
                     + "it would be a promise that cannot be kept.";
            }

            if (value <= 0)
            {
                return "A completion bonus of zero is a bonus that changes nothing (§4.3).";
            }

            if (value > MaxSetBonus)
            {
                return $"{value:P0} is more than a nudge. Stage 12 task 3 caps wing bonuses at "
                     + $"{MaxSetBonus:P0} so the Museum stays something pursued for its own "
                     + "sake rather than because it is mandatory (§5A).";
            }

            return null;
        }

        /// <summary>
        /// Things worth saying about the resulting set of entries.
        /// </summary>
        public static IReadOnlyList<string> Warn(IReadOnlyCollection<MuseumEntryDefinition> resulting)
        {
            var warnings = new List<string>();

            // A wing with a handful of entries is filled by accident, which makes its
            // completion bonus a freebie rather than a reward for a long collection.
            foreach (var group in resulting.GroupBy(e => e.Wing))
            {
                if (UncompletableWings.Contains(group.Key))
                {
                    continue;
                }

                if (group.Count() < 5)
                {
                    warnings.Add(
                        $"The {group.Key} wing has only {group.Count()} entr"
                        + $"{(group.Count() == 1 ? "y" : "ies")}. A wing that fills by accident "
                        + "makes its completion bonus a freebie rather than a reward.");
                }
            }

            // An entirely empty wing renders as a section header with nothing under it.
            var missing = Enum.GetValues<MuseumWing>()
                .Where(w => !resulting.Any(e => e.Wing == w))
                .ToList();

            if (missing.Count > 0)
            {
                warnings.Add(
                    $"No entries at all in: {string.Join(", ", missing)}. Those wings will "
                    + "render as empty headings.");
            }

            return warnings;
        }
    }
}
