using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Services.Clues;

/// <summary>
/// Riddle text, generated from templates and OSM tags (DESIGN.md §5B.2).
///
/// <para><b>Never hand-authored per clue.</b> §5B.2 is explicit that "a hand-authored clue
/// system would die of content debt within a month" — templates referencing tag patterns
/// give effectively unlimited content from data already in the database.</para>
///
/// <para>Pure and static so the phrasing can be asserted without a database.</para>
/// </summary>
public static class ClueRiddleText
{
    /// <summary>
    /// How each skill's POIs are described, obliquely. Keyed on the skill because that is
    /// what <c>PoiImportService</c> already maps every tag to — so a new tag inherits its
    /// phrasing for free.
    /// </summary>
    private static readonly Dictionary<SkillType, string[]> SkillPhrases = new()
    {
        [SkillType.Prayer] = ["where the faithful gather", "beneath a steeple", "where candles are lit"],
        [SkillType.Knowledge] = ["among shelves of quiet", "where the learned sit", "where pages outnumber people"],
        [SkillType.Woodcutting] = ["under old branches", "where the canopy closes", "among standing timber"],
        [SkillType.Fishing] = ["at the water's edge", "where the current slows", "where lines are cast"],
        [SkillType.Healing] = ["where the ailing are mended", "under a white lamp"],
        [SkillType.Athletics] = ["where breath is short and legs are long", "where records are kept in chalk"],
        [SkillType.Tavern] = ["where the taps run", "where strangers become regulars"],
        [SkillType.Trading] = ["where coin changes hands", "among laden shelves"],
        [SkillType.Banking] = ["where coin sleeps", "behind a heavy door"],
        [SkillType.Combat] = ["where old walls still stand", "where something was once defended"],
        [SkillType.Mining] = ["where the ground was opened", "where stone was taken"],
        [SkillType.Farming] = ["among tended rows", "where the soil is turned"],
        [SkillType.Smithing] = ["where metal is persuaded", "near a cold forge"],
        [SkillType.Cooking] = ["where bread is made", "where something is always warm"],
        [SkillType.Foraging] = ["where the verge grows wild", "among the hedgerow"],
        [SkillType.Exploration] = ["somewhere worth the walk", "off the usual path"],
    };

    /// <summary>A phrase for a skill, chosen deterministically from the seed.</summary>
    public static string PhraseFor(SkillType skill, int seed)
    {
        if (!SkillPhrases.TryGetValue(skill, out var phrases) || phrases.Length == 0)
            return "somewhere worth finding";

        // Deterministic: regenerating a scroll must not reword a step the player is
        // already carrying.
        return phrases[Math.Abs(seed) % phrases.Length];
    }

    /// <summary>
    /// A <c>Direct</c> step: the POI is named, so the challenge is the walk rather than
    /// the puzzle. The simplest type, and the one Stage 13 ships first.
    /// </summary>
    public static string Direct(string poiName, SkillType skill, int seed) =>
        string.IsNullOrWhiteSpace(poiName)
            ? $"Find the place {PhraseFor(skill, seed)}."
            : $"Seek out {poiName}.";

    /// <summary>
    /// A <c>Category</c> step: any POI of a kind. Works anywhere, which is what makes it
    /// the safe fallback for a player with sparse local POIs.
    /// </summary>
    public static string Category(SkillType skill, int seed) =>
        $"Stand somewhere {PhraseFor(skill, seed)}.";

    /// <summary>
    /// A <c>Coordinate</c> step: a search area rather than an answer. The radius is stated
    /// so the player knows how much ground to cover.
    /// </summary>
    public static string Coordinate(double radiusMetres) =>
        $"Something waits within {Math.Round(radiusMetres)} metres of the marked place. Search it out.";

    /// <summary>
    /// A <c>Cryptic</c> step: a phrase plus a distinguishing tag, never the POI's name.
    ///
    /// <para>§5B.2's worked example is "where the faithful gather beneath three spires" —
    /// a phrase for the category, plus a detail that narrows it to one.</para>
    /// </summary>
    public static string Cryptic(SkillType skill, string? distinguishingDetail, int seed)
    {
        var phrase = PhraseFor(skill, seed);

        return string.IsNullOrWhiteSpace(distinguishingDetail)
            ? $"Find the place {phrase}."
            : $"Find the place {phrase}, {distinguishingDetail}.";
    }
}
