using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Admin.Requests
{
    /// <summary>Create or update an item (Stage 18 task 4).</summary>
    public class SaveItemRequest
    {
        /// <summary>Null when creating.</summary>
        public int? Id { get; set; }

        public required string Key { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }

        public ItemKind Kind { get; set; }
        public ItemSlot Slot { get; set; }
        public ItemModifier Modifier { get; set; }
        public double ModifierValue { get; set; }
        public ItemModifier? SecondaryModifier { get; set; }
        public double SecondaryModifierValue { get; set; }
        public int Tier { get; set; } = 1;
    }
}
