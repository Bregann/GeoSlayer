using GeoSlayer.Domain.Enums;
using NetTopologySuite.Geometries;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeoSlayer.Domain.Database.Models
{
    /// <summary>
    /// A real-world Point of Interest pulled from OpenStreetMap,
    /// mapped to a game skill that players can train by visiting it.
    /// </summary>
    public class PointOfInterest
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>OSM node/way ID for deduplication.</summary>
        [Required]
        public long OsmId { get; set; }

        /// <summary>OSM element type ("node" or "way").</summary>
        [Required]
        public string OsmType { get; set; } = "node";

        [Required]
        public string Name { get; set; } = "";

        /// <summary>The game skill this POI lets players train.</summary>
        [Required]
        public SkillType Skill { get; set; }

        /// <summary>Location as a WGS 84 point.</summary>
        [Required]
        [Column(TypeName = "geometry (point, 4326)")]
        public Point Location { get; set; } = null!;

        /// <summary>
        /// How much XP per visit (can vary by POI importance).
        /// Default is 10; larger/rarer POIs can give more.
        /// </summary>
        public int XpReward { get; set; } = 10;

        /// <summary>
        /// The raw OSM tags this POI was imported with (Stage 13 task 3).
        ///
        /// <para>Stored because Cryptic clue steps need a <i>distinguishing detail</i> —
        /// "beneath three spires" — and that detail only exists in tags the skill mapping
        /// throws away. <c>ClueCrypticTags</c> reads it; nothing else should, since a
        /// system branching on arbitrary OSM tags is a system that breaks when OSM
        /// changes.</para>
        ///
        /// <para>Empty for every POI imported before this column existed. Cryptic generation
        /// treats that as "no detail available" and falls back to a Category step, so a
        /// stale POI degrades rather than breaking.</para>
        /// </summary>
        /// <remarks>
        /// Column type and JSON conversion are configured in <c>AppDbContext</c> — Npgsql
        /// cannot map a dictionary to jsonb without a converter.
        /// </remarks>
        public Dictionary<string, string> Tags { get; set; } = [];
    }
}
