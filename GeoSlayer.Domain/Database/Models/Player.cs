using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeoSlayer.Domain.Database.Models;

public class Player
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = null!;

    /// <summary>Coarse grid cell for the POI preloader (~5 km).</summary>
    public double? LastCellLat { get; set; }
    public double? LastCellLng { get; set; }

    /// <summary>Last position reported by the client (anti-cheat).</summary>
    public double LastLatitude { get; set; }
    public double LastLongitude { get; set; }

    /// <summary>UTC timestamp of the last sync (anti-cheat cooldown & speed validation).</summary>
    public DateTime? LastSyncAtUtc { get; set; }

    public int Xp { get; set; }

    public int Level { get; set; } = 1;

    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;
}
