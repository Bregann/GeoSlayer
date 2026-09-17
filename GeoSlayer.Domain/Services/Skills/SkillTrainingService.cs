using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.DTOs.Skills.Responses;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Exceptions;
using GeoSlayer.Domain.Services.Fog;
using Microsoft.EntityFrameworkCore;
using GeoSlayer.Domain.Interfaces.Api.Crafting;
using GeoSlayer.Domain.Interfaces.Api.Materials;
using GeoSlayer.Domain.Interfaces.Api.Museum;
using GeoSlayer.Domain.Interfaces.Api.Progression;
using GeoSlayer.Domain.Interfaces.Api.Skills;
using GeoSlayer.Domain.Services.Journey;

namespace GeoSlayer.Domain.Services.Skills
{
    /// <summary>
    /// Generic skill training (Stage 04 task 1).
    ///
    /// <para>Contains <b>no per-skill branches</b>. Which skills train from a cell comes from
    /// seeded <c>SkillTerrainMapping</c> rows; which skill a POI trains comes from the POI's
    /// own mapping. Adding Fishing should be seed data and nothing else.</para>
    /// </summary>
    public class SkillTrainingService(
        AppDbContext db,
        IProgressionService progression,
        IMaterialService materials,
        ICraftingService crafting,
        IMuseumService museum) : ISkillTrainingService
    {
        /// <summary>
        /// Must match <c>JourneyService.PoiInteractRadius</c>. Range is revalidated here
        /// against the player's last <i>server-verified</i> position — the client never
        /// supplies a position to this path (§7.2).
        /// </summary>
        private const double PoiInteractRadius = 50;

        /// <summary>
        /// Grace added to the range check. The stored position is from the last sync, so a
        /// player legitimately standing at a POI may be a little stale; without this, a real
        /// visit fails while the player is looking at the building.
        /// </summary>
        private const double RangeGraceMetres = 30;

        /// <summary>Minimum gap between visits to the same POI — the anti-spam floor (§7.2).</summary>
        private const double MinSecondsBetweenVisits = 30;

        public async Task<List<SkillTrainingDto>> TrainFromCells(
            int playerId, IReadOnlyList<GridCell> cells, CancellationToken ct)
        {
            if (cells.Count == 0) return [];

            // Only unlocked skills train. The row existing is the unlock (§3.1), so this is
            // also what stops a locked skill silently accruing XP.
            var unlocked = await db.PlayerSkills
                .Where(s => s.PlayerId == playerId)
                .Select(s => s.SkillType)
                .ToListAsync(ct);

            if (unlocked.Count == 0) return [];

            var mappings = await db.SkillTerrainMappings.ToListAsync(ct);
            if (mappings.Count == 0) return [];

            var unlockedSet = unlocked.ToHashSet();

            // Accumulate per skill across the whole batch, so one grant per skill hits the
            // progression service rather than one per cell per skill.
            var xpBySkill = new Dictionary<SkillType, double>();

            foreach (var cell in cells)
            {
                var terrain = await materials.GetOrClassifyTerrain(cell.GridLat, cell.GridLng, ct);

                foreach (var skill in unlockedSet)
                {
                    var xp = XpForCell(mappings, skill, terrain);
                    if (xp <= 0) continue;

                    xpBySkill[skill] = xpBySkill.GetValueOrDefault(skill) + xp;
                }
            }

            var results = new List<SkillTrainingDto>();

            foreach (var (skill, xp) in xpBySkill.OrderBy(kv => kv.Key))
            {
                var amount = (long)Math.Floor(xp);
                if (amount <= 0) continue;

                // All XP goes through IProgressionService — it owns the Adventurer cut, the
                // Scholar modifier, level-ups and the unlock ladder (Stage 02 criterion 2).
                var grant = await progression.GrantXp(playerId, skill, amount, XpSource.Walk, ct);

                results.Add(new SkillTrainingDto
                {
                    SkillType = skill,
                    Name = skill.ToString(),
                    SkillXpEarned = grant.SkillXpEarned,
                    AdventurerXpEarned = grant.AdventurerXpEarned,
                    Level = grant.SkillLevel,
                    LevelledUp = grant.SkillLevelledUp,
                });
            }

            return results;
        }

        /// <summary>
        /// XP one cell of <paramref name="terrain"/> grants <paramref name="skill"/>.
        ///
        /// <para>Terrain is a multiplier, never a gate (§5.2): the best matching flag wins,
        /// and a skill with no matching flag falls back to its <c>Open</c> base rate rather
        /// than to zero. A skill with no mappings at all trains nothing, which is how
        /// non-gathering skills opt out.</para>
        /// </summary>
        private static double XpForCell(
            IReadOnlyList<SkillTerrainMapping> mappings, SkillType skill, TerrainType terrain)
        {
            double best = 0;
            double baseRate = 0;

            foreach (var mapping in mappings)
            {
                if (mapping.SkillType != skill) continue;

                if (mapping.Terrain == TerrainType.Open)
                {
                    baseRate = mapping.XpPerCell;
                    continue;
                }

                // A cell can carry several terrain flags; the most generous match applies.
                if ((terrain & mapping.Terrain) == mapping.Terrain)
                    best = Math.Max(best, mapping.XpPerCell);
            }

            return best > 0 ? best : baseRate;
        }

        public async Task<PoiVisitResultDto> VisitPoi(int playerId, int poiId, CancellationToken ct)
        {
            var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct)
                ?? throw new NotFoundException($"Player {playerId} not found.");

            var poi = await db.PointsOfInterest.FirstOrDefaultAsync(p => p.Id == poiId, ct)
                ?? throw new NotFoundException($"POI {poiId} not found.");

            // ── Anti-cheat: range, against the server's own last known position ──────────
            // The client never supplies a position here. Trusting one would make this a
            // free teleport to any POI on the map (§7.2).
            // LastSyncAtUtc, not the coordinates: a row can carry a plausible-looking
            // lat/lng that the server never verified (a seeded default, a stale import), and
            // treating that as proof of location would be exactly the hole this check exists
            // to close. Only a completed sync counts.
            if (player.LastSyncAtUtc is null)
                throw new BadRequestException("No verified position yet — sync before visiting.");

            var distance = TraceValidator.HaversineMetres(
                player.LastLatitude, player.LastLongitude, poi.Location.Y, poi.Location.X);

            // Traveller's Boots and similar extend reach (§4.3) — an equipped item that
            // changes no behaviour is a bug.
            var gearRange = await crafting.GetModifierTotal(
                playerId, ItemModifier.PoiRangeMetres, ct);

            if (distance > PoiInteractRadius + RangeGraceMetres + gearRange)
                throw new BadRequestException(
                    $"Too far from {poi.Name} — {Math.Round(distance)}m away, need {PoiInteractRadius}m.");

            var now = DateTime.UtcNow;

            var visit = await db.PlayerPoiVisits
                .FirstOrDefaultAsync(v => v.PlayerId == playerId && v.PoiId == poiId, ct);

            // ── Anti-cheat: visit cooldown ───────────────────────────────────────────────
            if (visit is not null && (now - visit.LastVisitUtc).TotalSeconds < MinSecondsBetweenVisits)
                throw new BadRequestException("Visiting too quickly — wait a moment.");

            var isFirstVisit = visit is null;

            // Decay uses the count *before* this visit, so the first visit pays full (§3.4).
            var priorCount = visit is null
                ? 0
                : VisitDecay.DecayedCount(visit.VisitCount, now - visit.LastVisitUtc);

            var multiplier = VisitDecay.Multiplier(priorCount);
            var xp = VisitDecay.XpForVisit(poi.XpReward, priorCount);

            if (visit is null)
            {
                visit = new PlayerPoiVisit
                {
                    PlayerId = playerId,
                    PoiId = poiId,
                    VisitCount = 1,
                    TotalVisits = 1,
                    FirstVisitUtc = now,
                    LastVisitUtc = now,
                };
                db.PlayerPoiVisits.Add(visit);
            }
            else
            {
                // Store the decayed count plus this visit, so time away genuinely restores
                // value rather than the raw total climbing forever.
                visit.VisitCount = priorCount + 1;
                visit.TotalVisits += 1;
                visit.LastVisitUtc = now;
            }

            await db.SaveChangesAsync(ct);

            var result = new PoiVisitResultDto
            {
                SessionToken = Guid.NewGuid(),
                PoiId = poi.Id,
                PoiName = poi.Name,
                Skill = poi.Skill,
                IsFirstVisit = isFirstVisit,
                VisitCount = visit.VisitCount,
                TotalVisits = visit.TotalVisits,
                DecayMultiplier = multiplier,
            };

            // XP lands on the POI's own mapped skill — no per-skill code.
            var hasSkill = await db.PlayerSkills
                .AnyAsync(s => s.PlayerId == playerId && s.SkillType == poi.Skill, ct);

            if (hasSkill)
            {
                var grant = await progression.GrantXp(playerId, poi.Skill, xp, XpSource.Poi, ct);

                result.SkillXpEarned = grant.SkillXpEarned;
                result.AdventurerXpEarned = grant.AdventurerXpEarned;
                result.SkillLevel = grant.SkillLevel;
                result.LevelledUp = grant.SkillLevelledUp;
                result.Unlocks.AddRange(grant.Unlocks);
            }

            // A Landmark plinth per POI *type*, recorded with where and when — the diary
            // (§5A). Uses the POI's own mapped skill, so it needs no per-skill code.
            var landmark = await museum.RecordFind(
                playerId,
                Services.Museum.MuseumSeedData.LandmarkKey(poi.Skill),
                1, poi.Id, poi.Name, ct);

            if (landmark is not null) result.MuseumAcquisitions.Add(landmark);

            if (isFirstVisit)
            {
                // First-ever visit to a POI is a novelty milestone (§3.0b) — it rewards
                // exploring rather than farming.
                var milestone = await progression.GrantMilestone(
                    playerId, MilestoneType.FirstPoiVisit, 1, ct);

                result.AdventurerXpEarned += milestone.AdventurerXpEarned;
                result.Unlocks.AddRange(milestone.Unlocks);
            }

            return result;
        }
    }
}
