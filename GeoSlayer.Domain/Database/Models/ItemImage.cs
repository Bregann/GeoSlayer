using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeoSlayer.Domain.Database.Models
{
    /// <summary>
    /// An uploaded image for an <see cref="Item"/> (Stage 18 task 3).
    ///
    /// <para><b>Stored in the database, not on disk.</b> The API is containerised
    /// (<c>GeoSlayer.Core/Dockerfile</c>) with no declared volume, so a filesystem path
    /// would either vanish on redeploy or require infrastructure that does not exist yet.
    /// A column keeps deployment to one artefact and makes backup exactly what it already
    /// is — the trade is that images are read through the app rather than served by a CDN,
    /// which for a few dozen small icons is not a trade worth avoiding.</para>
    ///
    /// <para>Its own table rather than a column on <see cref="Item"/>: a <c>bytea</c> on the
    /// item row would be loaded by every query that touches items, including the crafting
    /// screen that wants names and nothing else. Separating it means the bytes are fetched
    /// only when something actually asks for the image.</para>
    /// </summary>
    public class ItemImage
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>The item this belongs to. One image per item — replacing overwrites.</summary>
        public int ItemId { get; set; }

        [ForeignKey(nameof(ItemId))]
        public virtual Item Item { get; set; } = null!;

        /// <summary>The image itself.</summary>
        [Required]
        public byte[] Data { get; set; } = [];

        /// <summary>
        /// Served back verbatim as the response content type.
        ///
        /// <para>Stored rather than inferred from the bytes on every read, and validated on
        /// the way in against an allow-list — echoing back a client-supplied content type
        /// unchecked is how an "image" upload becomes an HTML page served from your own
        /// origin.</para>
        /// </summary>
        [Required, MaxLength(64)]
        public string ContentType { get; set; } = null!;

        /// <summary>The original filename, for the admin interface to display.</summary>
        [Required, MaxLength(256)]
        public string FileName { get; set; } = null!;

        public int SizeBytes { get; set; }

        public DateTime UploadedUtc { get; set; }

        /// <summary>Who uploaded it — part of the audit trail (Stage 18 task 9).</summary>
        [Required]
        public string UploadedByUserId { get; set; } = null!;
    }
}
