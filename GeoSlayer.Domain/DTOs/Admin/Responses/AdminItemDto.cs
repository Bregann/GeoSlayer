using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Admin.Responses
{
    /// <summary>An item as the admin interface sees it (Stage 18 task 4).</summary>
    public class AdminItemDto
    {
        public int Id { get; set; }
        public required string Key { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }

        public ItemKind Kind { get; set; }
        public ItemSlot Slot { get; set; }
        public ItemModifier Modifier { get; set; }
        public double ModifierValue { get; set; }
        public ItemModifier? SecondaryModifier { get; set; }
        public double SecondaryModifierValue { get; set; }
        public int Tier { get; set; }

        /// <summary>Human-readable effect, the same text the app shows.</summary>
        public required string ModifierText { get; set; }

        /// <summary>True when an image has been uploaded for this item.</summary>
        public bool HasImage { get; set; }

        /// <summary>
        /// Whether anything in the game actually reads this item's modifier.
        ///
        /// <para>§4.3 is explicit that an equipped item changing no behaviour is a bug.
        /// Surfacing it here means an admin adding a modifier can see it is wired to
        /// something, rather than discovering months later that it never did anything.</para>
        /// </summary>
        public bool ModifierIsRead { get; set; }
    }
}
