using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeoSlayer.Domain.Database.Models
{
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

        /// <summary>
        /// Museum-only currency from donated duplicates (§5A.1) — the dignified sink for
        /// material overflow.
        /// </summary>
        public long Curation { get; set; }

        /// <summary>
        /// Coin in hand (DESIGN.md §9.5, resolved).
        ///
        /// <para>A column rather than a <c>Material</c> row: coin has no tier, no terrain, no
        /// drop table and no skill, so modelling it as a material would force every material
        /// query to special-case it — which is precisely how Dust became awkward.</para>
        ///
        /// <para><c>long</c> because nothing caps it. Earned by selling materials at Trading
        /// POIs (§5.4), which is what finally gives Trading an economy rather than just a
        /// gathering ladder.</para>
        /// </summary>
        public long Coin { get; set; }

        /// <summary>
        /// Coin deposited at a bank, earning interest (§5.4a).
        ///
        /// <para>Separate from <see cref="Coin"/> because deposited coin is <b>not spendable
        /// until withdrawn</b> — that illiquidity is the whole trade, and merging the two
        /// would make interest a free bonus on money you were using anyway.</para>
        /// </summary>
        public long CoinDeposited { get; set; }

        /// <summary>
        /// When interest was last settled onto <see cref="CoinDeposited"/>.
        ///
        /// <para>Interest accrues from this moment, bounded by the offline cap — the same
        /// rule worker accrual follows (§5.2). Unbounded compounding on an uncapped balance
        /// would eventually dwarf walking, which is the opposite of what a game about going
        /// outside should reward.</para>
        /// </summary>
        public DateTime? InterestSettledUtc { get; set; }

        /// <summary>
        /// Craft slots rented with coin, waiting to be used (§5D.4).
        ///
        /// <para>Consumed by the next craft queued beyond the permanent limit, rather than
        /// expiring on a timer. A rental that ticked away while you were asleep would punish
        /// a player for the offline half of the game, and §7.4 forbids that. It also means a
        /// rental cannot be stockpiled into a permanent upgrade — each one buys exactly one
        /// craft.</para>
        ///
        /// <para>Deliberately worse value than the Craft Slot upgrade, which is permanent
        /// and bought with Bonus Points. If renting were competitive, §4.3's rule that two
        /// routes to the same bonus makes one redundant would bite.</para>
        /// </summary>
        public int RentedCraftSlots { get; set; }

        public virtual ICollection<PlayerSkill> Skills { get; set; } = new List<PlayerSkill>();

        public virtual ICollection<PlayerUpgrade> Upgrades { get; set; } = new List<PlayerUpgrade>();

        [ForeignKey(nameof(UserId))]
        public virtual User User { get; set; } = null!;
    }
}
