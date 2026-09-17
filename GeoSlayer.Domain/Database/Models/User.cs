using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeoSlayer.Domain.Database.Models
{
    public class User
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public string Id { get; set; } = null!;

        [Required]
        public string FirstName { get; set; } = "";

        [Required]
        public string Username { get; set; } = "";

        [Required]
        public string Email { get; set; } = "";

        [Required]
        public string PasswordHash { get; set; } = "";

        /// <summary>
        /// Whether this user may reach the admin interface (Stage 18).
        ///
        /// <para>A flag rather than a roles table: there is exactly one privileged role and
        /// inventing a many-to-many for it would be structure without a second case. If a
        /// second role ever appears, that is the moment to normalise it — not before.</para>
        ///
        /// <para>Defaults to <see langword="false"/>, so every existing user and every new
        /// registration is an ordinary player. Admin is granted deliberately, out of band;
        /// there is no self-service path to it and there should not be.</para>
        /// </summary>
        public bool IsAdmin { get; set; }
    }
}
