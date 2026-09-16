using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.DTOs.Crafting.Responses;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Exceptions;
using GeoSlayer.Domain.Interfaces.Api;
using GeoSlayer.Domain.Services.Progression;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Domain.Services.Crafting;

/// <summary>
/// Crafting, gear and buildings (Stage 06, DESIGN.md §4.2–4.3).
/// </summary>
public class CraftingService(
    AppDbContext db,
    IProgressionService progression,
    IMaterialService materials) : ICraftingService
{
    /// <summary>Crafts runnable at once before the Craft Slot upgrade.</summary>
    private const int BaseQueueLimit = 1;

    // ── Recipes (§4.2) ──────────────────────────────────────────────

    public async Task<RecipeListDto> GetRecipes(int playerId, CancellationToken ct)
    {
        var recipes = await db.Recipes
            .Include(r => r.Inputs).ThenInclude(i => i.Material)
            .Include(r => r.OutputMaterial)
            .Include(r => r.OutputItem)
            .OrderBy(r => r.LevelRequired)
            .ThenBy(r => r.Name)
            .ToListAsync(ct);

        var skills = await db.PlayerSkills
            .Where(s => s.PlayerId == playerId)
            .ToDictionaryAsync(s => s.SkillType, s => s.Level, ct);

        var held = await db.PlayerMaterials
            .Where(pm => pm.PlayerId == playerId)
            .ToDictionaryAsync(pm => pm.MaterialId, pm => pm.Quantity, ct);

        var queued = await db.PlayerCrafts
            .CountAsync(c => c.PlayerId == playerId && !c.Collected, ct);

        return new RecipeListDto
        {
            QueuedCount = queued,
            QueueLimit = await QueueLimit(playerId, ct),
            Recipes = recipes.Select(r => ToDto(r, skills, held)).ToList(),
        };
    }

    private static RecipeDto ToDto(
        Recipe recipe,
        Dictionary<SkillType, int> skills,
        Dictionary<int, long> held)
    {
        var inputs = recipe.Inputs.Select(i => new RecipeInputDto
        {
            MaterialId = i.MaterialId,
            Key = i.Material.Key,
            Name = i.Material.Name,
            Quantity = i.Quantity,
            Held = held.GetValueOrDefault(i.MaterialId),
            HasEnough = held.GetValueOrDefault(i.MaterialId) >= i.Quantity,

            // A unique material comes only from POI visits, never from walking a cell.
            IsTravelGated = i.Material.IsUnique,
        }).ToList();

        var dto = new RecipeDto
        {
            Key = recipe.Key,
            Name = recipe.Name,
            Description = recipe.Description,
            SkillType = recipe.SkillType,
            SkillName = recipe.SkillType.ToString(),
            LevelRequired = recipe.LevelRequired,
            DurationSeconds = recipe.DurationSeconds,
            XpReward = recipe.XpReward,
            OutputName = recipe.OutputItem?.Name ?? recipe.OutputMaterial?.Name,
            OutputQuantity = recipe.OutputQuantity,
            OutputKind = recipe.OutputItem?.Kind,
            Inputs = inputs,
        };

        // The double gate (§4.2): unlocked on the ladder, then at level.
        if (!skills.TryGetValue(recipe.SkillType, out var level))
        {
            dto.LockReason = RecipeLockReason.SkillLocked;
            dto.LockText = $"{recipe.SkillType} is not unlocked yet";
        }
        else if (level < recipe.LevelRequired)
        {
            dto.LockReason = RecipeLockReason.LevelLocked;
            dto.LockText = $"Needs {recipe.SkillType} level {recipe.LevelRequired}";
        }
        else
        {
            var missing = inputs.Where(i => !i.HasEnough).ToList();

            // Travel-gated takes priority over plain shortage: it is a different kind of
            // "no" and the player should not be told to gather something they cannot.
            if (missing.Any(i => i.IsTravelGated))
            {
                var names = missing.Where(i => i.IsTravelGated).Select(i => i.Name);

                dto.LockReason = RecipeLockReason.TravelGated;
                dto.LockText = $"Needs {string.Join(", ", names)} — found only at rare places";
            }
            else if (missing.Count > 0)
            {
                dto.LockReason = RecipeLockReason.MissingMaterials;
                dto.LockText = "Not enough materials";
            }
            else
            {
                dto.LockReason = RecipeLockReason.None;
                dto.CanCraft = true;
            }
        }

        return dto;
    }

    private async Task<int> QueueLimit(int playerId, CancellationToken ct)
    {
        var bonus = await progression.GetUpgradeEffect(
            playerId, ProgressionDefaults.UpgradeKeys.CraftSlot, ct);

        return BaseQueueLimit + (int)bonus;
    }

    // ── The queue (task 2) ──────────────────────────────────────────

    public async Task<CraftDto> QueueCraft(int playerId, string recipeKey, CancellationToken ct)
    {
        var recipe = await db.Recipes
            .Include(r => r.Inputs).ThenInclude(i => i.Material)
            .FirstOrDefaultAsync(r => r.Key == recipeKey, ct)
            ?? throw new NotFoundException($"Recipe '{recipeKey}' not found.");

        var skill = await db.PlayerSkills
            .FirstOrDefaultAsync(s => s.PlayerId == playerId && s.SkillType == recipe.SkillType, ct);

        if (skill is null)
            throw new BadRequestException($"{recipe.SkillType} is not unlocked yet.");

        if (skill.Level < recipe.LevelRequired)
            throw new BadRequestException(
                $"{recipe.Name} needs {recipe.SkillType} level {recipe.LevelRequired}.");

        var running = await db.PlayerCrafts
            .CountAsync(c => c.PlayerId == playerId && !c.Collected, ct);

        var limit = await QueueLimit(playerId, ct);

        if (running >= limit)
            throw new BadRequestException(
                $"All {limit} craft slot{(limit == 1 ? "" : "s")} are busy — buy another with Bonus Points.");

        // Inputs are consumed at queue time, not completion (task 2): otherwise a player
        // could queue everything, spend the materials elsewhere, and still collect.
        var rows = await db.PlayerMaterials
            .Where(pm => pm.PlayerId == playerId)
            .ToDictionaryAsync(pm => pm.MaterialId, ct);

        foreach (var input in recipe.Inputs)
        {
            var row = rows.GetValueOrDefault(input.MaterialId);

            if (row is null || row.Quantity < input.Quantity)
                throw new BadRequestException(
                    $"Not enough {input.Material.Name} — need {input.Quantity}, have {row?.Quantity ?? 0}.");
        }

        foreach (var input in recipe.Inputs)
            rows[input.MaterialId].Quantity -= input.Quantity;

        var now = DateTime.UtcNow;

        var craft = new PlayerCraft
        {
            PlayerId = playerId,
            RecipeId = recipe.Id,
            RecipeKey = recipe.Key,
            StartedUtc = now,
            CompletesUtc = now.AddSeconds(recipe.DurationSeconds),
            Collected = false,
        };

        db.PlayerCrafts.Add(craft);
        await db.SaveChangesAsync(ct);

        return ToDto(craft, recipe.Name, now);
    }

    public async Task CancelCraft(int playerId, int craftId, CancellationToken ct)
    {
        var craft = await db.PlayerCrafts
            .Include(c => c.Recipe).ThenInclude(r => r.Inputs)
            .FirstOrDefaultAsync(c => c.Id == craftId && c.PlayerId == playerId, ct)
            ?? throw new NotFoundException($"Craft {craftId} not found.");

        if (craft.Collected)
            throw new BadRequestException("That craft has already been collected.");

        // Full refund. A partial one would make cancelling a trap, and the player has
        // gained nothing from a craft they stopped.
        var refund = craft.Recipe.Inputs.ToDictionary(i => i.MaterialId, i => i.Quantity);

        await materials.GrantMaterials(playerId, refund, ct);

        db.PlayerCrafts.Remove(craft);
        await db.SaveChangesAsync(ct);
    }

    public async Task<CraftCollectionDto> CollectCompletedCrafts(int playerId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Lazy evaluation on sync, exactly like worker accrual (§5.3) — no recurring job.
        var completed = await db.PlayerCrafts
            .Include(c => c.Recipe).ThenInclude(r => r.OutputItem)
            .Where(c => c.PlayerId == playerId && !c.Collected && c.CompletesUtc <= now)
            .ToListAsync(ct);

        var result = new CraftCollectionDto();

        if (completed.Count == 0) return result;

        var materialOutputs = new Dictionary<int, int>();

        foreach (var craft in completed)
        {
            craft.Collected = true;
            result.CompletedRecipes.Add(craft.Recipe.Name);

            if (craft.Recipe.OutputMaterialId is int materialId)
            {
                materialOutputs[materialId] =
                    materialOutputs.GetValueOrDefault(materialId) + craft.Recipe.OutputQuantity;
            }

            if (craft.Recipe.OutputItemId is int itemId)
            {
                var existing = await db.PlayerItems
                    .FirstOrDefaultAsync(pi => pi.PlayerId == playerId && pi.ItemId == itemId, ct);

                if (existing is null)
                {
                    db.PlayerItems.Add(new PlayerItem
                    {
                        PlayerId = playerId,
                        ItemId = itemId,
                        Quantity = craft.Recipe.OutputQuantity,
                        AcquiredUtc = now,
                    });
                }
                else
                {
                    existing.Quantity += craft.Recipe.OutputQuantity;
                }
            }
        }

        await db.SaveChangesAsync(ct);

        if (materialOutputs.Count > 0)
            result.Materials = await materials.GrantMaterials(playerId, materialOutputs, ct);

        // XP through IProgressionService, so the Adventurer cut and unlocks apply.
        foreach (var group in completed.GroupBy(c => c.Recipe.SkillType))
        {
            var xp = (long)Math.Floor(group.Sum(c => c.Recipe.XpReward));
            if (xp <= 0) continue;

            var grant = await progression.GrantXp(playerId, group.Key, xp, XpSource.Craft, ct);

            result.SkillXpEarned += grant.SkillXpEarned;
            result.AdventurerXpEarned += grant.AdventurerXpEarned;
        }

        // Completing a craft is a milestone (§3.0b).
        var milestone = await progression.GrantMilestone(
            playerId, MilestoneType.CraftComplete, completed.Count, ct);

        result.AdventurerXpEarned += milestone.AdventurerXpEarned;

        result.Items = await GetItems(playerId, ct);
        result.HasCollection = true;

        return result;
    }

    public async Task<List<CraftDto>> GetQueue(int playerId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var crafts = await db.PlayerCrafts
            .Include(c => c.Recipe)
            .Where(c => c.PlayerId == playerId && !c.Collected)
            .OrderBy(c => c.CompletesUtc)
            .ToListAsync(ct);

        return crafts.Select(c => ToDto(c, c.Recipe.Name, now)).ToList();
    }

    private static CraftDto ToDto(PlayerCraft craft, string recipeName, DateTime now) => new()
    {
        Id = craft.Id,
        RecipeKey = craft.RecipeKey,
        RecipeName = recipeName,
        StartedUtc = craft.StartedUtc,
        CompletesUtc = craft.CompletesUtc,
        IsComplete = craft.CompletesUtc <= now,
        SecondsRemaining = Math.Max(0, (craft.CompletesUtc - now).TotalSeconds),
    };

    // ── Items, gear and buildings (§4.3) ────────────────────────────

    public async Task<List<PlayerItemDto>> GetItems(int playerId, CancellationToken ct)
    {
        var items = await db.PlayerItems
            .Include(pi => pi.Item)
            .Include(pi => pi.Claim)
            .Where(pi => pi.PlayerId == playerId && pi.Quantity > 0)
            .OrderBy(pi => pi.Item.Kind)
            .ThenBy(pi => pi.Item.Tier)
            .ToListAsync(ct);

        return items.Select(ToDto).ToList();
    }

    private static PlayerItemDto ToDto(PlayerItem playerItem) => new()
    {
        Id = playerItem.Id,
        ItemId = playerItem.ItemId,
        Key = playerItem.Item.Key,
        Name = playerItem.Item.Name,
        Description = playerItem.Item.Description,
        Kind = playerItem.Item.Kind,
        Slot = playerItem.Item.Slot,
        Modifier = playerItem.Item.Modifier,
        ModifierValue = playerItem.Item.ModifierValue,
        ModifierText = ModifierText(playerItem.Item.Modifier, playerItem.Item.ModifierValue),
        Tier = playerItem.Item.Tier,
        Quantity = playerItem.Quantity,
        IsEquipped = playerItem.IsEquipped,
        ClaimId = playerItem.ClaimId,
        ClaimName = playerItem.Claim?.Name,
    };

    /// <summary>The modifier as text. The unit depends on which modifier it is.</summary>
    public static string ModifierText(ItemModifier modifier, double value) => modifier switch
    {
        ItemModifier.SkillXpPercent => $"+{Math.Round(value * 100)}% skill XP",
        ItemModifier.RevealRadius => $"+{value:0.##} cell reveal radius",
        ItemModifier.PoiRangeMetres => $"+{value:0.##}m POI range",
        ItemModifier.OfflineCapHours => $"+{value:0.##}h offline cap",
        ItemModifier.StackCapPercent => $"+{Math.Round(value * 100)}% stack caps",
        ItemModifier.WorkerRatePercent => $"+{Math.Round(value * 100)}% worker output",
        ItemModifier.ToolTier => $"Gathers up to tier {value:0}",
        _ => $"+{value:0.##}",
    };

    public async Task<List<PlayerItemDto>> SetEquipped(
        int playerId, int playerItemId, bool equipped, int? claimId, CancellationToken ct)
    {
        var playerItem = await db.PlayerItems
            .Include(pi => pi.Item)
            .FirstOrDefaultAsync(pi => pi.Id == playerItemId && pi.PlayerId == playerId, ct)
            ?? throw new NotFoundException($"Item {playerItemId} not found.");

        if (equipped)
        {
            if (playerItem.Item.Kind == ItemKind.Building)
            {
                if (claimId is null)
                    throw new BadRequestException("A building must be placed on a Claim.");

                var owns = await db.Claims.AnyAsync(c => c.Id == claimId && c.PlayerId == playerId, ct);

                if (!owns) throw new BadRequestException("That Claim is not yours.");

                playerItem.ClaimId = claimId;
            }
            else
            {
                // One item per slot. Slot competition is what keeps gear conditional
                // rather than merely additive (§4.3).
                var slot = playerItem.Item.Slot;

                if (slot != ItemSlot.None)
                {
                    var occupying = await db.PlayerItems
                        .Include(pi => pi.Item)
                        .Where(pi => pi.PlayerId == playerId
                                  && pi.IsEquipped
                                  && pi.Id != playerItem.Id
                                  && pi.Item.Slot == slot)
                        .ToListAsync(ct);

                    foreach (var other in occupying)
                        other.IsEquipped = false;
                }
            }
        }
        else
        {
            playerItem.ClaimId = null;
        }

        playerItem.IsEquipped = equipped;

        await db.SaveChangesAsync(ct);

        return await GetItems(playerId, ct);
    }

    public async Task<double> GetModifierTotal(
        int playerId, ItemModifier modifier, CancellationToken ct)
    {
        // Secondary modifiers count as well (Stage 11): a tool carries both its tier gate
        // and its speed bonus, and ignoring the second would make the speed invisible.
        var equipped = await db.PlayerItems
            .Include(pi => pi.Item)
            .Where(pi => pi.PlayerId == playerId && pi.IsEquipped && pi.Quantity > 0)
            .Select(pi => new
            {
                pi.Item.Modifier,
                pi.Item.ModifierValue,
                pi.Item.SecondaryModifier,
                pi.Item.SecondaryModifierValue,
            })
            .ToListAsync(ct);

        return equipped.Sum(i =>
            (i.Modifier == modifier ? i.ModifierValue : 0)
            + (i.SecondaryModifier == modifier ? i.SecondaryModifierValue : 0));
    }
}
