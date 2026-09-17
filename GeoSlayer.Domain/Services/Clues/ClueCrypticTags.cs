namespace GeoSlayer.Domain.Services.Clues
{
    /// <summary>
    /// Turns raw OSM tags into the distinguishing half of a Cryptic riddle
    /// (DESIGN.md §5B.2, Stage 13 task 3).
    ///
    /// <para>§5B.2's worked example is "where the faithful gather <b>beneath three
    /// spires</b>" — a category phrase from <see cref="ClueRiddleText"/>, plus a detail that
    /// narrows it to exactly one place. This class owns the second half.</para>
    ///
    /// <para><b>The hard rule is uniqueness.</b> Task 3 requires that a generated riddle
    /// resolve to exactly one POI in the search radius. An ambiguous riddle is one the player
    /// cannot solve, which is precisely what the one-skip rule exists to rescue them from —
    /// so producing one is worse than producing no Cryptic step at all. Every method here is
    /// built to fail closed: no usable detail means <see langword="null"/>, and the caller
    /// falls back to a Category step.</para>
    ///
    /// <para>Pure and static, so the phrasing and the uniqueness rule can both be asserted
    /// without a database.</para>
    /// </summary>
    public static class ClueCrypticTags
    {
        /// <summary>
        /// How a tag becomes a phrase. Ordered by how recognisable the detail is from the
        /// street, because the player has to confirm it standing in front of the place:
        /// a storey count or a spire is visible, an operator is usually a sign, and a
        /// construction date is often carved but sometimes not recorded anywhere visible.
        ///
        /// <para>A tag is only listed here if a phrase can be written for it that a player
        /// could actually check. A tag we cannot phrase is a tag we do not use.</para>
        /// </summary>
        private static readonly (string Key, Func<string, string?> Phrase)[] Phrasings =
        [
            ("building:levels", StoreyPhrase),
            ("tower:type", value => Describe(value, "with a {0} tower")),
            ("building:material", value => Describe(value, "built of {0}")),
            ("material", value => Describe(value, "made of {0}")),
            ("roof:colour", value => Describe(value, "under a {0} roof")),
            ("roof:material", value => Describe(value, "roofed in {0}")),
            ("colour", value => Describe(value, "painted {0}")),
            ("castle_type", value => Describe(value, "of the {0} kind")),
            ("denomination", value => Describe(value, "kept by the {0}")),
            ("artwork_type", value => Describe(value, "marked by a {0}")),
            ("artist_name", value => Describe(value, "signed by {0}")),
            ("sport", value => Describe(value, "given over to {0}")),
            ("cuisine", value => Describe(value, "where they cook {0}")),
            ("brewery", value => Describe(value, "where they pour {0}")),
            ("architect", value => Describe(value, "raised by {0}")),
            ("start_date", YearPhrase),
            ("heritage", _ => "that someone thought worth protecting"),
            ("listed_status", _ => "that someone thought worth protecting"),
            ("operator", value => Describe(value, "kept by {0}")),
            ("brand", value => Describe(value, "flying the {0} sign")),
        ];

        /// <summary>
        /// A phrase describing what makes <paramref name="candidate"/> unlike every POI in
        /// <paramref name="others"/>, or <see langword="null"/> when nothing does.
        ///
        /// <para>This is the uniqueness check, and it is the reason the method takes the
        /// whole candidate set rather than one POI's tags. A detail is only usable when no
        /// other POI in the radius shares it — "beneath three spires" is a riddle when one
        /// church has three spires and a coin flip when two do.</para>
        ///
        /// <para>Comparison is against POIs of <b>the same skill</b> only, chosen by the
        /// caller: the category phrase already narrows the field to one skill, so a library
        /// sharing a storey count with a church does not make the church ambiguous.</para>
        /// </summary>
        /// <param name="candidate">Tags of the POI the step points at.</param>
        /// <param name="others">Tags of every other same-skill POI within the search radius.</param>
        /// <param name="seed">Chooses between equally-good details, deterministically.</param>
        public static string? DistinguishingDetail(
            IReadOnlyDictionary<string, string> candidate,
            IReadOnlyCollection<IReadOnlyDictionary<string, string>> others,
            int seed)
        {
            if (candidate.Count == 0)
            {
                return null;
            }

            var usable = new List<string>();

            foreach (var (key, phrase) in Phrasings)
            {
                if (!candidate.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                // The uniqueness rule. Any other POI carrying the same key/value makes this
                // detail ambiguous, so it is discarded rather than softened.
                var shared = others.Any(other =>
                    other.TryGetValue(key, out var theirs)
                    && string.Equals(theirs, value, StringComparison.OrdinalIgnoreCase));

                if (shared)
                {
                    continue;
                }

                var text = phrase(value);

                if (!string.IsNullOrWhiteSpace(text))
                {
                    usable.Add(text);
                }
            }

            if (usable.Count == 0)
            {
                return null;
            }

            // Ordered by Phrasings, so the most recognisable detail wins when several are
            // unique. The seed only breaks ties among what is already usable — it never
            // reaches past a better detail for a worse one.
            //
            // Not Math.Abs: the seed comes from HashCode.Combine, which can return
            // int.MinValue, and Math.Abs throws on it. That would be a crash on scroll
            // generation roughly once in four billion.
            return usable[(int)((uint)seed % (uint)usable.Count)];
        }

        /// <summary>
        /// "beneath three storeys" — §5B.2's own example shape.
        ///
        /// <para>Spelled out rather than numeric because "beneath 3 storeys" reads as a
        /// database row and the riddle is meant to read as a riddle.</para>
        /// </summary>
        private static string? StoreyPhrase(string value)
        {
            if (!int.TryParse(value, out var levels) || levels <= 0 || levels > 12)
            {
                return null;
            }

            string[] words =
            [
                "", "a single storey", "two storeys", "three storeys", "four storeys",
                "five storeys", "six storeys", "seven storeys", "eight storeys",
                "nine storeys", "ten storeys", "eleven storeys", "twelve storeys",
            ];

            return levels == 1
                ? "that rises no higher than its neighbours"
                : $"standing {words[levels]} tall";
        }

        /// <summary>"that has stood since 1874" — the year only, from any OSM date format.</summary>
        private static string? YearPhrase(string value)
        {
            var digits = new string(value.TakeWhile(char.IsDigit).ToArray());

            if (digits.Length != 4 || !int.TryParse(digits, out var year))
            {
                return null;
            }

            return year is < 1000 or > 2100 ? null : $"that has stood since {year}";
        }

        /// <summary>
        /// Fills a template with a tag value, tidied into prose.
        ///
        /// <para>Returns null for multi-value tags (<c>brick;stone</c>) and for anything with
        /// odd characters. A riddle reading "built of brick;stone" is worse than no Cryptic
        /// step, which is the standing trade in this file.</para>
        /// </summary>
        private static string? Describe(string value, string template)
        {
            if (value.Contains(';') || value.Contains('=') || value.Length > 32)
            {
                return null;
            }

            var tidied = value.Replace('_', ' ').Trim();

            if (tidied.Length == 0 || !tidied.All(c => char.IsLetterOrDigit(c) || c is ' ' or '\'' or '-' or '&'))
            {
                return null;
            }

            return string.Format(template, tidied);
        }
    }
}
