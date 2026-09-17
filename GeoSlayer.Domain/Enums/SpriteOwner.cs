namespace GeoSlayer.Domain.Enums
{
    /// <summary>
    /// What a <see cref="Database.Models.Sprite"/> belongs to (Stage 18 task 6).
    ///
    /// <para>Ordinals are stored, so <b>never reorder these</b> — an insert in the middle
    /// would silently repoint every existing sprite at a different kind of thing.</para>
    /// </summary>
    public enum SpriteOwner
    {
        Item,
        Material,
        Encounter,
        MuseumEntry,
    }
}
