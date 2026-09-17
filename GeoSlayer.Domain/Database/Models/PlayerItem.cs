using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeoSlayer.Domain.Database.Models
{
    /// <summary>An item a player owns, and whether it is equipped or placed.</summary>
    public class PlayerItem
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int PlayerId { get; set; }

        public int ItemId { get; set; }

        public int Quantity { get; set; } = 1;

        /// <summary>
        /// True when this item's modifier is active. Gear is equipped; a building is placed
        /// on a Claim, which is what <see cref="ClaimId"/> records.
        /// </summary>
        public bool IsEquipped { get; set; }

        /// <summary>The Claim a building is placed on, if any.</summary>
        public int? ClaimId { get; set; }

        public DateTime AcquiredUtc { get; set; }

        [ForeignKey(nameof(PlayerId))]
        public virtual Player Player { get; set; } = null!;

        [ForeignKey(nameof(ItemId))]
        public virtual Item Item { get; set; } = null!;

        [ForeignKey(nameof(ClaimId))]
        public virtual Claim? Claim { get; set; }
    }
}
