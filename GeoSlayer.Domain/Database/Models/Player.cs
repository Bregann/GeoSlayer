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

    /// <summary>Cumulative lifetime Adventurer XP. <c>long</c> because the curve is
    /// uncapped — an <c>int</c> would cap progression at roughly level 126.
    /// Renamed from <c>Xp</c> in Stage 02 (DESIGN.md §3.1): the pool it always held is
    /// the Adventurer pool, so this is a rename plus a rescale, not new state.</summary>
    public long AdventurerXp { get; set; }

    /// <summary>Cached level derived from <see cref="AdventurerXp"/>.</summary>
    public int AdventurerLevel { get; set; } = 1;

    /// <summary>Bonus Points granted by levelling — one per Adventurer level (§3.0a).</summary>
    public int BonusPointsEarned { get; set; }

    /// <summary>Bonus Points spent on upgrades. Available = earned − spent.</summary>
    public int BonusPointsSpent { get; set; }

    /// <summary>How many times this player has respecced — the cost escalates (§3.0a).</summary>
    public int RespecCount { get; set; }

    public virtual ICollection<PlayerSkill> Skills { get; set; } = new List<PlayerSkill>();

    public virtual ICollection<PlayerUpgrade> Upgrades { get; set; } = new List<PlayerUpgrade>();

    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;
}
