using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.DTOs.Museum.Responses;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using GeoSlayer.Domain.Interfaces.Api.Museum;

namespace GeoSlayer.Domain.Services.Museum
{
    /// <summary>
    /// The Museum (Stage 12, DESIGN.md §5A).
    /// </summary>
    public class MuseumService(AppDbContext db, IRegionResolver regions) : IMuseumService
    {
        /// <summary>
        /// Curation paid per donated duplicate, by rarity.
        ///
        /// Deliberately modest: §5A.1 wants a "dignified sink" for spares, not a second
        /// economy that makes hoarding duplicates the optimal play.
        /// </summary>
        private static int CurationFor(MuseumRarity rarity) => rarity switch
        {
            MuseumRarity.Legendary => 50,
            MuseumRarity.Rare => 15,
            MuseumRarity.Uncommon => 5,
            _ => 1,
        };

        public async Task<MuseumDto> GetMuseum(int playerId, CancellationToken ct)
        {
            var definitions = await db.MuseumEntryDefinitions
                .OrderBy(d => d.Wing)
                .ThenBy(d => d.SortOrder)
                .ToListAsync(ct);

            var found = await db.PlayerMuseumEntries
                .Where(e => e.PlayerId == playerId)
                .ToDictionaryAsync(e => e.EntryKey, ct);

            var player = await db.Players
                .Where(p => p.Id == playerId)
                .Select(p => new { p.Curation })
                .FirstOrDefaultAsync(ct);

            var dto = new MuseumDto { Curation = player?.Curation ?? 0 };

            // Every wing appears, including ones with no entries yet (Relics, Expeditions).
            // An absent wing reads as "not in this game"; an empty one reads as "not yet",
            // which is the correct promise.
            foreach (var wing in Enum.GetValues<MuseumWing>())
            {
                var wingEntries = definitions.Where(d => d.Wing == wing).ToList();

                var entries = wingEntries.Select(d =>
                {
                    var owned = found.GetValueOrDefault(d.Key);

                    return new MuseumEntryDto
                    {
                        Key = d.Key,
                        Wing = d.Wing,
                        Name = d.Name,
                        Description = d.Description,
                        Rarity = d.Rarity,
                        UnlockCondition = d.UnlockCondition,
                        IsFound = owned is not null,
                        FirstAcquiredUtc = owned?.FirstAcquiredUtc,
                        AcquiredAtName = owned?.AcquiredAtName,
                        Quantity = owned?.Quantity ?? 0,
                        DonatableQuantity = Math.Max(0, (owned?.Quantity ?? 0) - (owned?.DonatedQuantity ?? 0) - 1),
                    };
                }).ToList();

                var foundCount = entries.Count(e => e.IsFound);

                var isComplete = entries.Count > 0 && foundCount == entries.Count;

                var bonus = MuseumSetBonus.Bonuses.TryGetValue(wing, out var b) ? b : (MuseumSetBonus.Bonus?)null;

                dto.Wings.Add(new MuseumWingDto
                {
                    Wing = wing,
                    Name = wing.ToString(),
                    Found = foundCount,
                    Total = entries.Count,
                    IsComplete = isComplete,
                    SetBonusDescription = bonus?.Description,
                    SetBonusActive = isComplete && bonus is not null,
                    Entries = entries,
                });
            }

            dto.TotalFound = dto.Wings.Sum(w => w.Found);
            dto.TotalEntries = dto.Wings.Sum(w => w.Total);

            return dto;
        }

        public async Task<List<MuseumWing>> GetCompletedWings(int playerId, CancellationToken ct)
        {
            var definitions = await db.MuseumEntryDefinitions
                .Select(d => new { d.Wing, d.Key })
                .ToListAsync(ct);

            var found = (await db.PlayerMuseumEntries
                    .Where(e => e.PlayerId == playerId)
                    .Select(e => e.EntryKey)
                    .ToListAsync(ct))
                .ToHashSet();

            return definitions
                .GroupBy(d => d.Wing)
                .Where(g => g.Any() && g.All(d => found.Contains(d.Key)))
                .Select(g => g.Key)
                .ToList();
        }

        public async Task<double> GetSetBonusTotal(
            int playerId, ItemModifier modifier, CancellationToken ct)
        {
            var completed = await GetCompletedWings(playerId, ct);

            return MuseumSetBonus.TotalFor(modifier, completed);
        }

