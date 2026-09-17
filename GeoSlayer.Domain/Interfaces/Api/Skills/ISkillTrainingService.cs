using GeoSlayer.Domain.DTOs.Skills.Responses;
using GeoSlayer.Domain.Services.Fog;

namespace GeoSlayer.Domain.Interfaces.Api.Skills
{
    /// <summary>
    /// Generic skill training (Stage 04 task 1).
    ///
    /// <para><b>No method here knows about a specific skill.</b> Training is driven entirely
    /// by seeded <c>SkillTerrainMapping</c> rows and the POI's own mapped skill, so adding a
    /// skill is seed data. A <c>if (skill == …)</c> branch appearing in an implementation of
    /// this interface means the machinery has been built too narrowly.</para>
    /// </summary>
    public interface ISkillTrainingService
    {
        /// <summary>
        /// Train every skill whose terrain mapping matches the cells just revealed.
        ///
        /// A skill with no row for a terrain still trains from its <c>Open</c> row — terrain
        /// multiplies, it never gates (§5.2).
        /// </summary>
        /// <param name="walkedMetres">
        /// Distance actually walked in this batch, for the skills in
        /// <c>SkillSeedData.DistanceSynergySkills</c>. Cells reward new ground; this rewards
        /// the legs, which is the only way a skill about endurance can pay for a route the
        /// player has walked before.
        /// </param>
        Task<List<SkillTrainingDto>> TrainFromCells(
            int playerId, IReadOnlyList<GridCell> cells, double walkedMetres, CancellationToken ct);

        /// <summary>
        /// Visit a POI: validates range server-side, applies decay, grants XP and materials.
        /// </summary>
        Task<PoiVisitResultDto> VisitPoi(int playerId, int poiId, CancellationToken ct);
    }
}
