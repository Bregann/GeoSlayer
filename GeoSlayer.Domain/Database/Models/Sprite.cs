using GeoSlayer.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeoSlayer.Domain.Database.Models
{
    /// <summary>
    /// Artwork for anything the game shows an icon for (Stage 18 task 6).
    ///
    /// <para>Generalises <see cref="ItemImage"/>, which only covered items. Materials,
    /// encounters and Museum entries all want the same thing, and three more near-identical
    /// tables would mean three more upload endpoints, three more validators and three more
    /// places to fix the same bug.</para>
    ///
    /// <para>Keyed on <see cref="OwnerType"/> plus <see cref="OwnerId"/> rather than a
    /// foreign key per kind. That costs referential integrity — nothing stops a sprite
    /// outliving the material it belonged to — so deletion has to clean up explicitly. The
    /// trade is deliberate: a polymorphic key is the only shape that does not multiply by
    /// entity type, and an orphaned blob is a storage cost rather than a correctness
    /// problem.</para>
    ///
    /// <para>Stored as <c>bytea</c> for the same reason <see cref="ItemImage"/> is: the API
    /// is containerised with no declared volume, so a filesystem path would vanish on
    /// redeploy.</para>
    /// </summary>
    public class Sprite
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>What kind of thing this belongs to.</summary>
        public SpriteOwner OwnerType { get; set; }

        /// <summary>Which one. Not a foreign key — see the class remarks.</summary>
        public int OwnerId { get; set; }

        [Required]
        public byte[] Data { get; set; } = [];

        /// <summary>
        /// Served back verbatim as the response content type.
        ///
        /// <para>Validated on the way in against an allow-list. Echoing a client-supplied
        /// content type unchecked is how an "image" becomes an HTML page served from your
        /// own origin.</para>
        /// </summary>
        [Required, MaxLength(64)]
        public string ContentType { get; set; } = null!;

        [Required, MaxLength(256)]
        public string FileName { get; set; } = null!;

        public int SizeBytes { get; set; }

        public DateTime UploadedUtc { get; set; }

        /// <summary>Who uploaded it — part of the audit trail.</summary>
        [Required]
        public string UploadedByUserId { get; set; } = null!;
    }
}