        public async Task<MuseumAcquisitionDto?> RecordFind(
            int playerId, string entryKey, int quantity, int? poiId, string? placeName, CancellationToken ct)
        {
            if (quantity <= 0) return null;

            var definition = await db.MuseumEntryDefinitions
                .FirstOrDefaultAsync(d => d.Key == entryKey, ct);

            // A find with no plinth is not an error — Cartography entries are created on
            // discovery, and a material added before its definition is seeded should not
            // throw on a hot path.
            if (definition is null) return null;

            var existing = await db.PlayerMuseumEntries
                .FirstOrDefaultAsync(e => e.PlayerId == playerId && e.EntryKey == entryKey, ct);

            if (existing is not null)
            {
                // First-find is what counts (§5A.3), so the date and place are never
                // overwritten — the diary keeps the original.
                existing.Quantity += quantity;
                await db.SaveChangesAsync(ct);
                return null;
            }

            db.PlayerMuseumEntries.Add(new PlayerMuseumEntry
            {
                PlayerId = playerId,
                EntryKey = entryKey,
                FirstAcquiredUtc = DateTime.UtcNow,
                Quantity = quantity,
                AcquiredAtPoiId = poiId,
                AcquiredAtName = placeName,
            });

            await db.SaveChangesAsync(ct);

            var wingTotal = await db.MuseumEntryDefinitions
                .CountAsync(d => d.Wing == definition.Wing, ct);

            var wingKeys = await db.MuseumEntryDefinitions
                .Where(d => d.Wing == definition.Wing)
                .Select(d => d.Key)
                .ToListAsync(ct);

            var wingFound = await db.PlayerMuseumEntries
                .CountAsync(e => e.PlayerId == playerId && wingKeys.Contains(e.EntryKey), ct);

            return new MuseumAcquisitionDto
            {
                Key = definition.Key,
                Name = definition.Name,
                Wing = definition.Wing,
                Rarity = definition.Rarity,
                AcquiredAtName = placeName,
                CompletedWing = wingTotal > 0 && wingFound == wingTotal,
            };
        }

        public async Task<MuseumAcquisitionDto?> RecordRegion(
            int playerId, double latitude, double longitude, CancellationToken ct)
        {
            var cellLat = PoiRegionResolver.SnapToGrid(latitude);
            var cellLng = PoiRegionResolver.SnapToGrid(longitude);

            // The cache is shared across players, so a region is resolved at most once ever.
            // This is what makes criterion 8 hold and keeps the lookup policy-safe.
            var cached = await db.GeoRegions
                .FirstOrDefaultAsync(r => r.CellLat == cellLat && r.CellLng == cellLng, ct);

            if (cached is null)
            {
                var resolved = await regions.Resolve(latitude, longitude, ct);

                // Unresolvable is fine — no plinth beats an invented place name.
                if (resolved is null) return null;

                cached = new GeoRegion
                {
                    CellLat = cellLat,
                    CellLng = cellLng,
                    RegionKey = resolved.RegionKey,
                    Name = resolved.Name,
                    Kind = resolved.Kind,
                    ResolvedAtUtc = DateTime.UtcNow,
                };

                db.GeoRegions.Add(cached);

                try
                {
                    await db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException)
                {
                    // Another request cached it first; take theirs.
                    db.ChangeTracker.Clear();

                    cached = await db.GeoRegions
                        .FirstOrDefaultAsync(r => r.CellLat == cellLat && r.CellLng == cellLng, ct);

                    if (cached is null) return null;
                }
            }

            // Cartography plinths are created on discovery: the set of regions is unbounded
            // and player-specific, so seeding every possible one is not meaningful.
            var definition = await db.MuseumEntryDefinitions
                .FirstOrDefaultAsync(d => d.Key == cached.RegionKey, ct);

            if (definition is null)
            {
                definition = new MuseumEntryDefinition
                {
                    Key = cached.RegionKey,
                    Wing = MuseumWing.Cartography,
                    Name = cached.Name,
                    Description = $"You have been to {cached.Name}.",
                    Rarity = MuseumRarity.Uncommon,
                    UnlockCondition = $"Visit {cached.Name}",
                    SortOrder = 0,
                };

                db.MuseumEntryDefinitions.Add(definition);

                try
                {
                    await db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException)
                {
                    db.ChangeTracker.Clear();
                }
            }

            return await RecordFind(playerId, cached.RegionKey, 1, null, cached.Name, ct);
        }

