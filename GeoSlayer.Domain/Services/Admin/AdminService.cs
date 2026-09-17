using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.DTOs.Admin.Requests;
using GeoSlayer.Domain.DTOs.Admin.Responses;
using GeoSlayer.Domain.Exceptions;
using GeoSlayer.Domain.Interfaces.Api.Admin;
using GeoSlayer.Domain.Services.Crafting;
using GeoSlayer.Domain.Services.Economy;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Domain.Services.Admin
{
    /// <summary>
    /// Game management (Stage 18).
    ///
    /// <para>Almost every number in this game is seeded data on purpose — DESIGN.md says so
    /// repeatedly, "so balance can be retuned without a deploy". Until now that promise was
    /// half kept: the data lived in JSON and C# seed classes, so retuning meant editing
    /// source and redeploying anyway. This service is what makes it true.</para>
    ///
    /// <para><b>Every mutation writes an audit entry.</b> See <see cref="AdminAuditEntry"/>
    /// for why.</para>
    /// </summary>
    public class AdminService(AppDbContext db, IGameSettings gameSettings) : IAdminService
    {
        // ── Items (task 4) ──────────────────────────────────────────────

        public async Task<List<AdminItemDto>> GetItems(CancellationToken ct)
        {
            var items = await db.Items.OrderBy(i => i.Tier).ThenBy(i => i.Name).ToListAsync(ct);

            // One query for which items have an image, rather than loading the blobs — the
            // whole reason ItemImage is its own table.
            var withImages = await db.ItemImages.Select(i => i.ItemId).ToListAsync(ct);
            var imageSet = withImages.ToHashSet();

            return [.. items.Select(item => ToDto(item, imageSet.Contains(item.Id)))];
        }

        private static AdminItemDto ToDto(Item item, bool hasImage) => new()
        {
            Id = item.Id,
            Key = item.Key,
            Name = item.Name,
            Description = item.Description,
            Kind = item.Kind,
            Slot = item.Slot,
            Modifier = item.Modifier,
            ModifierValue = item.ModifierValue,
            SecondaryModifier = item.SecondaryModifier,
            SecondaryModifierValue = item.SecondaryModifierValue,
            Tier = item.Tier,

            // The same text the app shows, so an admin sees what a player will see rather
            // than a second description that can drift from it.
            ModifierText = CraftingService.ModifierText(item.Modifier, item.ModifierValue),

            HasImage = hasImage,
            ModifierIsRead = ModifierReaders.IsRead(item.Modifier),
        };

        public async Task<AdminItemDto> SaveItem(
            string adminUserId, SaveItemRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Key))
            {
                throw new BadRequestException("An item needs a key.");
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new BadRequestException("An item needs a name.");
            }

            // Refused rather than silently allowed: §4.3 calls an item that changes no
            // behaviour a bug, and the admin interface should not be the way one gets
            // created. The message names the rule so the refusal is actionable.
            if (!ModifierReaders.IsRead(request.Modifier))
            {
                throw new BadRequestException(
                    $"Nothing in the game reads {request.Modifier}, so an item using it would " +
                    "do nothing (DESIGN.md §4.3). Wire up a reader first.");
            }

            var isNew = request.Id is null;

            var item = isNew
                ? new Item { Key = request.Key, Name = request.Name, Description = request.Description }
                : await db.Items.FirstOrDefaultAsync(i => i.Id == request.Id, ct)
                  ?? throw new NotFoundException($"Item {request.Id} not found.");

            // Keys are the identity every recipe and seed file references, so a collision
            // would silently repoint a recipe at the wrong item.
            var keyTaken = await db.Items
                .AnyAsync(i => i.Key == request.Key && i.Id != (request.Id ?? 0), ct);

            if (keyTaken)
            {
                throw new BadRequestException($"Another item already uses the key '{request.Key}'.");
            }

            item.Key = request.Key;
            item.Name = request.Name;
            item.Description = request.Description;
            item.Kind = request.Kind;
            item.Slot = request.Slot;
            item.Modifier = request.Modifier;
            item.ModifierValue = request.ModifierValue;
            item.SecondaryModifier = request.SecondaryModifier;
            item.SecondaryModifierValue = request.SecondaryModifierValue;
            item.Tier = request.Tier;

            if (isNew)
            {
                db.Items.Add(item);
            }

            await db.SaveChangesAsync(ct);

            await Audit(adminUserId, "Item", item.Id.ToString(), isNew ? "Created" : "Updated",
                $"{item.Key}: {CraftingService.ModifierText(item.Modifier, item.ModifierValue)}", ct);

            var hasImage = await db.ItemImages.AnyAsync(i => i.ItemId == item.Id, ct);

            return ToDto(item, hasImage);
        }

        public async Task DeleteItem(string adminUserId, int itemId, CancellationToken ct)
        {
            var item = await db.Items.FirstOrDefaultAsync(i => i.Id == itemId, ct)
                ?? throw new NotFoundException($"Item {itemId} not found.");

            // Refused rather than cascaded. Deleting an item a recipe produces would leave
            // that recipe pointing at nothing, and the failure would surface as a crash in
            // the crafting screen rather than here where it can be explained.
            var usedByRecipe = await db.Recipes.AnyAsync(r => r.OutputItemId == itemId, ct);

            if (usedByRecipe)
            {
                throw new BadRequestException(
                    $"'{item.Key}' is produced by a recipe. Delete or repoint the recipe first.");
            }

            var heldBy = await db.PlayerItems.CountAsync(pi => pi.ItemId == itemId, ct);

            db.Items.Remove(item);
            await db.SaveChangesAsync(ct);

            // The count is recorded because this is the destructive case: players lost
            // something, and six months later someone will ask what.
            await Audit(adminUserId, "Item", itemId.ToString(), "Deleted",
                $"{item.Key} ({item.Name}); removed from {heldBy} player inventor{(heldBy == 1 ? "y" : "ies")}", ct);
        }

        // ── Images (task 3) ─────────────────────────────────────────────

        public async Task UploadItemImage(
            string adminUserId, int itemId, byte[] data, string? contentType, string? fileName,
            CancellationToken ct)
        {
            var item = await db.Items.FirstOrDefaultAsync(i => i.Id == itemId, ct)
                ?? throw new NotFoundException($"Item {itemId} not found.");

            // Validated before anything is stored. An admin tool is still an upload path.
            var rejection = ImageValidation.Reject(contentType, data);

            if (rejection is not null)
            {
                throw new BadRequestException(rejection);
            }

            var existing = await db.ItemImages.FirstOrDefaultAsync(i => i.ItemId == itemId, ct);
            var replacing = existing is not null;

            var image = existing ?? new ItemImage { ItemId = itemId };

            image.Data = data;
            image.ContentType = contentType!;
            image.FileName = ImageValidation.SafeFileName(fileName, contentType!);
            image.SizeBytes = data.Length;
            image.UploadedUtc = DateTime.UtcNow;
            image.UploadedByUserId = adminUserId;

            if (!replacing)
            {
                db.ItemImages.Add(image);
            }

            await db.SaveChangesAsync(ct);

            await Audit(adminUserId, "Item", itemId.ToString(),
                replacing ? "ImageReplaced" : "ImageUploaded",
                $"{item.Key}: {image.FileName}, {data.Length / 1024}KB", ct);
        }

        public async Task DeleteItemImage(string adminUserId, int itemId, CancellationToken ct)
        {
            var image = await db.ItemImages.FirstOrDefaultAsync(i => i.ItemId == itemId, ct)
                ?? throw new NotFoundException($"Item {itemId} has no image.");

            db.ItemImages.Remove(image);
            await db.SaveChangesAsync(ct);

            await Audit(adminUserId, "Item", itemId.ToString(), "ImageDeleted", image.FileName, ct);
        }

        public async Task<(byte[] Data, string ContentType)?> GetItemImage(int itemId, CancellationToken ct)
        {
            var image = await db.ItemImages
                .Where(i => i.ItemId == itemId)
                .Select(i => new { i.Data, i.ContentType })
                .FirstOrDefaultAsync(ct);

            return image is null ? null : (image.Data, image.ContentType);
        }

        // ── Materials (task 6) ──────────────────────────────────────────

        public async Task<List<AdminMaterialDto>> GetMaterials(CancellationToken ct)
        {
            var materials = await db.Materials
                .OrderBy(m => m.Category)
                .ThenBy(m => m.Tier)
                .ToListAsync(ct);

            return [.. materials.Select(ToDto)];
        }

        private static AdminMaterialDto ToDto(Material material) => new()
        {
            Id = material.Id,
            Key = material.Key,
            Name = material.Name,
            Category = material.Category,
            SkillType = material.SkillType,
            Tier = material.Tier,
            LevelRequired = material.LevelRequired,
            BaseGatherSeconds = material.BaseGatherSeconds,
            XpPerUnit = material.XpPerUnit,
            IsUnique = material.IsUnique,

            // Derived rather than stored (§5D.1), so it is shown as a consequence of the
            // tier and category rather than as something to edit.
            UnitPrice = CoinPricing.UnitPrice(material.Tier, material.Category),

            // The number §4.1a actually constrains — XP/hour must not fall as tiers rise.
            XpPerSecond = material.BaseGatherSeconds > 0
                ? Math.Round(material.XpPerUnit / material.BaseGatherSeconds, 4)
                : 0,
        };

        public async Task<AdminMaterialDto> SaveMaterial(
            string adminUserId, SaveMaterialRequest request, CancellationToken ct)
        {
            var isNew = request.Id is null;

            var material = isNew
                ? new Material { Key = request.Key, Name = request.Name }
                : await db.Materials.FirstOrDefaultAsync(m => m.Id == request.Id, ct)
                  ?? throw new NotFoundException($"Material {request.Id} not found.");

            material.Key = request.Key;
            material.Name = request.Name;
            material.Category = request.Category;
            material.SkillType = request.SkillType;
            material.Tier = request.Tier;
            material.LevelRequired = request.LevelRequired;
            material.BaseGatherSeconds = request.BaseGatherSeconds;
            material.XpPerUnit = request.XpPerUnit;
            material.IsUnique = request.IsUnique;

            // Validated against every *other* material, so editing one in place does not
            // trip the uniqueness and tier rules against itself.
            var others = await db.Materials
                .Where(m => m.Id != (request.Id ?? 0))
                .AsNoTracking()
                .ToListAsync(ct);

            var rejection = MaterialValidation.Reject(material, others);

            if (rejection is not null)
            {
                // Cleared before throwing: the entity was mutated in place above, and
                // leaving it tracked would let the bad values reach the database on the
                // next unrelated SaveChanges.
                db.ChangeTracker.Clear();

                throw new BadRequestException(rejection);
            }

            if (isNew)
            {
                db.Materials.Add(material);
            }

            await db.SaveChangesAsync(ct);

            await Audit(adminUserId, "Material", material.Id.ToString(),
                isNew ? "Created" : "Updated",
                $"{material.Key}: {material.Category} tier {material.Tier}, " +
                $"level {material.LevelRequired}, {material.BaseGatherSeconds}s", ct);

            return ToDto(material);
        }

        public async Task DeleteMaterial(string adminUserId, int materialId, CancellationToken ct)
        {
            var material = await db.Materials.FirstOrDefaultAsync(m => m.Id == materialId, ct)
                ?? throw new NotFoundException($"Material {materialId} not found.");

            // Each of these would leave a dangling reference that surfaces as a crash
            // somewhere else in the game rather than here, where it can be explained.
            if (await db.RecipeInputs.AnyAsync(i => i.MaterialId == materialId, ct))
            {
                throw new BadRequestException(
                    $"'{material.Key}' is an input to a recipe. Change the recipe first.");
            }

            if (await db.Recipes.AnyAsync(r => r.OutputMaterialId == materialId, ct))
            {
                throw new BadRequestException(
                    $"'{material.Key}' is produced by a recipe. Change the recipe first.");
            }

            if (await db.DropTableEntries.AnyAsync(e => e.MaterialId == materialId, ct))
            {
                throw new BadRequestException(
                    $"'{material.Key}' is in a drop table. Remove it from the table first.");
            }

            var heldBy = await db.PlayerMaterials
                .CountAsync(pm => pm.MaterialId == materialId && pm.Quantity > 0, ct);

            db.Materials.Remove(material);
            await db.SaveChangesAsync(ct);

            await Audit(adminUserId, "Material", materialId.ToString(), "Deleted",
                $"{material.Key} ({material.Name}); held by {heldBy} player" +
                $"{(heldBy == 1 ? "" : "s")}", ct);
        }

        // ── Recipes (task 7) ────────────────────────────────────────────

        public async Task<List<AdminRecipeDto>> GetRecipes(CancellationToken ct)
        {
            var recipes = await db.Recipes
                .Include(r => r.Inputs).ThenInclude(i => i.Material)
                .Include(r => r.OutputMaterial)
                .Include(r => r.OutputItem)
                .OrderBy(r => r.SkillType)
                .ThenBy(r => r.LevelRequired)
                .ToListAsync(ct);

            return [.. recipes.Select(ToDto)];
        }

        private static AdminRecipeDto ToDto(Recipe recipe)
        {
            var inputCost = RecipeValidation.CostOf(
                recipe.Inputs.Select(i => (i.Material, i.Quantity)));

            // Only a material output has a coin value — an item's worth is its modifier,
            // which no price can express.
            var outputValue = recipe.OutputMaterial is null
                ? 0
                : CoinPricing.UnitPrice(recipe.OutputMaterial.Tier, recipe.OutputMaterial.Category)
                  * recipe.OutputQuantity;

            return new AdminRecipeDto
            {
                Id = recipe.Id,
                Key = recipe.Key,
                Name = recipe.Name,
                Description = recipe.Description,
                SkillType = recipe.SkillType,
                LevelRequired = recipe.LevelRequired,
                DurationSeconds = recipe.DurationSeconds,
                XpReward = recipe.XpReward,
                OutputMaterialId = recipe.OutputMaterialId,
                OutputItemId = recipe.OutputItemId,
                OutputQuantity = recipe.OutputQuantity,
                OutputName = recipe.OutputItem?.Name ?? recipe.OutputMaterial?.Name,

                Inputs = [.. recipe.Inputs.Select(i => new AdminRecipeInputDto
                {
                    MaterialId = i.MaterialId,
                    MaterialKey = i.Material.Key,
                    MaterialName = i.Material.Name,
                    Quantity = i.Quantity,
                })],

                InputCost = inputCost,
                OutputValue = outputValue,
                Warnings = [.. RecipeValidation.Warn(recipe, inputCost, outputValue)],
            };
        }

        public async Task<AdminRecipeDto> SaveRecipe(
            string adminUserId, SaveRecipeRequest request, CancellationToken ct)
        {
            var isNew = request.Id is null;

            var recipe = isNew
                ? new Recipe { Key = request.Key, Name = request.Name, Description = request.Description }
                : await db.Recipes
                    .Include(r => r.Inputs)
                    .FirstOrDefaultAsync(r => r.Id == request.Id, ct)
                  ?? throw new NotFoundException($"Recipe {request.Id} not found.");

            recipe.Key = request.Key;
            recipe.Name = request.Name;
            recipe.Description = request.Description;
            recipe.SkillType = request.SkillType;
            recipe.LevelRequired = request.LevelRequired;
            recipe.DurationSeconds = request.DurationSeconds;
            recipe.XpReward = request.XpReward;
            recipe.OutputMaterialId = request.OutputMaterialId;
            recipe.OutputItemId = request.OutputItemId;
            recipe.OutputQuantity = request.OutputQuantity;

            var inputs = request.Inputs
                .Select(i => new RecipeInput { MaterialId = i.MaterialId, Quantity = i.Quantity })
                .ToList();

            var others = await db.Recipes
                .Where(r => r.Id != (request.Id ?? 0))
                .AsNoTracking()
                .ToListAsync(ct);

            var rejection = RecipeValidation.Reject(recipe, inputs, others);

            if (rejection is not null)
            {
                // Same reasoning as SaveMaterial: the entity was mutated in place, so a
                // rejection has to discard it or the bad values reach the database on the
                // next unrelated save.
                db.ChangeTracker.Clear();

                throw new BadRequestException(rejection);
            }

            // Referenced ids are checked here rather than in the validator, which is pure —
            // a dangling foreign key would otherwise surface as a database error with no
            // useful message.
            await AssertOutputExists(request, ct);
            await AssertInputsExist(inputs, ct);

            if (isNew)
            {
                db.Recipes.Add(recipe);
            }
            else
            {
                // Replaced wholesale rather than diffed: the set is small, and a diff that
                // gets the removals wrong leaves phantom inputs a player still pays.
                db.RecipeInputs.RemoveRange(recipe.Inputs);
                recipe.Inputs.Clear();
            }

            foreach (var input in inputs)
            {
                recipe.Inputs.Add(input);
            }

            await db.SaveChangesAsync(ct);

            await Audit(adminUserId, "Recipe", recipe.Id.ToString(),
                isNew ? "Created" : "Updated",
                $"{recipe.Key}: {recipe.SkillType} level {recipe.LevelRequired}, " +
                $"{Math.Round(recipe.DurationSeconds)}s, {inputs.Count} input" +
                $"{(inputs.Count == 1 ? "" : "s")}", ct);

            // Reloaded so the DTO carries the material and item names the chain is read by.
            var saved = await db.Recipes
                .Include(r => r.Inputs).ThenInclude(i => i.Material)
                .Include(r => r.OutputMaterial)
                .Include(r => r.OutputItem)
                .AsNoTracking()
                .FirstAsync(r => r.Id == recipe.Id, ct);

            return ToDto(saved);
        }

        private async Task AssertOutputExists(SaveRecipeRequest request, CancellationToken ct)
        {
            if (request.OutputMaterialId is int materialId
                && !await db.Materials.AnyAsync(m => m.Id == materialId, ct))
            {
                throw new BadRequestException($"Output material {materialId} does not exist.");
            }

            if (request.OutputItemId is int itemId
                && !await db.Items.AnyAsync(i => i.Id == itemId, ct))
            {
                throw new BadRequestException($"Output item {itemId} does not exist.");
            }
        }

        private async Task AssertInputsExist(List<RecipeInput> inputs, CancellationToken ct)
        {
            var ids = inputs.Select(i => i.MaterialId).Distinct().ToList();

            var found = await db.Materials
                .Where(m => ids.Contains(m.Id))
                .Select(m => m.Id)
                .ToListAsync(ct);

            var missing = ids.Except(found).ToList();

            if (missing.Count > 0)
            {
                throw new BadRequestException(
                    $"Input material{(missing.Count == 1 ? "" : "s")} " +
                    $"{string.Join(", ", missing)} do not exist.");
            }
        }

        public async Task DeleteRecipe(string adminUserId, int recipeId, CancellationToken ct)
        {
            var recipe = await db.Recipes
                .Include(r => r.Inputs)
                .FirstOrDefaultAsync(r => r.Id == recipeId, ct)
                ?? throw new NotFoundException($"Recipe {recipeId} not found.");

            // Unlike an item or a material, a recipe owns its inputs outright — nothing else
            // points at them — so cascading here is correct rather than destructive.
            var queued = await db.PlayerCrafts
                .CountAsync(c => c.RecipeId == recipeId && !c.Collected, ct);

            if (queued > 0)
            {
                throw new BadRequestException(
                    $"{queued} player craft{(queued == 1 ? " is" : "s are")} still queued on " +
                    $"'{recipe.Key}'. Let them finish, or the queue points at nothing.");
            }

            db.RecipeInputs.RemoveRange(recipe.Inputs);
            db.Recipes.Remove(recipe);
            await db.SaveChangesAsync(ct);

            await Audit(adminUserId, "Recipe", recipeId.ToString(), "Deleted",
                $"{recipe.Key} ({recipe.Name})", ct);
        }

        // ── Encounters (task 8) ─────────────────────────────────────────

        public async Task<List<AdminEncounterDto>> GetEncounters(CancellationToken ct)
        {
            var definitions = await db.EncounterDefinitions
                .OrderBy(e => e.Tier)
                .ThenBy(e => e.MinCombatLevel)
                .ToListAsync(ct);

            // Set-level, so every row carries the same list. §5C.2 constrains the whole
            // ladder — a single definition is never wrong on its own.
            var warnings = EncounterValidation.Warn(definitions).ToList();

            return [.. definitions.Select(e => ToDto(e, warnings))];
        }

        private static AdminEncounterDto ToDto(
            EncounterDefinition definition, List<string> setWarnings) => new()
            {
                Id = definition.Id,
                Key = definition.Key,
                Name = definition.Name,
                Description = definition.Description,
                Tier = definition.Tier,
                MinCombatLevel = definition.MinCombatLevel,
                IsTrainingGround = definition.IsTrainingGround,
                SetWarnings = setWarnings,
            };

        public async Task<AdminEncounterDto> SaveEncounter(
            string adminUserId, SaveEncounterRequest request, CancellationToken ct)
        {
            var isNew = request.Id is null;

            var definition = isNew
                ? new EncounterDefinition
                {
                    Key = request.Key,
                    Name = request.Name,
                    Description = request.Description,
                }
                : await db.EncounterDefinitions.FirstOrDefaultAsync(e => e.Id == request.Id, ct)
                  ?? throw new NotFoundException($"Encounter {request.Id} not found.");

            definition.Key = request.Key;
            definition.Name = request.Name;
            definition.Description = request.Description;
            definition.Tier = request.Tier;
            definition.MinCombatLevel = request.MinCombatLevel;
            definition.IsTrainingGround = request.IsTrainingGround;

            var others = await db.EncounterDefinitions
                .Where(e => e.Id != (request.Id ?? 0))
                .AsNoTracking()
                .ToListAsync(ct);

            var rejection = EncounterValidation.Reject(definition, others);

            // Checked against the set this save would *produce*, not the one that exists —
            // otherwise an edit that breaks the ladder passes because the old row still
            // covers the tier.
            rejection ??= EncounterValidation.RejectSet([.. others, definition]);

            if (rejection is not null)
            {
                // The entity was mutated in place; discard it or the bad values ride along
                // on the next unrelated SaveChanges.
                db.ChangeTracker.Clear();

                throw new BadRequestException(rejection);
            }

            if (isNew)
            {
                db.EncounterDefinitions.Add(definition);
            }

            await db.SaveChangesAsync(ct);

            await Audit(adminUserId, "Encounter", definition.Id.ToString(),
                isNew ? "Created" : "Updated",
                $"{definition.Key}: tier {definition.Tier} at Combat {definition.MinCombatLevel}" +
                $"{(definition.IsTrainingGround ? ", training ground" : ", roaming")}", ct);

            var resulting = await db.EncounterDefinitions.AsNoTracking().ToListAsync(ct);

            return ToDto(definition, [.. EncounterValidation.Warn(resulting)]);
        }

        public async Task DeleteEncounter(string adminUserId, int encounterId, CancellationToken ct)
        {
            var definition = await db.EncounterDefinitions
                .FirstOrDefaultAsync(e => e.Id == encounterId, ct)
                ?? throw new NotFoundException($"Encounter {encounterId} not found.");

            // Deletion breaks §5C.2 just as easily as an edit does — removing the only
            // roaming encounter at a tier locks every castle-less player out of it.
            var remaining = await db.EncounterDefinitions
                .Where(e => e.Id != encounterId)
                .AsNoTracking()
                .ToListAsync(ct);

            var rejection = EncounterValidation.RejectSet(remaining);

            if (rejection is not null)
            {
                throw new BadRequestException(rejection);
            }

            // Player encounters reference the definition by key rather than by id, so
            // in-flight ones do not dangle — they simply stop resolving, which
            // EncounterService already handles as a missing definition.
            var live = await db.PlayerEncounters
                .CountAsync(e => e.DefinitionKey == definition.Key && e.ResolvedUtc == null, ct);

            db.EncounterDefinitions.Remove(definition);
            await db.SaveChangesAsync(ct);

            await Audit(adminUserId, "Encounter", encounterId.ToString(), "Deleted",
                $"{definition.Key} ({definition.Name}); {live} unresolved player encounter" +
                $"{(live == 1 ? "" : "s")} referenced it", ct);
        }

        // ── Progression (task 8) ────────────────────────────────────────

        public async Task<AdminProgressionDto> GetProgression(CancellationToken ct)
        {
            var unlocks = await db.UnlockDefinitions
                .OrderBy(u => u.AdventurerLevel)
                .ThenBy(u => u.Payload)
                .ToListAsync(ct);

            var upgrades = await db.UpgradeDefinitions
                .OrderBy(u => u.Category)
                .ThenBy(u => u.Name)
                .ToListAsync(ct);

            return new AdminProgressionDto
            {
                Unlocks = [.. unlocks.Select(ToDto)],
                Upgrades = [.. upgrades.Select(ToDto)],
                LadderWarnings = [.. ProgressionValidation.WarnUnlocks(unlocks)],
            };
        }

        private static AdminUnlockDto ToDto(UnlockDefinition unlock) => new()
        {
            Id = unlock.Id,
            AdventurerLevel = unlock.AdventurerLevel,
            UnlockType = unlock.UnlockType,
            Payload = unlock.Payload,
            DisplayName = unlock.DisplayName,
        };

        private static AdminUpgradeDto ToDto(UpgradeDefinition upgrade) => new()
        {
            Id = upgrade.Id,
            Key = upgrade.Key,
            Name = upgrade.Name,
            Category = upgrade.Category,
            Description = upgrade.Description,
            MaxRank = upgrade.MaxRank,
            CostCurve = upgrade.CostCurve,
            EffectPerRank = upgrade.EffectPerRank,
            MinAdventurerLevel = upgrade.MinAdventurerLevel,

            // Summed here rather than left to the reader: the total cost of maxing an
            // upgrade is the number that actually decides whether it is worth buying, and
            // it is the one thing a curve makes hard to eyeball.
            TotalCost = SafeTotalCost(upgrade),
        };

        /// <summary>
        /// Total Bonus Points to max an upgrade, tolerating a malformed curve.
        ///
        /// <para>Validation stops new bad curves, but a row seeded or migrated before this
        /// existed could still be unparseable — and a listing that throws would make the
        /// offending row impossible to find and fix through the interface.</para>
        /// </summary>
        private static int SafeTotalCost(UpgradeDefinition upgrade)
        {
            var total = 0;

            foreach (var part in upgrade.CostCurve
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(part, out var cost))
                {
                    total += cost;
                }
            }

            return total;
        }

        public async Task<AdminUpgradeDto> SaveUpgrade(
            string adminUserId, SaveUpgradeRequest request, CancellationToken ct)
        {
            var isNew = request.Id is null;

            var upgrade = isNew
                ? new UpgradeDefinition
                {
                    Key = request.Key,
                    Name = request.Name,
                    Category = request.Category,
                    Description = request.Description,
                    CostCurve = request.CostCurve,
                }
                : await db.UpgradeDefinitions.FirstOrDefaultAsync(u => u.Id == request.Id, ct)
                  ?? throw new NotFoundException($"Upgrade {request.Id} not found.");

            upgrade.Key = request.Key;
            upgrade.Name = request.Name;
            upgrade.Category = request.Category;
            upgrade.Description = request.Description;
            upgrade.MaxRank = request.MaxRank;
            upgrade.CostCurve = request.CostCurve;
            upgrade.EffectPerRank = request.EffectPerRank;
            upgrade.MinAdventurerLevel = request.MinAdventurerLevel;

            var others = await db.UpgradeDefinitions
                .Where(u => u.Id != (request.Id ?? 0))
                .AsNoTracking()
                .ToListAsync(ct);

            var rejection = ProgressionValidation.RejectUpgrade(upgrade, others);

            if (rejection is not null)
            {
                db.ChangeTracker.Clear();

                throw new BadRequestException(rejection);
            }

            if (isNew)
            {
                db.UpgradeDefinitions.Add(upgrade);
            }

            await db.SaveChangesAsync(ct);

            await Audit(adminUserId, "Upgrade", upgrade.Id.ToString(),
                isNew ? "Created" : "Updated",
                $"{upgrade.Key}: {upgrade.MaxRank} ranks at {upgrade.CostCurve}", ct);

            return ToDto(upgrade);
        }

        public async Task DeleteUpgrade(string adminUserId, int upgradeId, CancellationToken ct)
        {
            var upgrade = await db.UpgradeDefinitions
                .FirstOrDefaultAsync(u => u.Id == upgradeId, ct)
                ?? throw new NotFoundException($"Upgrade {upgradeId} not found.");

            // Refused rather than cascaded: players spent Bonus Points on these ranks, and
            // deleting the definition would take the purchase without refunding it.
            var bought = await db.PlayerUpgrades
                .CountAsync(p => p.UpgradeKey == upgrade.Key && p.Rank > 0, ct);

            if (bought > 0)
            {
                throw new BadRequestException(
                    $"{bought} player{(bought == 1 ? " has" : "s have")} bought ranks in " +
                    $"'{upgrade.Key}'. Deleting it would take what they paid for without a refund.");
            }

            db.UpgradeDefinitions.Remove(upgrade);
            await db.SaveChangesAsync(ct);

            await Audit(adminUserId, "Upgrade", upgradeId.ToString(), "Deleted",
                $"{upgrade.Key} ({upgrade.Name})", ct);
        }

        public async Task<AdminUnlockDto> SaveUnlock(
            string adminUserId, SaveUnlockRequest request, CancellationToken ct)
        {
            var isNew = request.Id is null;

            var unlock = isNew
                ? new UnlockDefinition { Payload = request.Payload, DisplayName = request.DisplayName }
                : await db.UnlockDefinitions.FirstOrDefaultAsync(u => u.Id == request.Id, ct)
                  ?? throw new NotFoundException($"Unlock {request.Id} not found.");

            unlock.AdventurerLevel = request.AdventurerLevel;
            unlock.UnlockType = request.UnlockType;
            unlock.Payload = request.Payload;
            unlock.DisplayName = request.DisplayName;

            var others = await db.UnlockDefinitions
                .Where(u => u.Id != (request.Id ?? 0))
                .AsNoTracking()
                .ToListAsync(ct);

            var rejection = ProgressionValidation.RejectUnlock(unlock, others);

            if (rejection is not null)
            {
                db.ChangeTracker.Clear();

                throw new BadRequestException(rejection);
            }

            if (isNew)
            {
                db.UnlockDefinitions.Add(unlock);
            }

            await db.SaveChangesAsync(ct);

            await Audit(adminUserId, "Unlock", unlock.Id.ToString(),
                isNew ? "Created" : "Updated",
                $"{unlock.Payload} at Adventurer {unlock.AdventurerLevel}", ct);

            return ToDto(unlock);
        }

        public async Task DeleteUnlock(string adminUserId, int unlockId, CancellationToken ct)
        {
            var unlock = await db.UnlockDefinitions.FirstOrDefaultAsync(u => u.Id == unlockId, ct)
                ?? throw new NotFoundException($"Unlock {unlockId} not found.");

            db.UnlockDefinitions.Remove(unlock);
            await db.SaveChangesAsync(ct);

            // Not refused: players who already unlocked it keep their PlayerSkill row — the
            // row existing *is* the unlock (§3.1), so removing the rung only stops future
            // players reaching it. Worth recording how many that affects.
            var alreadyHave = await db.PlayerSkills
                .CountAsync(s => s.SkillType.ToString() == unlock.Payload, ct);

            await Audit(adminUserId, "Unlock", unlockId.ToString(), "Deleted",
                $"{unlock.Payload} at Adventurer {unlock.AdventurerLevel}; " +
                $"{alreadyHave} player{(alreadyHave == 1 ? "" : "s")} already had it", ct);
        }

        // ── Tunable numbers (Stage 18) ──────────────────────────────────

        public async Task<List<AdminGameSettingDto>> GetGameSettings(CancellationToken ct)
        {
            var settings = await db.GameSettings
                .OrderBy(g => g.Category)
                .ThenBy(g => g.Key)
                .ToListAsync(ct);

            return [.. settings.Select(ToDto)];
        }

        private static AdminGameSettingDto ToDto(GameSetting setting) => new()
        {
            Id = setting.Id,
            Key = setting.Key,
            Value = setting.Value,
            Default = setting.Default,
            Category = setting.Category,
            Description = setting.Description,
            MinValue = setting.MinValue,
            MaxValue = setting.MaxValue,

            // Compared as text rather than parsed: "0.001" and "0.0010" are the same number
            // but a different edit, and an admin who typed one should see it as changed.
            IsChanged = !string.Equals(setting.Value, setting.Default, StringComparison.Ordinal),
        };

        public async Task<AdminGameSettingDto> SaveGameSetting(
            string adminUserId, SaveGameSettingRequest request, CancellationToken ct)
        {
            var setting = await db.GameSettings.FirstOrDefaultAsync(g => g.Id == request.Id, ct)
                ?? throw new NotFoundException($"Setting {request.Id} not found.");

            if (!double.TryParse(request.Value, out var parsed))
            {
                throw new BadRequestException($"'{request.Value}' is not a number.");
            }

            // Bounds are part of the setting's definition, not advice. A tuning value with no
            // ceiling is a way to break the game from a text box — an interest rate of 10 per
            // hour, or a coin multiplier of zero that makes every material worthless.
            if (parsed < setting.MinValue || parsed > setting.MaxValue)
            {
                throw new BadRequestException(
                    $"{setting.Key} must be between {setting.MinValue} and {setting.MaxValue}.");
            }

            var previous = setting.Value;
            setting.Value = request.Value;

            await db.SaveChangesAsync(ct);

            // Reloaded immediately: a setting that needed a restart to take effect would be
            // no better than the constant it replaced.
            await gameSettings.Reload();

            await Audit(adminUserId, "GameSetting", setting.Key, "Updated",
                $"{previous} → {request.Value} (shipped default {setting.Default})", ct);

            return ToDto(setting);
        }

        // ── Museum (task 8) ─────────────────────────────────────────────

        public async Task<List<AdminMuseumEntryDto>> GetMuseumEntries(CancellationToken ct)
        {
            var entries = await db.MuseumEntryDefinitions
                .OrderBy(e => e.Wing)
                .ThenBy(e => e.SortOrder)
                .ToListAsync(ct);

            // One grouped query rather than a count per row — the Museum has hundreds of
            // entries and this listing is the whole table.
            var foundCounts = await db.PlayerMuseumEntries
                .GroupBy(e => e.EntryKey)
                .Select(g => new { Key = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

            var warnings = MuseumValidation.Warn(entries).ToList();

            return [.. entries.Select(e => ToDto(e, foundCounts.GetValueOrDefault(e.Key), warnings))];
        }

        private static AdminMuseumEntryDto ToDto(
            MuseumEntryDefinition entry, int foundBy, List<string> setWarnings) => new()
            {
                Id = entry.Id,
                Key = entry.Key,
                Name = entry.Name,
                Description = entry.Description,
                Wing = entry.Wing,
                Rarity = entry.Rarity,
                UnlockCondition = entry.UnlockCondition,
                SortOrder = entry.SortOrder,
                FoundBy = foundBy,
                SetWarnings = setWarnings,
            };

        public async Task<AdminMuseumEntryDto> SaveMuseumEntry(
            string adminUserId, SaveMuseumEntryRequest request, CancellationToken ct)
        {
            var isNew = request.Id is null;

            var entry = isNew
                ? new MuseumEntryDefinition
                {
                    Key = request.Key,
                    Name = request.Name,
                    Description = request.Description,
                    UnlockCondition = request.UnlockCondition,
                }
                : await db.MuseumEntryDefinitions.FirstOrDefaultAsync(e => e.Id == request.Id, ct)
                  ?? throw new NotFoundException($"Museum entry {request.Id} not found.");

            entry.Key = request.Key;
            entry.Name = request.Name;
            entry.Description = request.Description;
            entry.Wing = request.Wing;
            entry.Rarity = request.Rarity;
            entry.UnlockCondition = request.UnlockCondition;
            entry.SortOrder = request.SortOrder;

            var others = await db.MuseumEntryDefinitions
                .Where(e => e.Id != (request.Id ?? 0))
                .AsNoTracking()
                .ToListAsync(ct);

            var rejection = MuseumValidation.Reject(entry, others);

            if (rejection is not null)
            {
                db.ChangeTracker.Clear();

                throw new BadRequestException(rejection);
            }

            if (isNew)
            {
                db.MuseumEntryDefinitions.Add(entry);
            }

            await db.SaveChangesAsync(ct);

            await Audit(adminUserId, "MuseumEntry", entry.Id.ToString(),
                isNew ? "Created" : "Updated",
                $"{entry.Key}: {entry.Wing}, {entry.Rarity}", ct);

            var resulting = await db.MuseumEntryDefinitions.AsNoTracking().ToListAsync(ct);
            var foundBy = await db.PlayerMuseumEntries.CountAsync(e => e.EntryKey == entry.Key, ct);

            return ToDto(entry, foundBy, [.. MuseumValidation.Warn(resulting)]);
        }

        public async Task DeleteMuseumEntry(string adminUserId, int entryId, CancellationToken ct)
        {
            var entry = await db.MuseumEntryDefinitions.FirstOrDefaultAsync(e => e.Id == entryId, ct)
                ?? throw new NotFoundException($"Museum entry {entryId} not found.");

            // Refused rather than cascaded. A found entry is something a player collected —
            // deleting the definition takes it off their shelf, and §5A.1 makes the
            // collection the entire point of the system.
            var foundBy = await db.PlayerMuseumEntries.CountAsync(e => e.EntryKey == entry.Key, ct);

            if (foundBy > 0)
            {
                throw new BadRequestException(
                    $"{foundBy} player{(foundBy == 1 ? " has" : "s have")} found '{entry.Key}'. "
                    + "Deleting it would take it off their shelf.");
            }

            db.MuseumEntryDefinitions.Remove(entry);
            await db.SaveChangesAsync(ct);

            await Audit(adminUserId, "MuseumEntry", entryId.ToString(), "Deleted",
                $"{entry.Key} ({entry.Name})", ct);
        }

        // ── Audit (task 9) ──────────────────────────────────────────────

        public async Task<List<AdminAuditDto>> GetAuditTrail(int limit, CancellationToken ct)
        {
            // Clamped rather than trusted: an unbounded limit on a table that only grows is
            // a way to ask the database for everything by accident.
            var take = Math.Clamp(limit, 1, 500);

            return await db.AdminAuditEntries
                .OrderByDescending(e => e.OccurredUtc)
                .ThenByDescending(e => e.Id)
                .Take(take)
                .Select(e => new AdminAuditDto
                {
                    Id = e.Id,
                    AdminUsername = e.AdminUsername,
                    EntityType = e.EntityType,
                    EntityId = e.EntityId,
                    Action = e.Action,
                    Detail = e.Detail,
                    OccurredUtc = e.OccurredUtc,
                })
                .ToListAsync(ct);
        }

        /// <summary>
        /// Records an admin action.
        ///
        /// <para>Called after the change has been saved, deliberately: an audit entry for
        /// something that then failed to commit would be worse than none, because it would
        /// be believed.</para>
        /// </summary>
        private async Task Audit(
            string adminUserId, string entityType, string? entityId, string action,
            string? detail, CancellationToken ct)
        {
            // Denormalised so the trail still reads correctly if the user is later renamed
            // or removed — see AdminAuditEntry.
            var username = await db.Users
                .Where(u => u.Id == adminUserId)
                .Select(u => u.Username)
                .FirstOrDefaultAsync(ct);

            db.AdminAuditEntries.Add(new AdminAuditEntry
            {
                AdminUserId = adminUserId,
                AdminUsername = username ?? adminUserId,
                EntityType = entityType,
                EntityId = entityId,
                Action = action,
                Detail = detail,
                OccurredUtc = DateTime.UtcNow,
            });

            await db.SaveChangesAsync(ct);
        }
    }
}
