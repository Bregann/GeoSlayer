using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.DTOs.Retention.Responses
{
    public class DistrictStatusDto
    {
        public string? DistrictKey { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public double OutputBonus { get; set; }
        public int ClaimCount { get; set; }

        /// <summary>
        /// The District this player is closest to forming, when they have none.
        ///
        /// <para>Without this, "no District" is a dead end: §5.5 deliberately requires varied
        /// terrain, so a player whose Claims are all one terrain has no way to learn that from
        /// the status alone. Named so the app can say what is actually missing.</para>
        /// </summary>
        public string? NearestName { get; set; }

        /// <summary>Terrains the nearest District needs that the player has not claimed.</summary>
        public List<string> MissingTerrains { get; set; } = [];

        /// <summary>Further Claims the nearest District needs, beyond terrain variety.</summary>
        public int MissingClaims { get; set; }
    }
}
