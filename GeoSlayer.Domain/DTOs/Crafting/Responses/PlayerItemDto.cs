using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Crafting.Responses
{
    public class PlayerItemDto
    {
        public int Id { get; set; }
        public int ItemId { get; set; }
        public required string Key { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        public ItemKind Kind { get; set; }
        public ItemSlot Slot { get; set; }
        public ItemModifier Modifier { get; set; }
        public double ModifierValue { get; set; }

        /// <summary>
        /// The effect as text, e.g. "+20% skill XP". Includes a secondary modifier when the
        /// item has one — a tool that gates a tier *and* gathers faster must say both, or the
        /// screen undersells it.
        /// </summary>
        public required string ModifierText { get; set; }

        public ItemModifier? SecondaryModifier { get; set; }
        public double SecondaryModifierValue { get; set; }

        public int Tier { get; set; }

        /// <summary>
        /// Whether an admin has uploaded an image for this item (Stage 18 task 3).
        ///
        /// <para>Sent so the app knows whether to render an image at all. Without it the
        /// only way to find out is to request one and handle the 404, which means a failed
        /// request per imageless item on every render.</para>
        /// </summary>
        public bool HasImage { get; set; }
        public int Quantity { get; set; }
        public bool IsEquipped { get; set; }
        public int? ClaimId { get; set; }
        public string? ClaimName { get; set; }
    }
}
