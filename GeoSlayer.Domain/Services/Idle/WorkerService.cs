using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.DTOs.Idle.Responses;
using GeoSlayer.Domain.DTOs.Materials.Responses;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Exceptions;
using GeoSlayer.Domain.Interfaces.Api.Crafting;
using GeoSlayer.Domain.Interfaces.Api.Idle;
using GeoSlayer.Domain.Interfaces.Api.Materials;
using GeoSlayer.Domain.Interfaces.Api.Progression;
using GeoSlayer.Domain.Services.Fog;
using GeoSlayer.Domain.Services.Materials;
using GeoSlayer.Domain.Services.Progression;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Domain.Services.Idle
{
    /// <summary>
    /// Claims, workers and offline accrual (Stage 05, DESIGN.md §5.1–5.3).
    /// </summary>
    public class WorkerService(
        AppDbContext db,
        IProgressionService progression,
        IMaterialService materials,
        ICraftingService crafting) : IWorkerService
    {
        /// <summary>Edge length of a claimable block. A 3×3 must be fully revealed (§5.1).</summary>
        private const int ClaimSize = 3;

        /// <summary>
        /// Revealed cells required per Claim held — the density cap (§5.1), so a player
        /// cannot claim their whole neighbourhood on day one. Forces spreading out.
        /// </summary>
        private const int CellsPerClaim = 40;

        /// <summary>Materials one Claim costs.</summary>
        private static readonly (string Key, int Quantity)[] ClaimCost =
        [
            ("wild_grass", 25),
            ("scrap", 10),
        ];

        /// <summary>
        /// Which terrains each skill is good at, derived from the seeded terrain mappings so
        /// this cannot drift from what walking uses. Anything above base rate counts.
        /// </summary>
        private async Task<Dictionary<SkillType, TerrainType>> SkillTerrains(CancellationToken ct)
        {
            var mappings = await db.SkillTerrainMappings.ToListAsync(ct);

            var baseRates = mappings
                .Where(m => m.Terrain == TerrainType.Open)
                .ToDictionary(m => m.SkillType, m => m.XpPerCell);

            var result = new Dictionary<SkillType, TerrainType>();

            foreach (var mapping in mappings)
            {
                if (mapping.Terrain == TerrainType.Open)
                {
                    continue;
                }

                var baseRate = baseRates.GetValueOrDefault(mapping.SkillType, 0);
                if (mapping.XpPerCell <= baseRate)
                {
                    continue;
                }

                result[mapping.SkillType] = result.GetValueOrDefault(mapping.SkillType) | mapping.Terrain;
            }

            return result;
        }

        /// <summary>Base cap plus whatever the Offline Cap upgrade adds (§5.2).</summary>
        private async Task<double> OfflineCapHours(int playerId, CancellationToken ct)
        {
            var bonus = await progression.GetUpgradeEffect(
                playerId, ProgressionDefaults.UpgradeKeys.OfflineCap, ct);

            // A Hearthstone Charm extends the cap alongside the upgrade (§4.3).
            var gear = await crafting.GetModifierTotal(playerId, ItemModifier.OfflineCapHours, ct);

            return OfflineAccrual.BaseOfflineCapHours + bonus + gear;
        }

        // ── Offline accrual (§5.3) ──────────────────────────────────────

        public async Task<OfflineAccrualDto> CollectOfflineAccrual(int playerId, CancellationToken ct)
        {
            var workers = await db.Workers
                .Include(w => w.Claim)
                .Where(w => w.PlayerId == playerId && w.AssignedSkill != null && w.ClaimId != null)
                .ToListAsync(ct);

            var capHours = await OfflineCapHours(playerId, ct);

            var result = new OfflineAccrualDto { OfflineCapHours = capHours };

            if (workers.Count == 0)
            {
                return result;
            }

            var terrains = await SkillTerrains(ct);
            var now = DateTime.UtcNow;

            // Only unlocked skills accrue — a worker assigned before a skill was locked
            // again should not keep paying into it.
            var unlocked = (await db.PlayerSkills
                    .Where(s => s.PlayerId == playerId)
                    .Select(s => s.SkillType)
                    .ToListAsync(ct))
                .ToHashSet();

            var xpBySkill = new Dictionary<SkillType, double>();
            var maxHours = 0.0;
            var wasCapped = false;

            // Snapshot every worker's accrual *before* advancing any collection point, so the
            // XP and material passes describe the same window. Recomputing after the advance
            // would silently yield zero.
            var snapshots = new List<(Worker Worker, SkillType Skill, OfflineAccrual.Accrual Accrual)>();

            foreach (var worker in workers)
            {
                var skill = worker.AssignedSkill!.Value;
                if (!unlocked.Contains(skill))
                {
                    continue;
                }

                var accrual = OfflineAccrual.Compute(
                    worker.LastCollectedAtUtc,
                    now,
                    capHours,
                    worker.Tier,
                    worker.Claim?.TerrainProfile ?? TerrainType.Open,
                    terrains.GetValueOrDefault(skill, TerrainType.Open));

                if (accrual.Elapsed <= TimeSpan.Zero)
                {
                    continue;
                }

                snapshots.Add((worker, skill, accrual));

                xpBySkill[skill] = xpBySkill.GetValueOrDefault(skill) + accrual.SkillXp;

                maxHours = Math.Max(maxHours, accrual.Elapsed.TotalHours);
                wasCapped |= accrual.WasCapped;

                // Collection point advances even when the cap truncated the payout, so the
                // next window starts now rather than replaying the capped period.
                worker.LastCollectedAtUtc = now;
            }

            if (snapshots.Count == 0)
            {
                return result;
            }

            await db.SaveChangesAsync(ct);

            result.HoursAccrued = maxHours;
            result.WasCapped = wasCapped;

            foreach (var (skill, xp) in xpBySkill.OrderBy(kv => kv.Key))
            {
                var amount = (long)Math.Floor(xp);
                if (amount <= 0)
                {
                    continue;
                }

                // XpSource.Idle pays the reduced Adventurer ratio (§3.3), so idle never
                // drives the unlock ladder at walking pace.
                var grant = await progression.GrantXp(playerId, skill, amount, XpSource.Idle, ct);

                result.Skills.Add(new SkillAccrualDto
                {
                    SkillType = skill,
                    Name = skill.ToString(),
                    XpEarned = grant.SkillXpEarned,
                    Level = grant.SkillLevel,
                    LevelledUp = grant.SkillLevelledUp,
                });

                result.AdventurerXpEarned += grant.AdventurerXpEarned;
                result.BonusPointsGranted += grant.BonusPointsGranted;
                result.Unlocks.AddRange(grant.Unlocks);
            }

            // Upkeep (§5.2), deferred from Stage 05 until Cooking existed to supply food.
            // Unfed workers idle — they do not die and nothing accrued is lost, because
            // §7.4's "never punish you for sleeping" outranks the sink.
            result.UpkeepConsumed = await ConsumeUpkeep(playerId, snapshots, ct);
            result.WorkersWentUnfed = result.UpkeepConsumed.Unfed;
            result.WorkersWentUnpaid = result.UpkeepConsumed.Unpaid;

            result.Materials = await AwardWorkerMaterials(playerId, snapshots, ct);

            result.HasAccrual = result.Skills.Count > 0 || result.Materials.Count > 0;

            return result;
        }

        /// <summary>
        /// Pay and feed workers for the hours worked (§5.2).
        ///
        /// <para>Two costs, not two currencies for one cost: workers are <b>paid in coin and
        /// fed on top</b>, the way employment actually works. An earlier design had coin as a
        /// substitute for food, which would just have made players optimise to whichever was
        /// cheaper and ignore the other.</para>
        ///
        /// <para>Crucially this never throws and never rolls back what was already earned —
        /// an unpaid or unfed worker idles, it does not lose the night (§7.4).</para>
        /// </summary>
        private async Task<UpkeepDto> ConsumeUpkeep(
            int playerId,
            List<(Worker Worker, SkillType Skill, OfflineAccrual.Accrual Accrual)> snapshots,
            CancellationToken ct)
        {
            var required = snapshots.Sum(s => OfflineAccrual.FoodRequired(s.Accrual.Elapsed));
            var wages = snapshots.Sum(s => OfflineAccrual.WagesRequired(s.Accrual.Elapsed));

            var result = new UpkeepDto { FoodRequired = required, WagesRequired = wages };

            // Wages first, because they are the simpler half — a single balance rather than a
            // walk through the larder.
            if (wages > 0)
            {
                var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct);

                if (player is not null)
                {
                    // Partial payment is deliberate. Taking nothing when a player cannot
                    // cover the full bill would let them run workers indefinitely on an empty
                    // purse, and taking them into debt would punish someone for sleeping.
                    var paid = Math.Min(wages, player.Coin);

                    player.Coin -= paid;

                    result.WagesPaid = paid;
                    result.Unpaid = paid < wages;
                }
            }

            if (required <= 0)
            {
                if (result.WagesPaid > 0)
                {
                    await db.SaveChangesAsync(ct);
                }

                return result;
            }

            // Cheapest food first, so a player's Ambrosia is not eaten while rations sit
            // in the bag.
            var food = await db.PlayerMaterials
                .Include(pm => pm.Material)
                .Where(pm => pm.PlayerId == playerId
                          && pm.Quantity > 0
                          && pm.Material.Category == MaterialCategory.Cooked)
                .OrderBy(pm => pm.Material.Tier)
                .ToListAsync(ct);

            var remaining = required;

            foreach (var row in food)
            {
                if (remaining <= 0)
                {
                    break;
                }

                var taken = (int)Math.Min(remaining, row.Quantity);

                row.Quantity -= taken;
                remaining -= taken;

                result.Consumed.Add(new UpkeepLineDto
                {
                    MaterialId = row.MaterialId,
                    Name = row.Material.Name,
                    Quantity = taken,
                });
            }

            result.FoodConsumed = required - remaining;
            result.Unfed = remaining > 0;

            if (result.Consumed.Count > 0 || result.WagesPaid > 0)
            {
                await db.SaveChangesAsync(ct);
            }

            return result;
        }

        /// <summary>
        /// Materials workers produced. Respects stack caps with Dust overflow, so workers
        /// never hard-stall (§7.4) — the idle layer must not punish a player for sleeping.
        /// </summary>
        private async Task<List<MaterialGainDto>> AwardWorkerMaterials(
            int playerId,
            List<(Worker Worker, SkillType Skill, OfflineAccrual.Accrual Accrual)> snapshots,
            CancellationToken ct)
        {
            var skillLevels = await db.PlayerSkills
                .Where(s => s.PlayerId == playerId)
                .ToDictionaryAsync(s => s.SkillType, s => s.Level, ct);

            if (skillLevels.Count == 0)
            {
                return [];
            }

            var candidates = await db.Materials
                .Where(m => m.SkillType != null && !m.IsUnique)
                .ToListAsync(ct);

            var totals = new Dictionary<int, int>();

            foreach (var (_, skill, accrual) in snapshots)
            {
                if (!skillLevels.TryGetValue(skill, out var level))
                {
                    continue;
                }

                // Highest tier the player has unlocked in this skill — the same rule cells
                // use, so idle and walking agree on what a player can obtain.
                var best = candidates
                    .Where(m => m.SkillType == skill && m.LevelRequired <= level)
                    .OrderByDescending(m => m.Tier)
                    .FirstOrDefault();

                if (best is null)
                {
                    continue;
                }

                // Higher-tier materials take longer per unit, so a worker producing them
                // produces fewer of them (§4.1a) — this is what makes high-tier idle slow.
                var units = (int)Math.Floor(
                    accrual.MaterialUnits * (3.0 / Math.Max(1, best.BaseGatherSeconds)));

                if (units <= 0)
                {
                    continue;
                }

                totals[best.Id] = totals.GetValueOrDefault(best.Id) + units;
            }

            return totals.Count == 0 ? [] : await materials.GrantMaterials(playerId, totals, ct);
        }

        // ── Claims (§5.1) ───────────────────────────────────────────────

        public async Task<ClaimEligibilityDto> CheckClaimEligibility(
            int playerId, int centreGridLat, int centreGridLng, CancellationToken ct)
        {
            var required = ClaimSize * ClaimSize;
            var half = ClaimSize / 2;

            var revealed = await db.RevealedCells
                .CountAsync(r => r.PlayerId == playerId
                              && r.GridLat >= centreGridLat - half && r.GridLat <= centreGridLat + half
                              && r.GridLng >= centreGridLng - half && r.GridLng <= centreGridLng + half,
                            ct);

            var totalRevealed = await db.RevealedCells.CountAsync(r => r.PlayerId == playerId, ct);
            var claimsHeld = await db.Claims.CountAsync(c => c.PlayerId == playerId, ct);

            // Two independent limits: density (§5.1) and purchased Claim slots.
            var densityLimit = totalRevealed / CellsPerClaim;
            var slotBonus = await progression.GetUpgradeEffect(
                playerId, ProgressionDefaults.UpgradeKeys.ClaimSlot, ct);
            var slotLimit = 1 + (int)slotBonus;

            var limit = Math.Min(densityLimit, slotLimit);

            var cost = await BuildCost(playerId, ct);
            var canAfford = cost.All(c => c.Held >= c.Quantity);

            var result = new ClaimEligibilityDto
            {
                RevealedCells = revealed,
                RequiredCells = required,
                Cost = cost,
                CanAfford = canAfford,
                ClaimsHeld = claimsHeld,
                ClaimLimit = limit,
            };

            var overlapping = await db.Claims.AnyAsync(
                c => c.PlayerId == playerId
                  && Math.Abs(c.CentreGridLat - centreGridLat) < ClaimSize
                  && Math.Abs(c.CentreGridLng - centreGridLng) < ClaimSize,
                ct);

            result.Reason =
                revealed < required ? $"Only {revealed} of {required} cells revealed here"
                : overlapping ? "Overlaps a Claim you already hold"
                : claimsHeld >= limit
                    ? densityLimit <= slotLimit
                        ? $"Explore more ground — one Claim per {CellsPerClaim} revealed cells"
                        : "No free Claim slots — buy another with Bonus Points"
                : !canAfford ? "Not enough materials"
                : null;

            result.IsEligible = result.Reason is null;

            return result;
        }

        private async Task<List<MaterialCostDto>> BuildCost(int playerId, CancellationToken ct)
        {
            var keys = ClaimCost.Select(c => c.Key).ToList();

            var rows = await db.Materials
                .Where(m => keys.Contains(m.Key))
                .ToListAsync(ct);

            var held = await db.PlayerMaterials
                .Where(pm => pm.PlayerId == playerId)
                .ToDictionaryAsync(pm => pm.MaterialId, pm => pm.Quantity, ct);

            var cost = new List<MaterialCostDto>();

            foreach (var (key, quantity) in ClaimCost)
            {
                var material = rows.FirstOrDefault(m => m.Key == key);
                if (material is null)
                {
                    continue;
                }

                cost.Add(new MaterialCostDto
                {
                    MaterialId = material.Id,
                    Key = material.Key,
                    Name = material.Name,
                    Quantity = quantity,
                    Held = held.GetValueOrDefault(material.Id),
                });
            }

            return cost;
        }

        public async Task<ClaimDto> CreateClaim(
            int playerId, int centreGridLat, int centreGridLng, string? name, CancellationToken ct)
        {
            var eligibility = await CheckClaimEligibility(playerId, centreGridLat, centreGridLng, ct);

            if (!eligibility.IsEligible)
            {
                throw new BadRequestException(eligibility.Reason ?? "Cannot claim here.");
            }

            // Deduct the cost. Claims are permanent (§5.1), so this is charged once.
            foreach (var line in eligibility.Cost)
            {
                var row = await db.PlayerMaterials
                    .FirstAsync(pm => pm.PlayerId == playerId && pm.MaterialId == line.MaterialId, ct);

                row.Quantity -= line.Quantity;
            }

            var half = ClaimSize / 2;

            // Terrain profile is the union of the block's cells (§5.1).
            var terrain = TerrainType.Open;

            for (var lat = centreGridLat - half; lat <= centreGridLat + half; lat++)
            {
                for (var lng = centreGridLng - half; lng <= centreGridLng + half; lng++)
                {
                    terrain |= await materials.GetOrClassifyTerrain(lat, lng, ct);
                }
            }

            var claim = new Claim
            {
                PlayerId = playerId,
                CentreGridLat = centreGridLat,
                CentreGridLng = centreGridLng,
                Size = ClaimSize,
                TerrainProfile = terrain,
                Name = string.IsNullOrWhiteSpace(name) ? $"Claim {centreGridLat},{centreGridLng}" : name.Trim(),
                ClaimedUtc = DateTime.UtcNow,
            };

            db.Claims.Add(claim);
            await db.SaveChangesAsync(ct);

            // Claiming territory is a large Adventurer milestone (§3.0b).
            await progression.GrantMilestone(playerId, MilestoneType.ClaimTerritory, 1, ct);

            return ToDto(claim, 0);
        }

        public async Task<List<ClaimDto>> GetClaims(int playerId, CancellationToken ct)
        {
            var claims = await db.Claims
                .Where(c => c.PlayerId == playerId)
                .OrderBy(c => c.ClaimedUtc)
                .ToListAsync(ct);

            var workerCounts = await db.Workers
                .Where(w => w.PlayerId == playerId && w.ClaimId != null)
                .GroupBy(w => w.ClaimId!.Value)
                .Select(g => new { ClaimId = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var counts = workerCounts.ToDictionary(x => x.ClaimId, x => x.Count);

            return claims.Select(c => ToDto(c, counts.GetValueOrDefault(c.Id))).ToList();
        }

        private static ClaimDto ToDto(Claim claim, int workerCount)
        {
            var half = claim.Size / 2;

            var (south, west, _, _) = FogService.CellBounds(
                claim.CentreGridLat - half, claim.CentreGridLng - half);

            var (_, _, north, east) = FogService.CellBounds(
                claim.CentreGridLat + half, claim.CentreGridLng + half);

            return new ClaimDto
            {
                Id = claim.Id,
                Name = claim.Name,
                CentreGridLat = claim.CentreGridLat,
                CentreGridLng = claim.CentreGridLng,
                Size = claim.Size,
                TerrainProfile = claim.TerrainProfile,
                Terrains = TerrainNames(claim.TerrainProfile),
                ClaimedUtc = claim.ClaimedUtc,
                South = south,
                West = west,
                North = north,
                East = east,
                WorkerCount = workerCount,
            };
        }

        private static List<string> TerrainNames(TerrainType terrain)
        {
            if (terrain == TerrainType.Open)
            {
                return ["Open"];
            }

            return Enum.GetValues<TerrainType>()
                .Where(t => t != TerrainType.Open && (terrain & t) == t)
                .Select(t => t.ToString())
                .ToList();
        }

        // ── Workers (§5.2) ──────────────────────────────────────────────

        public async Task<List<WorkerDto>> GetWorkers(int playerId, CancellationToken ct)
        {
            var workers = await db.Workers
                .Include(w => w.Claim)
                .Where(w => w.PlayerId == playerId)
                .OrderBy(w => w.Id)
                .ToListAsync(ct);

            var terrains = await SkillTerrains(ct);
            var capHours = await OfflineCapHours(playerId, ct);
            var now = DateTime.UtcNow;

            return workers.Select(w => ToDto(w, terrains, capHours, now)).ToList();
        }

        private static WorkerDto ToDto(
            Worker worker,
            Dictionary<SkillType, TerrainType> terrains,
            double capHours,
            DateTime now)
        {
            var claimTerrain = worker.Claim?.TerrainProfile ?? TerrainType.Open;

            var skillTerrains = worker.AssignedSkill is null
                ? TerrainType.Open
                : terrains.GetValueOrDefault(worker.AssignedSkill.Value, TerrainType.Open);

            var matches = OfflineAccrual.TerrainMatches(claimTerrain, skillTerrains);
            var multiplier = matches ? OfflineAccrual.MatchingTerrainMultiplier : 1.0;
            var tierMultiplier = 1 + Math.Max(0, worker.Tier - 1) * OfflineAccrual.PerTierBonus;

            var idle = worker.ClaimId is null || worker.AssignedSkill is null;
            var capReached = OfflineAccrual.CapReachedAtUtc(worker.LastCollectedAtUtc, capHours);

            return new WorkerDto
            {
                Id = worker.Id,
                Name = worker.Name,
                Tier = worker.Tier,
                ClaimId = worker.ClaimId,
                ClaimName = worker.Claim?.Name,
                AssignedSkill = worker.AssignedSkill,
                AssignedSkillName = worker.AssignedSkill?.ToString(),
                LastCollectedAtUtc = worker.LastCollectedAtUtc,
                XpPerHour = idle ? 0 : OfflineAccrual.BaseXpPerHour * tierMultiplier * multiplier,
                MaterialsPerHour = idle ? 0 : OfflineAccrual.BaseMaterialsPerHour * tierMultiplier * multiplier,
                TerrainMultiplier = multiplier,
                TerrainMatches = matches,
                CapReachedAtUtc = capReached,
                IsAtCap = !idle && now >= capReached,
                IsIdle = idle,
            };
        }

        public async Task<WorkerDto> AssignWorker(
            int playerId, int workerId, int? claimId, SkillType? skill, CancellationToken ct)
        {
            var worker = await db.Workers
                .Include(w => w.Claim)
                .FirstOrDefaultAsync(w => w.Id == workerId && w.PlayerId == playerId, ct)
                ?? throw new NotFoundException($"Worker {workerId} not found.");

            if (claimId is not null)
            {
                var owns = await db.Claims.AnyAsync(c => c.Id == claimId && c.PlayerId == playerId, ct);

                if (!owns)
                {
                    throw new BadRequestException("That Claim is not yours.");
                }
            }

            if (skill is not null)
            {
                // Unlocked, not terrain-matched. Any unlocked skill on any Claim (§5.2) —
                // requiring matching terrain here is the geographic lockout the whole design
                // exists to avoid.
                var unlocked = await db.PlayerSkills
                    .AnyAsync(s => s.PlayerId == playerId && s.SkillType == skill, ct);

                if (!unlocked)
                {
                    throw new BadRequestException($"{skill} is not unlocked yet.");
                }
            }

            // Reassigning banks whatever accrued first, so a switch never silently discards
            // hours the player already earned.
            if (worker.ClaimId is not null && worker.AssignedSkill is not null)
            {
                await CollectOfflineAccrual(playerId, ct);
            }

            worker.ClaimId = claimId;
            worker.AssignedSkill = skill;
            worker.LastCollectedAtUtc = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);

            var terrains = await SkillTerrains(ct);
            var capHours = await OfflineCapHours(playerId, ct);

            await db.Entry(worker).Reference(w => w.Claim).LoadAsync(ct);

            return ToDto(worker, terrains, capHours, DateTime.UtcNow);
        }

        public async Task<WorkerDto> HireWorker(int playerId, CancellationToken ct)
        {
            var held = await db.Workers.CountAsync(w => w.PlayerId == playerId, ct);

            // One free worker, then one per Worker Slot rank (§5.2: the flagship purchase).
            var bonus = await progression.GetUpgradeEffect(
                playerId, ProgressionDefaults.UpgradeKeys.WorkerSlot, ct);

            var capacity = 1 + (int)bonus;

            if (held >= capacity)
            {
                throw new BadRequestException(
                    $"All {capacity} worker slot{(capacity == 1 ? "" : "s")} are full — buy another with Bonus Points.");
            }

            var now = DateTime.UtcNow;

            var worker = new Worker
            {
                PlayerId = playerId,
                Name = $"Worker {held + 1}",
                Tier = 1,
                StartedAtUtc = now,
                LastCollectedAtUtc = now,
            };

            db.Workers.Add(worker);
            await db.SaveChangesAsync(ct);

            var terrains = await SkillTerrains(ct);
            var capHours = await OfflineCapHours(playerId, ct);

            return ToDto(worker, terrains, capHours, now);
        }
    }
}
