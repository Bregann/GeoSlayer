using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeoSlayer.Domain.Database.Models
{
    /// <summary>
    /// A record of something an admin changed (Stage 18 task 9).
    ///
    /// <para>Stage 18's acceptance criteria require that every balance-affecting admin action
    /// is recorded with who, what and when. The reason is practical rather than ceremonial:
    /// an admin action that alters a player's balance and leaves no record is
    /// <b>indistinguishable from a bug</b>. Without this table, "my coin went down" has no
    /// answer.</para>
    ///
    /// <para>Append-only by convention. Nothing in the codebase updates or deletes a row
    /// here, and nothing should — an audit trail that can be edited is a log, not a
    /// trail.</para>
    /// </summary>
    public class AdminAuditEntry
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>The admin who did it.</summary>
        [Required]
        public string AdminUserId { get; set; } = null!;

        /// <summary>Their username at the time, denormalised deliberately.</summary>
        /// <remarks>
        /// Copied rather than joined so the trail still reads correctly if the user is
        /// renamed or removed. A foreign key would be tidier and would lose the history the
        /// moment it mattered most.
        /// </remarks>
        [Required, MaxLength(128)]
        public string AdminUsername { get; set; } = null!;

        /// <summary>What kind of thing was touched, e.g. "Item", "Material", "Player".</summary>
        [Required, MaxLength(64)]
        public string EntityType { get; set; } = null!;

        /// <summary>Which one, as text so any key shape fits.</summary>
        [MaxLength(64)]
        public string? EntityId { get; set; }

        /// <summary>What happened, e.g. "Created", "Updated", "Deleted", "ImageUploaded".</summary>
        [Required, MaxLength(64)]
        public string Action { get; set; } = null!;

        /// <summary>
        /// What changed, in enough detail to answer a question later.
        ///
        /// <para>Free text rather than a structured diff: the audience is a human reading it
        /// months afterwards, and a JSON blob of before/after would be both larger and harder
        /// to scan. Callers are expected to write something a person can act on.</para>
        /// </summary>
        [MaxLength(2048)]
        public string? Detail { get; set; }

        public DateTime OccurredUtc { get; set; }
    }
}
