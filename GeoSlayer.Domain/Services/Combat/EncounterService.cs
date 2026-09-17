using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.DTOs.Combat.Responses;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Exceptions;
using GeoSlayer.Domain.Interfaces.Api.Combat;
using GeoSlayer.Domain.Interfaces.Api.Materials;
using GeoSlayer.Domain.Interfaces.Api.Museum;
using GeoSlayer.Domain.Interfaces.Api.Progression;
using GeoSlayer.Domain.Services.Fog;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Domain.Services.Combat
{
    /// <summary>
    /// Combat encounters (DESIGN.md §5C).
    ///
    /// <para>The rule this service exists to honour is §5C.2: <b>historic POIs are a boost,
    /// never the only venue</b>. Roaming encounters spawn at any POI category, so a player
    /// with no castle within twenty miles still trains Combat — slower, never blocked.</para>
    /// </summary>
    public class EncounterService(
        AppDbContext db,
        IMaterialService materials,
        IProgressionService progression,
        IMuseumService museum) : IEncounterService
    {
        /// <summary>How far from the player encounters are offered.</summary>
        private const double SpawnRadiusMetres = 2000.0;

        /// <summary>Roaming encounters offered at once, so the map stays legible.</summary>
        private const int MaxRoamingPerWindow = 3;

        public async Task<List<EncounterDto>> GetEncounters(int playerId, CancellationToken ct)
        {
            var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct)
                ?? throw new NotFoundException($"Player {playerId} not found.");

            await SpawnFor(player, ct);

            var now = DateTime.UtcNow;

            var open = await db.PlayerEncounters
                .Include(e => e.Poi)
                .Where(e => e.PlayerId == playerId && e.ResolvedUtc == null
                         && (e.ExpiresUtc == null || e.ExpiresUtc > now))
                .ToListAsync(ct);

            if (open.Count == 0)
            {
                return [];
            }

            var definitions = await db.EncounterDefinitions.ToListAsync(ct);
            var byKey = definitions.ToDictionary(d => d.Key);
            var combatLevel = await CombatLevel(playerId, ct);

            return open
                .Where(e => byKey.ContainsKey(e.DefinitionKey))
                .Select(e =>
                {
                    var definition = byKey[e.DefinitionKey];

                    var distance = TraceValidator.HaversineMetres(
                        player.LastLatitude, player.LastLongitude,
                        e.Poi.Location.Y, e.Poi.Location.X);

                    return new EncounterDto
                    {
                        Id = e.Id,
                        Key = definition.Key,
                        Name = definition.Name,
                        Description = definition.Description,
                        PoiId = e.PoiId,
                        PoiName = e.Poi.Name,
                        Latitude = e.Poi.Location.Y,
                        Longitude = e.Poi.Location.X,
                        Tier = definition.Tier,
                        MinCombatLevel = definition.MinCombatLevel,
                        IsTrainingGround = definition.IsTrainingGround,
                        ExpiresUtc = e.ExpiresUtc,
                        WinChance = EncounterResolution.WinChance(combatLevel, definition.MinCombatLevel),
                        DistanceMetres = distance,
                        IsInRange = distance <= EncounterResolution.ArrivalRadiusMetres,
                    };
                })
                // Training grounds last: they will still be there tomorrow, so the expiring
                // ones are the ones worth surfacing first.
                .OrderBy(d => d.IsTrainingGround)
                .ThenBy(d => d.DistanceMetres)
                .ToList();
        }

        /// <summary>
        /// Spawns encounters near the player, deterministic per cell and window.
        ///
        /// <para>Reuses the Resource Surge shape (§5.6): two players in the same place on the
        /// same day are offered the same fights, and the work is cheap enough to run on
        /// every request.</para>
        /// </summary>
        private async Task SpawnFor(Player player, CancellationToken ct)
        {
            // Without a verified position there is nowhere to spawn against. Same reasoning
            // as VisitPoi: a seeded or imported lat/lng is not proof of location (§7.2).
            if (player.LastSyncAtUtc is null)
            {
                return;
            }

            var now = DateTime.UtcNow;
            var combatLevel = await CombatLevel(player.Id, ct);

            var definitions = await db.EncounterDefinitions
                .Where(d => d.MinCombatLevel <= combatLevel)
                .ToListAsync(ct);

            if (definitions.Count == 0)
            {
                return;
            }

            var nearby = await NearbyPois(player, ct);

            if (nearby.Count == 0)
            {
                return;
            }

            var existing = (await db.PlayerEncounters
                .Where(e => e.PlayerId == player.Id)
                .Select(e => new { e.PoiId, e.DefinitionKey })
                .ToListAsync(ct))
                .Select(e => (e.PoiId, e.DefinitionKey))
                .ToHashSet();

            var added = new List<PlayerEncounter>();

            // ── Training grounds: permanent, on historic ground ──────────────────────────
            // Seeded from the POI's own Combat mapping, so no per-skill branch here.
            var trainingTiers = definitions.Where(d => d.IsTrainingGround)
                .OrderByDescending(d => d.Tier).ToList();

            if (trainingTiers.Count > 0)
            {
                foreach (var poi in nearby.Where(p => p.Skill == EncounterSeedData.Skill))
                {
                    var definition = trainingTiers[0];

                    if (existing.Contains((poi.Id, definition.Key)))
                    {
                        continue;
                    }

                    added.Add(new PlayerEncounter
                    {
                        PlayerId = player.Id,
                        DefinitionKey = definition.Key,
                        PoiId = poi.Id,
                        SpawnedUtc = now,
                        ExpiresUtc = null,
                    });

                    existing.Add((poi.Id, definition.Key));
                }
            }

            // ── Roaming: any POI category, which is what §5C.2 turns on ──────────────────
            var roaming = definitions.Where(d => !d.IsTrainingGround).ToList();

            if (roaming.Count > 0)
            {
                var cellLat = Museum.PoiRegionResolver.SnapToGrid(player.LastLatitude);
                var cellLng = Museum.PoiRegionResolver.SnapToGrid(player.LastLongitude);

                // Deterministic on cell and day, like a Surge: the same place offers the same
                // fights, and a player cannot re-roll by syncing repeatedly.
                var random = new Random(HashCode.Combine(cellLat, cellLng, now.Date.DayOfYear));

                var openRoaming = await db.PlayerEncounters
                    .CountAsync(e => e.PlayerId == player.Id && e.ResolvedUtc == null
                                  && e.ExpiresUtc != null && e.ExpiresUtc > now, ct);

                for (var i = openRoaming; i < MaxRoamingPerWindow; i++)
                {
                    var poi = nearby[random.Next(nearby.Count)];
                    var definition = roaming[random.Next(roaming.Count)];

                    if (existing.Contains((poi.Id, definition.Key)))
                    {
                        continue;
                    }

                    added.Add(new PlayerEncounter
                    {
                        PlayerId = player.Id,
                        DefinitionKey = definition.Key,
                        PoiId = poi.Id,
                        SpawnedUtc = now,
                        ExpiresUtc = now.Add(EncounterResolution.RoamingLifetime),
                    });

                    existing.Add((poi.Id, definition.Key));
                }
            }

            if (added.Count == 0)
            {
                return;
            }

            db.PlayerEncounters.AddRange(added);

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Two concurrent syncs racing the same deterministic spawn. The unique index
                // is the arbiter; losing is harmless.
                db.ChangeTracker.Clear();
            }
        }

        private async Task<List<PointOfInterest>> NearbyPois(Player player, CancellationToken ct)
        {
            // A coarse box first so the database does the bulk of the filtering, then an
            // exact distance in memory — the same approach the POI preloader uses.
            var degrees = SpawnRadiusMetres / 111_000.0;

            var candidates = await db.PointsOfInterest
                .Where(p => p.Location.Y >= player.LastLatitude - degrees
                         && p.Location.Y <= player.LastLatitude + degrees
                         && p.Location.X >= player.LastLongitude - degrees
                         && p.Location.X <= player.LastLongitude + degrees)
                .Take(200)
                .ToListAsync(ct);

            return candidates
                .Where(p => TraceValidator.HaversineMetres(
                    player.LastLatitude, player.LastLongitude,
                    p.Location.Y, p.Location.X) <= SpawnRadiusMetres)
                .OrderBy(p => p.Id)
                .ToList();
        }

        public async Task<EncounterResultDto> Resolve(int playerId, int encounterId, CancellationToken ct)
        {
            var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct)
                ?? throw new NotFoundException($"Player {playerId} not found.");

            var encounter = await db.PlayerEncounters
                .Include(e => e.Poi)
                .FirstOrDefaultAsync(e => e.Id == encounterId && e.PlayerId == playerId, ct)
                ?? throw new NotFoundException($"Encounter {encounterId} not found.");

            if (encounter.ResolvedUtc is not null)
            {
                throw new BadRequestException("That encounter is already over.");
            }

            var now = DateTime.UtcNow;

            if (encounter.ExpiresUtc is not null && encounter.ExpiresUtc <= now)
            {
                throw new BadRequestException("That encounter has moved on.");
            }

            // ── Anti-cheat: range, against the server's own last known position ──────────
            // Identical to VisitPoi and clue steps (§7.2). The client never supplies a
            // position, so an encounter cannot become a free teleport.
            if (player.LastSyncAtUtc is null)
            {
                throw new BadRequestException("No verified position yet — sync before fighting.");
            }

            var distance = TraceValidator.HaversineMetres(
                player.LastLatitude, player.LastLongitude,
                encounter.Poi.Location.Y, encounter.Poi.Location.X);

            if (distance > EncounterResolution.ArrivalRadiusMetres)
            {
                throw new BadRequestException(
                    $"Too far from {encounter.Poi.Name} — {Math.Round(distance)}m away.");
            }

            var definition = await db.EncounterDefinitions
                .FirstOrDefaultAsync(d => d.Key == encounter.DefinitionKey, ct)
                ?? throw new NotFoundException($"Encounter definition {encounter.DefinitionKey} missing.");

            var combatLevel = await CombatLevel(playerId, ct);
            var won = EncounterResolution.Resolve(encounter.Id, combatLevel, definition.MinCombatLevel);

            encounter.ResolvedUtc = now;
            encounter.Won = won;

            // A training ground is permanent, so it reopens rather than being consumed.
            // That is precisely the advantage of knowing a ruin nearby (§5C.1).
            if (definition.IsTrainingGround)
            {
                encounter.ResolvedUtc = null;
                encounter.SpawnedUtc = now;
            }

            var result = new EncounterResultDto
            {
                EncounterId = encounter.Id,
                Name = definition.Name,
                Won = won,
                Message = won
                    ? $"You saw off the {definition.Name.ToLowerInvariant()}."
                    // Never phrased as a loss of anything: nothing was taken (§5C.3).
                    : $"The {definition.Name.ToLowerInvariant()} got the better of you this time.",
            };

            var hasCombat = await db.PlayerSkills
                .AnyAsync(s => s.PlayerId == playerId && s.SkillType == EncounterSeedData.Skill, ct);

            if (hasCombat)
            {
                var grant = await progression.GrantXp(
                    playerId, EncounterSeedData.Skill,
                    EncounterResolution.XpFor(definition.Tier, won), XpSource.Poi, ct);

                result.SkillXpEarned = grant.SkillXpEarned;
                result.AdventurerXpEarned = grant.AdventurerXpEarned;
                result.CombatLevel = grant.SkillLevel;
                result.LevelledUp = grant.SkillLevelledUp;
                result.Unlocks.AddRange(grant.Unlocks);
            }

            // Materials only on a win — but a loss still pays XP above, so the walk is never
            // wasted. Losing costs time, never materials (§5C.3).
            if (won)
            {
                var material = await db.Materials
                    .Where(m => m.SkillType == EncounterSeedData.Skill
                             && m.Tier == definition.Tier && !m.IsUnique)
                    .FirstOrDefaultAsync(ct);

                if (material is not null)
                {
                    var gains = await materials.GrantMaterials(
                        playerId,
                        new Dictionary<int, int>
                        {
                            [material.Id] = EncounterResolution.MaterialsFor(definition.Tier),
                        },
                        ct);

                    result.Materials.AddRange(gains);

                    var acquisition = await museum.RecordFind(
                        playerId, material.Key, definition.Tier,
                        encounter.PoiId, encounter.Poi.Name, ct);

                    if (acquisition is not null)
                    {
                        result.MuseumAcquisitions.Add(acquisition);
                    }
                }
            }

            await db.SaveChangesAsync(ct);

            return result;
        }

        private async Task<int> CombatLevel(int playerId, CancellationToken ct)
        {
            var skill = await db.PlayerSkills
                .FirstOrDefaultAsync(s => s.PlayerId == playerId && s.SkillType == EncounterSeedData.Skill, ct);

            return skill?.Level ?? 1;
        }
    }
}
