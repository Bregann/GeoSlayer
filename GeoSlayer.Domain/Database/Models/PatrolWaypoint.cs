using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeoSlayer.Domain.Database.Models
{
    /// <summary>One point on a patrol route. Order matters — the circuit is walked in sequence.</summary>
    public class PatrolWaypoint
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int RouteId { get; set; }

        public int Sequence { get; set; }

        public double Latitude { get; set; }
        public double Longitude { get; set; }

        [ForeignKey(nameof(RouteId))]
        public virtual PatrolRoute Route { get; set; } = null!;
    }
}
