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
    public class AdminService(AppDbContext db) : IAdminService
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
