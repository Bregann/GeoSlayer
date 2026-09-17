using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.DTOs.Clues.Responses;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Exceptions;
using GeoSlayer.Domain.Interfaces.Api;
using GeoSlayer.Domain.Services.Fog;
using GeoSlayer.Domain.Services.Museum;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Domain.Services.Clues
{
    /// <summary>
    /// Clue scrolls (Stage 13, DESIGN.md §5B).
    /// </summary>
    public class ClueService(
        AppDbContext db,
        IMaterialService materials,
        IMuseumService museum) : IClueService
    {
        /// <summary>
        /// Relic keys a completed scroll can award, by tier. Seeded here rather than in JSON
        /// because they are Museum entry keys, and the Museum owns its own vocabulary.
        /// </summary>
        private static readonly Dictionary<ClueTier, string[]> RelicsByTier = new()
        {
            [ClueTier.Wandering] = ["relic:worn_milestone", "relic:brass_token"],
            [ClueTier.Roaming] = ["relic:folded_map", "relic:tarnished_compass"],
            [ClueTier.Pilgrim] = ["relic:pilgrims_badge", "relic:sealed_letter"],
            [ClueTier.Odyssey] = ["relic:wayfarers_lantern", "relic:cartographers_seal"],
        };

        /// <summary>Every Relic, for the Museum seeder.</summary>
        public static IEnumerable<(ClueTier Tier, string Key, string Name)> AllRelics()
        {
            var names = new Dictionary<string, string>
            {
                ["relic:worn_milestone"] = "Worn Milestone",
                ["relic:brass_token"] = "Brass Token",
                ["relic:folded_map"] = "Folded Map",
                ["relic:tarnished_compass"] = "Tarnished Compass",
                ["relic:pilgrims_badge"] = "Pilgrim's Badge",
                ["relic:sealed_letter"] = "Sealed Letter",
                ["relic:wayfarers_lantern"] = "Wayfarer's Lantern",
                ["relic:cartographers_seal"] = "Cartographer's Seal",
            };

            foreach (var (tier, keys) in RelicsByTier)
                foreach (var key in keys)
                    yield return (tier, key, names.GetValueOrDefault(key, key));
        }

        public async Task<List<ClueScrollDto>> GetScrolls(int playerId, CancellationToken ct)
        {
            var scrolls = await db.PlayerClueScrolls
                .Include(s => s.Steps)
                .Where(s => s.PlayerId == playerId)
                .OrderBy(s => s.CompletedUtc == null ? 0 : 1)
                .ThenBy(s => s.Tier)
                .ToListAsync(ct);

            return scrolls.Select(ToDto).ToList();
        }

        private static ClueScrollDto ToDto(PlayerClueScroll scroll)
        {
            var steps = scroll.Steps.OrderBy(s => s.StepIndex).ToList();

            return new ClueScrollDto
            {
                Id = scroll.Id,
                Tier = scroll.Tier,
                TierName = scroll.Tier.ToString(),
                CurrentStep = scroll.CurrentStep,
                StepCount = steps.Count,
                IsComplete = scroll.CompletedUtc is not null,
                SkipUsed = scroll.SkipUsed,
                SkipCost = ClueTierConfig.For(scroll.Tier).SkipCostCuration,
                StartedUtc = scroll.StartedUtc,
                CompletedUtc = scroll.CompletedUtc,

                // Solved steps and the current one only. Showing the whole chain would let a
                // player plan a route the clue is meant to reveal one leg at a time.
                Steps = steps
                    .Where(s => s.StepIndex <= scroll.CurrentStep)
                    .Select(ToDto)
                    .ToList(),
            };
        }

        private static ClueStepDto ToDto(ClueStep step) => new()
        {
            StepIndex = step.StepIndex,
            StepType = step.StepType,
            RiddleText = step.RiddleText,

            // Only a coordinate step exposes a position, and only as a search area. A Direct
            // or Cryptic step's POI location stays server-side — handing it over would turn
            // the riddle into a map pin.
            SearchLat = step.StepType == ClueStepType.Coordinate ? step.TargetLat : null,
            SearchLng = step.StepType == ClueStepType.Coordinate ? step.TargetLng : null,
            SearchRadius = step.StepType == ClueStepType.Coordinate ? step.TargetRadius : null,

            IsSolved = step.SolvedUtc is not null,
            WasSkipped = step.WasSkipped,
            SolvedUtc = step.SolvedUtc,
        };

        // ── Generation (§5B.3: always achievable) ───────────────────────

        public async Task<ClueScrollDto?> GenerateScroll(int playerId, ClueTier tier, CancellationToken ct)
        {
            // One active scroll per tier (§5B.1) — stops hoarding, keeps each meaningful.
            var existing = await db.PlayerClueScrolls
                .AnyAsync(s => s.PlayerId == playerId && s.Tier == tier && s.CompletedUtc == null, ct);

            if (existing)
                throw new BadRequestException($"You already carry a {tier} scroll.");

            var shape = ClueTierConfig.For(tier);

            // Anchor on the player's own revealed territory. This is the whole of §5B.3's
            // "always achievable" rule: a step is only ever placed near ground they have
            // actually walked, so a rural player gets a rural clue.
            var cells = await db.RevealedCells
                .Where(r => r.PlayerId == playerId)
                .Select(r => new { r.GridLat, r.GridLng })
                .ToListAsync(ct);

            if (cells.Count == 0) return null;

            var seed = HashCode.Combine(playerId, tier, DateTime.UtcNow.Date.DayOfYear);
            var random = new Random(seed);

            var anchor = cells[random.Next(cells.Count)];
            var (anchorLat, anchorLng) = CellCentre(anchor.GridLat, anchor.GridLng);

            // Candidate POIs within the tier's radius of that anchor. Bounding the *query*
            // rather than filtering afterwards is what guarantees no 200-mile step.
            var degreeRadius = shape.SearchRadiusMetres / 111_320.0;

            var candidates = await db.PointsOfInterest
                .Where(p => p.Location.Y >= anchorLat - degreeRadius
                         && p.Location.Y <= anchorLat + degreeRadius
                         && p.Location.X >= anchorLng - degreeRadius * 2
                         && p.Location.X <= anchorLng + degreeRadius * 2)
                .Select(p => new { p.Id, p.Name, p.Skill, Lat = p.Location.Y, Lng = p.Location.X })
                .Take(200)
                .ToListAsync(ct);

            var scroll = new PlayerClueScroll
            {
                PlayerId = playerId,
                Tier = tier,
                CurrentStep = 0,
                StartedUtc = DateTime.UtcNow,
            };

            var usedPoiIds = new HashSet<int>();

            for (var index = 0; index < shape.StepCount; index++)
            {
                var available = candidates.Where(c => !usedPoiIds.Contains(c.Id)).ToList();

                ClueStep step;

                if (available.Count > 0)
                {
                    var poi = available[random.Next(available.Count)];
                    usedPoiIds.Add(poi.Id);

                    // Direct for the first step so a new player learns the mechanic on an
                    // easy one; Category afterwards, which works even where POIs are thin.
                    var type = index == 0 ? ClueStepType.Direct : ClueStepType.Category;

                    step = new ClueStep
                    {
                        StepIndex = index,
                        StepType = type,
                        TargetPoiId = type == ClueStepType.Direct ? poi.Id : null,
                        TargetSkill = poi.Skill,
                        TargetLat = poi.Lat,
                        TargetLng = poi.Lng,
                        TargetRadius = ClueTierConfig.PoiArrivalRadiusMetres,
                        RiddleText = type == ClueStepType.Direct
                            ? ClueRiddleText.Direct(poi.Name, poi.Skill, seed + index)
                            : ClueRiddleText.Category(poi.Skill, seed + index),
                    };
                }
                else
                {
                    // No POI to hand — fall back to a coordinate step on the player's own
                    // revealed ground. A sparse-POI player must still get a completable
                    // scroll (criterion 9), and a cell they revealed is by definition
                    // reachable.
                    var cell = cells[random.Next(cells.Count)];
                    var (lat, lng) = CellCentre(cell.GridLat, cell.GridLng);

                    step = new ClueStep
                    {
                        StepIndex = index,
                        StepType = ClueStepType.Coordinate,
                        TargetLat = lat,
                        TargetLng = lng,
                        TargetRadius = ClueTierConfig.CoordinateArrivalRadiusMetres,
                        RiddleText = ClueRiddleText.Coordinate(ClueTierConfig.CoordinateArrivalRadiusMetres),
                    };
                }

                scroll.Steps.Add(step);
            }

            db.PlayerClueScrolls.Add(scroll);
            await db.SaveChangesAsync(ct);

            return ToDto(scroll);
        }

        private static (double Lat, double Lng) CellCentre(int gridLat, int gridLng)
        {
            var (south, west, north, east) = FogService.CellBounds(gridLat, gridLng);
            return ((south + north) / 2, (west + east) / 2);
        }

        // ── Attempting a step (§5B.3: respect the anti-cheat) ───────────

        public async Task<ClueProgressDto> AttemptStep(int playerId, int scrollId, CancellationToken ct)
        {
            var (scroll, step, player) = await LoadCurrent(playerId, scrollId, ct);

            // Validated against the server's own last verified position — the client never
            // supplies one, exactly as with POI visits. Trusting a client here would make
            // clue completion a free teleport.
            if (player.LastSyncAtUtc is null)
                throw new BadRequestException("No verified position yet — sync before attempting a clue.");

            if (step.TargetLat is null || step.TargetLng is null)
                throw new BadRequestException("That step has no location to check.");

            var distance = TraceValidator.HaversineMetres(
                player.LastLatitude, player.LastLongitude, step.TargetLat.Value, step.TargetLng.Value);

            var radius = step.TargetRadius ?? ClueTierConfig.PoiArrivalRadiusMetres;

            if (distance > radius)
            {
                throw new BadRequestException(
                    step.StepType == ClueStepType.Coordinate
                        ? "Not here. Keep searching."
                        : "You are not there yet.");
            }

            step.SolvedUtc = DateTime.UtcNow;

            return await Advance(scroll, ct);
        }

        public async Task<ClueProgressDto> SkipStep(int playerId, int scrollId, CancellationToken ct)
        {
            var (scroll, step, player) = await LoadCurrent(playerId, scrollId, ct);

            // One skip per scroll (§5B.3): a clue the player cannot solve must never
            // permanently block their only scroll of that tier — but it should cost.
            if (scroll.SkipUsed)
                throw new BadRequestException("You have already skipped a step on this scroll.");

            var cost = ClueTierConfig.For(scroll.Tier).SkipCostCuration;

            if (player.Curation < cost)
                throw new BadRequestException($"Skipping costs {cost} curation; you have {player.Curation}.");

            player.Curation -= cost;
            scroll.SkipUsed = true;

            step.SolvedUtc = DateTime.UtcNow;
            step.WasSkipped = true;

            return await Advance(scroll, ct);
        }

        private async Task<(PlayerClueScroll Scroll, ClueStep Step, Player Player)> LoadCurrent(
            int playerId, int scrollId, CancellationToken ct)
        {
            var scroll = await db.PlayerClueScrolls
                .Include(s => s.Steps)
                .FirstOrDefaultAsync(s => s.Id == scrollId && s.PlayerId == playerId, ct)
                ?? throw new NotFoundException($"Scroll {scrollId} not found.");

            if (scroll.CompletedUtc is not null)
                throw new BadRequestException("That scroll is already complete.");

            var step = scroll.Steps.FirstOrDefault(s => s.StepIndex == scroll.CurrentStep)
                ?? throw new BadRequestException("That scroll has no current step.");

            var player = await db.Players.FirstAsync(p => p.Id == playerId, ct);

            return (scroll, step, player);
        }

        private async Task<ClueProgressDto> Advance(PlayerClueScroll scroll, CancellationToken ct)
        {
            scroll.CurrentStep += 1;

            var result = new ClueProgressDto { ScrollId = scroll.Id, StepSolved = true };

            var isComplete = scroll.CurrentStep >= scroll.Steps.Count;

            if (isComplete)
            {
                scroll.CompletedUtc = DateTime.UtcNow;
                result.ScrollComplete = true;
            }

            await db.SaveChangesAsync(ct);

            if (isComplete)
            {
                result.Reward = await AwardRewards(scroll, ct);
            }
            else
            {
                result.NextStep = ToDto(scroll.Steps.First(s => s.StepIndex == scroll.CurrentStep));
            }

            return result;
        }

        /// <summary>
        /// Roll the tier's reward table.
        ///
        /// <para><b>Raw power is deliberately low</b> (§5B.3): Relics, Curation and a little
        /// material. A player who ignores clues entirely must not fall behind, so there is no
        /// XP and no gear here — the pull is collection, not stats.</para>
        /// </summary>
        private async Task<ClueRewardDto> AwardRewards(PlayerClueScroll scroll, CancellationToken ct)
        {
            var shape = ClueTierConfig.For(scroll.Tier);
            var reward = new ClueRewardDto();

            var player = await db.Players.FirstAsync(p => p.Id == scroll.PlayerId, ct);

            player.Curation += shape.CurationReward;
            reward.CurationEarned = shape.CurationReward;

            await db.SaveChangesAsync(ct);

            // One Relic per completed scroll, chosen from the tier's table. Deterministic on
            // the scroll id so a retry cannot re-roll for a better one.
            var pool = RelicsByTier[scroll.Tier];
            var relicKey = pool[Math.Abs(scroll.Id) % pool.Length];

            var relic = await museum.RecordFind(
                scroll.PlayerId, relicKey, 1, null, null, ct);

            if (relic is not null) reward.Relics.Add(relic);

            // A little material, scaled by tier. Enough to feel like a find, not enough to
            // make clues the efficient way to gather.
            var material = await db.Materials
                .Where(m => !m.IsUnique && m.LevelRequired == 1)
                .OrderBy(m => m.Id)
                .FirstOrDefaultAsync(ct);

            if (material is not null)
            {
                var quantity = (int)scroll.Tier * 5 + 5;

                reward.Materials = await materials.GrantMaterials(
                    scroll.PlayerId, new Dictionary<int, int> { [material.Id] = quantity }, ct);
            }

            return reward;
        }
    }
}