        public async Task<List<MuseumAcquisitionDto>> RecordCellFinds(
            int playerId,
            IReadOnlyCollection<TerrainType> terrains,
            IReadOnlyCollection<string> materialKeys,
            CancellationToken ct)
        {
            var acquisitions = new List<MuseumAcquisitionDto>();

            foreach (var terrain in terrains.Distinct().Where(t => t != TerrainType.Open))
            {
                var found = await RecordFind(
                    playerId, MuseumSeedData.TerrainKey(terrain), 1, null, null, ct);

                if (found is not null) acquisitions.Add(found);
            }

            foreach (var key in materialKeys.Distinct())
            {
                var found = await RecordFind(
                    playerId, MuseumSeedData.MaterialKey(key), 1, null, null, ct);

                if (found is not null) acquisitions.Add(found);
            }

            return acquisitions;
        }

        public async Task<List<MuseumAcquisitionDto>> CheckFeats(int playerId, CancellationToken ct)
        {
            var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct);
            if (player is null) return [];

            // Every counter a Feat can test, read once. Derived from existing tables — no
            // Feat needs a column of its own (§5A.2).
            var counters = new Dictionary<string, int>
            {
                ["RevealedCells"] = await db.RevealedCells.CountAsync(r => r.PlayerId == playerId, ct),
                ["PoisVisited"] = await db.PlayerPoiVisits.CountAsync(v => v.PlayerId == playerId, ct),
                ["AdventurerLevel"] = player.AdventurerLevel,
                ["Claims"] = await db.Claims.CountAsync(c => c.PlayerId == playerId, ct),
                ["CraftsCompleted"] = await db.PlayerCrafts.CountAsync(c => c.PlayerId == playerId && c.Collected, ct),
                ["AnySkillLevel"] = await db.PlayerSkills
                    .Where(s => s.PlayerId == playerId)
                    .Select(s => (int?)s.Level)
                    .MaxAsync(ct) ?? 0,
            };

            var already = (await db.PlayerMuseumEntries
                    .Where(e => e.PlayerId == playerId && e.EntryKey.StartsWith(MuseumSeedData.Keys.Feat))
                    .Select(e => e.EntryKey)
                    .ToListAsync(ct))
                .ToHashSet();

            var acquisitions = new List<MuseumAcquisitionDto>();

            foreach (var feat in MuseumSeedData.Feats)
            {
                var key = MuseumSeedData.FeatKey(feat.Key);

                if (already.Contains(key)) continue;
                if (!counters.TryGetValue(feat.Condition, out var value)) continue;
                if (value < feat.Threshold) continue;

                var found = await RecordFind(playerId, key, 1, null, null, ct);
                if (found is not null) acquisitions.Add(found);
            }

            return acquisitions;
        }

        public async Task<DonationResultDto> DonateDuplicates(
            int playerId, string entryKey, int quantity, CancellationToken ct)
        {
            if (quantity <= 0) throw new BadRequestException("Nothing to donate.");

            var entry = await db.PlayerMuseumEntries
                .FirstOrDefaultAsync(e => e.PlayerId == playerId && e.EntryKey == entryKey, ct)
                ?? throw new NotFoundException("You have not found that yet.");

            var definition = await db.MuseumEntryDefinitions
                .FirstOrDefaultAsync(d => d.Key == entryKey, ct)
                ?? throw new NotFoundException($"No Museum entry '{entryKey}'.");

            // One is always kept on the plinth. Donating the display piece would make the
            // Museum a shop, and §5A is explicit that entries are permanent.
            var available = entry.Quantity - entry.DonatedQuantity - 1;

            if (available < quantity)
                throw new BadRequestException(
                    $"Only {Math.Max(0, available)} spare{(available == 1 ? "" : "s")} to donate.");

            var player = await db.Players.FirstAsync(p => p.Id == playerId, ct);

            var earned = (long)quantity * CurationFor(definition.Rarity);

            entry.DonatedQuantity += quantity;
            player.Curation += earned;

            await db.SaveChangesAsync(ct);

            return new DonationResultDto
            {
                Key = entryKey,
                Donated = quantity,
                CurationEarned = earned,
                TotalCuration = player.Curation,
            };
        }
    }
}
