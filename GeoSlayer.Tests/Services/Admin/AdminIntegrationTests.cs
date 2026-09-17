using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.DTOs.Admin.Requests;
using GeoSlayer.Domain.DTOs.Admin.Responses;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Exceptions;
using GeoSlayer.Domain.Services.Admin;
using GeoSlayer.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Tests.Services.Admin
{
    /// <summary>
    /// Admin item and image management against a real database (Stage 18).
    /// </summary>
    [TestFixture]
    public class AdminIntegrationTests : DatabaseIntegrationTestBase
    {
        private AdminService _sut = null!;
        private User _admin = null!;

        private static CancellationToken Ct => CancellationToken.None;

        private static byte[] Png(int padding = 32) =>
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, .. new byte[padding]];

        protected override async Task CustomSetUp()
        {
            var (user, _) = await TestDatabaseSeedHelper.SeedMinimalData(DbContext);
            _admin = user;

            _admin.IsAdmin = true;
            await DbContext.SaveChangesAsync();

            // Materials and drop tables are needed by the task 6 tests — the ladder rules
            // are only meaningful against a real ladder.
            await TestDatabaseSeedHelper.SeedMaterialDefinitions(DbContext);
            await TestDatabaseSeedHelper.SeedSkillDefinitions(DbContext);
            await TestDatabaseSeedHelper.SeedEncounterDefinitions(DbContext);
            await TestDatabaseSeedHelper.SeedProgressionDefinitions(DbContext);

            _sut = new AdminService(DbContext);
        }

        private static SaveItemRequest NewItem(string key = "test_charm") => new()
        {
            Key = key,
            Name = "Test Charm",
            Description = "For the test suite.",
            Kind = ItemKind.Gear,
            Slot = ItemSlot.Trinket,
            Modifier = ItemModifier.SkillXpPercent,
            ModifierValue = 0.1,
            Tier = 1,
        };

        // ── Items ───────────────────────────────────────────────────────

        [Test]
        public async Task AnItemCanBeCreatedAndListed()
        {
            var saved = await _sut.SaveItem(_admin.Id, NewItem(), Ct);

            var items = await _sut.GetItems(Ct);

            Assert.Multiple(() =>
            {
                Assert.That(saved.Id, Is.GreaterThan(0));
                Assert.That(items.Any(i => i.Key == "test_charm"), Is.True);
                Assert.That(saved.ModifierText, Is.Not.Empty, "the admin sees what the player sees");
            });
        }

        [Test]
        public async Task AnItemCanBeUpdated()
        {
            var created = await _sut.SaveItem(_admin.Id, NewItem(), Ct);

            var update = NewItem();
            update.Id = created.Id;
            update.Name = "Renamed Charm";
            update.ModifierValue = 0.25;

            var updated = await _sut.SaveItem(_admin.Id, update, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(updated.Id, Is.EqualTo(created.Id), "updating must not create a second item");
                Assert.That(updated.Name, Is.EqualTo("Renamed Charm"));
                Assert.That(updated.ModifierValue, Is.EqualTo(0.25));
            });
        }

        [Test]
        public async Task ADuplicateKey_IsRejected()
        {
            // Keys are the identity every recipe and seed file references, so a collision
            // would silently repoint a recipe at the wrong item.
            await _sut.SaveItem(_admin.Id, NewItem("shared_key"), Ct);

            Assert.That(
                async () => await _sut.SaveItem(_admin.Id, NewItem("shared_key"), Ct),
                Throws.TypeOf<BadRequestException>());
        }

        [Test]
        public async Task AnItemUsingAnUnreadModifier_IsRejected()
        {
            // §4.3: an item that changes no behaviour is a bug, and the admin interface must
            // not be the way one gets created.
            var request = NewItem("dead_item");
            request.Modifier = (ItemModifier)9999;

            Assert.That(
                async () => await _sut.SaveItem(_admin.Id, request, Ct),
                Throws.TypeOf<BadRequestException>());

            Assert.That(await DbContext.Items.AnyAsync(i => i.Key == "dead_item"), Is.False);
        }

        [Test]
        public async Task AnItemWithNoKeyOrName_IsRejected()
        {
            var noKey = NewItem();
            noKey.Key = "  ";

            var noName = NewItem("has_key");
            noName.Name = "";

            Assert.Multiple(() =>
            {
                Assert.That(async () => await _sut.SaveItem(_admin.Id, noKey, Ct),
                    Throws.TypeOf<BadRequestException>());
                Assert.That(async () => await _sut.SaveItem(_admin.Id, noName, Ct),
                    Throws.TypeOf<BadRequestException>());
            });
        }

        [Test]
        public async Task DeletingAnItemProducedByARecipe_IsRejected()
        {
            // Cascading would leave the recipe pointing at nothing, surfacing as a crash in
            // the crafting screen rather than here where it can be explained.
            var created = await _sut.SaveItem(_admin.Id, NewItem(), Ct);

            DbContext.Recipes.Add(new Recipe
            {
                Key = "craft_test_charm",
                Name = "Test Charm",
                Description = "",
                SkillType = SkillType.Smithing,
                LevelRequired = 1,
                DurationSeconds = 10,
                XpReward = 1,
                OutputItemId = created.Id,
                OutputQuantity = 1,
            });
            await DbContext.SaveChangesAsync();

            Assert.That(
                async () => await _sut.DeleteItem(_admin.Id, created.Id, Ct),
                Throws.TypeOf<BadRequestException>());
        }

        [Test]
        public async Task AnUnusedItemCanBeDeleted()
        {
            var created = await _sut.SaveItem(_admin.Id, NewItem(), Ct);

            await _sut.DeleteItem(_admin.Id, created.Id, Ct);

            Assert.That(await DbContext.Items.AnyAsync(i => i.Id == created.Id), Is.False);
        }

        // ── Images ──────────────────────────────────────────────────────

        [Test]
        public async Task AnImageCanBeUploadedAndRead()
        {
            var item = await _sut.SaveItem(_admin.Id, NewItem(), Ct);

            await _sut.UploadItemImage(_admin.Id, item.Id, Png(), "image/png", "icon.png", Ct);

            var image = await _sut.GetItemImage(item.Id, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(image, Is.Not.Null);
                Assert.That(image!.Value.ContentType, Is.EqualTo("image/png"));
                Assert.That(image.Value.Data, Is.Not.Empty);
            });
        }

        [Test]
        public async Task UploadingAgain_ReplacesRatherThanAccumulates()
        {
            var item = await _sut.SaveItem(_admin.Id, NewItem(), Ct);

            await _sut.UploadItemImage(_admin.Id, item.Id, Png(16), "image/png", "first.png", Ct);
            await _sut.UploadItemImage(_admin.Id, item.Id, Png(64), "image/png", "second.png", Ct);

            var rows = await DbContext.ItemImages.CountAsync(i => i.ItemId == item.Id);
            var stored = await DbContext.ItemImages.FirstAsync(i => i.ItemId == item.Id);

            Assert.Multiple(() =>
            {
                Assert.That(rows, Is.EqualTo(1), "one image per item");
                Assert.That(stored.FileName, Is.EqualTo("second.png"));
            });
        }

        [Test]
        public async Task AnInvalidImage_IsRejectedAndNothingIsStored()
        {
            var item = await _sut.SaveItem(_admin.Id, NewItem(), Ct);

            var html = "<html><script>alert(1)</script></html>"u8.ToArray();

            Assert.That(
                async () => await _sut.UploadItemImage(_admin.Id, item.Id, html, "image/png", "x.png", Ct),
                Throws.TypeOf<BadRequestException>());

            Assert.That(await DbContext.ItemImages.AnyAsync(i => i.ItemId == item.Id), Is.False);
        }

        [Test]
        public async Task AnItemWithNoImage_ReadsAsNull()
        {
            // The app falls back to an emoji for these, so null must be an ordinary answer
            // rather than an error.
            var item = await _sut.SaveItem(_admin.Id, NewItem(), Ct);

            Assert.That(await _sut.GetItemImage(item.Id, Ct), Is.Null);
        }

        [Test]
        public async Task GetItems_ReportsWhetherAnImageExists()
        {
            var withImage = await _sut.SaveItem(_admin.Id, NewItem("with_image"), Ct);
            await _sut.SaveItem(_admin.Id, NewItem("without_image"), Ct);

            await _sut.UploadItemImage(_admin.Id, withImage.Id, Png(), "image/png", "i.png", Ct);

            var items = await _sut.GetItems(Ct);

            Assert.Multiple(() =>
            {
                Assert.That(items.First(i => i.Key == "with_image").HasImage, Is.True);
                Assert.That(items.First(i => i.Key == "without_image").HasImage, Is.False);
            });
        }

        [Test]
        public async Task DeletingAnItem_TakesItsImageWithIt()
        {
            // An orphaned blob is invisible storage nobody will think to clean up.
            var item = await _sut.SaveItem(_admin.Id, NewItem(), Ct);
            await _sut.UploadItemImage(_admin.Id, item.Id, Png(), "image/png", "i.png", Ct);

            await _sut.DeleteItem(_admin.Id, item.Id, Ct);

            Assert.That(await DbContext.ItemImages.AnyAsync(i => i.ItemId == item.Id), Is.False);
        }

        [Test]
        public async Task ThePlayerApp_LearnsWhichItemsHaveArtwork()
        {
            // Closes the loop for task 3. Without HasImage on the player's own item list,
            // the app's only way to find out is to request an image and handle the 404 —
            // a failed request per imageless item, on every render.
            var withArt = await _sut.SaveItem(_admin.Id, NewItem("with_art"), Ct);
            var withoutArt = await _sut.SaveItem(_admin.Id, NewItem("without_art"), Ct);

            await _sut.UploadItemImage(_admin.Id, withArt.Id, Png(), "image/png", "i.png", Ct);

            var player = await DbContext.Players.FirstAsync();

            DbContext.PlayerItems.AddRange(
                new PlayerItem { PlayerId = player.Id, ItemId = withArt.Id, Quantity = 1 },
                new PlayerItem { PlayerId = player.Id, ItemId = withoutArt.Id, Quantity = 1 });

            await DbContext.SaveChangesAsync();

            var crafting = TestDatabaseSeedHelper.CreateCraftingService(
                DbContext,
                TestDatabaseSeedHelper.CreateProgressionService(DbContext),
                TestDatabaseSeedHelper.CreateMaterialService(DbContext, TerrainType.Urban));

            var held = await crafting.GetItems(player.Id, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(held.First(i => i.Key == "with_art").HasImage, Is.True);
                Assert.That(held.First(i => i.Key == "without_art").HasImage, Is.False);
            });
        }

        // ── Materials (task 6) ──────────────────────────────────────────

        /// <summary>
        /// A material on a ladder that does not exist yet.
        ///
        /// <para>Exploration is the only skill with no seeded ladder, and Coastal's seeded
        /// materials are terrain drops with no skill attached — so this pair is free. Every
        /// other skill already owns all seven rungs of its category, which is exactly what
        /// <c>MaterialValidation</c> refuses to let a second material share.</para>
        /// </summary>
        private static SaveMaterialRequest NewMaterial(
            string key = "test_find",
            SkillType? skill = SkillType.Exploration,
            MaterialCategory category = MaterialCategory.Coastal,
            int tier = 6,
            int level = 70,
            double seconds = 40,
            double xp = 150) => new()
            {
                Key = key,
                Name = "Test Ore",
                SkillType = skill,
                Category = category,
                Tier = tier,
                LevelRequired = level,
                BaseGatherSeconds = seconds,
                XpPerUnit = xp,
            };

        [Test]
        public async Task AMaterialCanBeCreated_AndCarriesItsDerivedValues()
        {
            var saved = await _sut.SaveMaterial(_admin.Id, NewMaterial(), Ct);

            Assert.Multiple(() =>
            {
                Assert.That(saved.Id, Is.GreaterThan(0));

                // Price is derived from tier and category (§5D.1), not stored — an admin
                // should see the consequence of their choices rather than discover it.
                Assert.That(saved.UnitPrice, Is.GreaterThan(0));
                Assert.That(saved.XpPerSecond, Is.EqualTo(150.0 / 40).Within(0.001));
            });
        }

        [Test]
        public async Task AMaterialBreakingALadderRule_IsRejectedAndNotSaved()
        {
            // Mined already holds all seven Mining rungs, so claiming that category for a
            // different skill is the shared-category violation. MaterialValidation covers
            // the rule itself; this proves the service enforces it and stores nothing.
            var broken = NewMaterial(
                key: "intruder", skill: SkillType.Exploration, category: MaterialCategory.Mined);

            Assert.That(
                async () => await _sut.SaveMaterial(_admin.Id, broken, Ct),
                Throws.TypeOf<BadRequestException>());

            Assert.That(await DbContext.Materials.AnyAsync(m => m.Key == "intruder"), Is.False,
                "a rejected material must not reach the database");
        }

        [Test]
        public async Task ARejectedMaterial_DoesNotCorruptTheNextSave()
        {
            // The service mutates the tracked entity before validating, so a rejection has
            // to clear the change tracker — otherwise the bad values ride along on the next
            // unrelated SaveChanges.
            var existing = await DbContext.Materials
                .Where(m => m.SkillType == SkillType.Mining)
                .OrderBy(m => m.Tier)
                .FirstAsync();

            var originalLevel = existing.LevelRequired;

            // Tier 0 is off the ladder entirely, so this is refused whatever else is seeded.
            var broken = NewMaterial(key: existing.Key, skill: SkillType.Mining,
                category: MaterialCategory.Mined, tier: 0);
            broken.Id = existing.Id;

            Assert.That(
                async () => await _sut.SaveMaterial(_admin.Id, broken, Ct),
                Throws.TypeOf<BadRequestException>());

            DbContext.ChangeTracker.Clear();

            var reloaded = await DbContext.Materials.FirstAsync(m => m.Id == existing.Id);

            Assert.That(reloaded.LevelRequired, Is.EqualTo(originalLevel),
                "the rejected edit must not have been persisted");
        }

        [Test]
        public async Task AMaterialInADropTable_CannotBeDeleted()
        {
            // Deleting it would leave the drop table pointing at nothing, surfacing as a
            // crash during a sync rather than here where it can be explained.
            var inTable = await DbContext.DropTableEntries
                .Select(e => e.MaterialId)
                .FirstAsync();

            Assert.That(
                async () => await _sut.DeleteMaterial(_admin.Id, inTable, Ct),
                Throws.TypeOf<BadRequestException>());
        }

        [Test]
        public async Task AnUnreferencedMaterialCanBeDeleted()
        {
            var created = await _sut.SaveMaterial(
                _admin.Id, NewMaterial("orphan_find", tier: 7, level: 90, seconds: 60, xp: 240), Ct);

            await _sut.DeleteMaterial(_admin.Id, created.Id, Ct);

            Assert.That(await DbContext.Materials.AnyAsync(m => m.Id == created.Id), Is.False);
        }

        [Test]
        public async Task MaterialMutations_AreAudited()
        {
            var created = await _sut.SaveMaterial(
                _admin.Id, NewMaterial("audited_find", tier: 7, level: 90, seconds: 60, xp: 240), Ct);

            await _sut.DeleteMaterial(_admin.Id, created.Id, Ct);

            var trail = await _sut.GetAuditTrail(50, Ct);
            var forMaterial = trail.Where(e => e.EntityType == "Material").ToList();

            Assert.That(forMaterial.Select(e => e.Action),
                Does.Contain("Created").And.Contains("Deleted"));
        }

        // ── Recipes (task 7) ────────────────────────────────────────────

        /// <summary>
        /// A saveable recipe, creating its output item on first use.
        ///
        /// <para>Reuses the item when it already exists, because the update tests call this
        /// twice for the same recipe and item keys are unique.</para>
        /// </summary>
        private async Task<SaveRecipeRequest> NewRecipe(string key = "test_craft")
        {
            var outputKey = $"{key}_output";

            var existing = await DbContext.Items
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Key == outputKey);

            var item = existing is null
                ? await _sut.SaveItem(_admin.Id, NewItem(outputKey), Ct)
                : ToExistingItem(existing);

            var input = await DbContext.Materials.FirstAsync(m => m.SkillType == SkillType.Mining);

            return new SaveRecipeRequest
            {
                Key = key,
                Name = "Test Craft",
                Description = "",
                SkillType = SkillType.Smithing,
                LevelRequired = 5,
                DurationSeconds = 600,
                XpReward = 70,
                OutputItemId = item.Id,
                OutputQuantity = 1,
                Inputs = [new SaveRecipeInputRequest { MaterialId = input.Id, Quantity = 4 }],
            };
        }

        /// <summary>Just enough of an AdminItemDto for NewRecipe to read its id.</summary>
        private static AdminItemDto ToExistingItem(Item item) => new()
        {
            Id = item.Id,
            Key = item.Key,
            Name = item.Name,
            Description = item.Description,
            ModifierText = "",
        };

        [Test]
        public async Task ARecipeCanBeCreated_WithItsInputsAndCost()
        {
            var saved = await _sut.SaveRecipe(_admin.Id, await NewRecipe(), Ct);

            Assert.Multiple(() =>
            {
                Assert.That(saved.Id, Is.GreaterThan(0));
                Assert.That(saved.Inputs, Has.Count.EqualTo(1));

                // Named, not bare ids — the chain is unreadable otherwise.
                Assert.That(saved.Inputs[0].MaterialName, Is.Not.Empty);
                Assert.That(saved.OutputName, Is.Not.Empty);

                Assert.That(saved.InputCost, Is.GreaterThan(0));
            });
        }

        [Test]
        public async Task UpdatingARecipe_ReplacesItsInputsRatherThanAccumulating()
        {
            // Inputs are replaced wholesale, so a removed one must actually disappear —
            // a phantom input is a cost the player still pays.
            var created = await _sut.SaveRecipe(_admin.Id, await NewRecipe(), Ct);

            var other = await DbContext.Materials
                .Where(m => m.SkillType == SkillType.Mining)
                .OrderByDescending(m => m.Tier)
                .FirstAsync();

            var update = await NewRecipe();
            update.Id = created.Id;
            update.Key = created.Key;
            update.OutputItemId = created.OutputItemId;
            update.Inputs = [new SaveRecipeInputRequest { MaterialId = other.Id, Quantity = 2 }];

            var updated = await _sut.SaveRecipe(_admin.Id, update, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(updated.Inputs, Has.Count.EqualTo(1));
                Assert.That(updated.Inputs[0].MaterialId, Is.EqualTo(other.Id));
            });

            var rows = await DbContext.RecipeInputs.CountAsync(i => i.RecipeId == created.Id);

            Assert.That(rows, Is.EqualTo(1), "the old input row must be gone, not orphaned");
        }

        [Test]
        public async Task ARecipeWithNoInputs_IsRejected()
        {
            var broken = await NewRecipe("no_inputs");
            broken.Inputs = [];

            Assert.That(
                async () => await _sut.SaveRecipe(_admin.Id, broken, Ct),
                Throws.TypeOf<BadRequestException>());

            Assert.That(await DbContext.Recipes.AnyAsync(r => r.Key == "no_inputs"), Is.False);
        }

        [Test]
        public async Task ARecipeReferencingAMissingMaterial_IsRejected()
        {
            // A dangling foreign key would otherwise surface as a database error with no
            // useful message.
            var broken = await NewRecipe("bad_input");
            broken.Inputs = [new SaveRecipeInputRequest { MaterialId = 999_999, Quantity = 1 }];

            Assert.That(
                async () => await _sut.SaveRecipe(_admin.Id, broken, Ct),
                Throws.TypeOf<BadRequestException>());
        }

        [Test]
        public async Task ALossMakingRecipe_SavesButCarriesAWarning()
        {
            // The split that matters: structurally sound, economically silly. §5D.1 expects
            // produced goods to beat their parts, but an admin mid-tune must be able to save.
            var expensive = await DbContext.Materials
                .Where(m => m.SkillType == SkillType.Mining)
                .OrderByDescending(m => m.Tier)
                .FirstAsync();

            var cheap = await DbContext.Materials
                .Where(m => m.SkillType == SkillType.Mining)
                .OrderBy(m => m.Tier)
                .FirstAsync();

            var request = await NewRecipe("loss_maker");
            request.OutputItemId = null;
            request.OutputMaterialId = cheap.Id;
            request.Inputs = [new SaveRecipeInputRequest { MaterialId = expensive.Id, Quantity = 10 }];

            var saved = await _sut.SaveRecipe(_admin.Id, request, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(saved.Id, Is.GreaterThan(0), "it must still save");
                Assert.That(saved.Warnings.Any(w => w.Contains("loses money")), Is.True);
                Assert.That(saved.InputCost, Is.GreaterThan(saved.OutputValue));
            });
        }

        [Test]
        public async Task ARecipeWithQueuedCrafts_CannotBeDeleted()
        {
            // Deleting it would leave the queue pointing at nothing.
            var created = await _sut.SaveRecipe(_admin.Id, await NewRecipe(), Ct);
            var player = await DbContext.Players.FirstAsync();

            DbContext.PlayerCrafts.Add(new PlayerCraft
            {
                PlayerId = player.Id,
                RecipeId = created.Id,
                RecipeKey = created.Key,
                StartedUtc = DateTime.UtcNow,
                CompletesUtc = DateTime.UtcNow.AddHours(1),
                Collected = false,
            });
            await DbContext.SaveChangesAsync();

            Assert.That(
                async () => await _sut.DeleteRecipe(_admin.Id, created.Id, Ct),
                Throws.TypeOf<BadRequestException>());
        }

        [Test]
        public async Task DeletingARecipe_TakesItsInputsWithIt()
        {
            // A recipe owns its inputs outright — nothing else points at them — so cascading
            // is correct here, unlike for an item or a material.
            var created = await _sut.SaveRecipe(_admin.Id, await NewRecipe(), Ct);

            await _sut.DeleteRecipe(_admin.Id, created.Id, Ct);

            Assert.Multiple(async () =>
            {
                Assert.That(await DbContext.Recipes.AnyAsync(r => r.Id == created.Id), Is.False);
                Assert.That(await DbContext.RecipeInputs.AnyAsync(i => i.RecipeId == created.Id), Is.False);
            });
        }

        [Test]
        public async Task RecipeMutations_AreAudited()
        {
            var created = await _sut.SaveRecipe(_admin.Id, await NewRecipe("audited_craft"), Ct);
            await _sut.DeleteRecipe(_admin.Id, created.Id, Ct);

            var forRecipe = (await _sut.GetAuditTrail(50, Ct))
                .Where(e => e.EntityType == "Recipe")
                .Select(e => e.Action)
                .ToList();

            Assert.That(forRecipe, Does.Contain("Created").And.Contains("Deleted"));
        }

        // ── Encounters (task 8) ─────────────────────────────────────────

        [Test]
        public async Task TheSeededEncounters_LoadWithNoSetWarnings()
        {
            // If the shipped ladder cannot pass its own validator, one of the two is wrong.
            var encounters = await _sut.GetEncounters(Ct);

            Assert.That(encounters, Is.Not.Empty);
            Assert.That(encounters[0].SetWarnings, Is.Empty);
        }

        [Test]
        public async Task ARoamingEncounterCoveringANewTier_CanBeAdded()
        {
            var added = await _sut.SaveEncounter(_admin.Id, new SaveEncounterRequest
            {
                Key = "roaming_extra",
                Name = "Something Else",
                Tier = 3,
                MinCombatLevel = 20,
            }, Ct);

            Assert.That(added.Id, Is.GreaterThan(0));
        }

        [Test]
        public async Task DeletingTheOnlyRoamingEncounterAtATier_IsRefused()
        {
            // §5C.2, at runtime. Removing it would leave every castle-less player unable to
            // train Combat past that tier — a symptom close to undiagnosable from a bug
            // report, which is exactly why it is refused rather than warned about.
            var tiers = await DbContext.EncounterDefinitions
                .Where(e => !e.IsTrainingGround)
                .GroupBy(e => e.Tier)
                .Select(g => new { Tier = g.Key, Count = g.Count(), Id = g.Min(e => e.Id) })
                .ToListAsync();

            var soleAtTier = tiers.First(t => t.Count == 1);

            Assert.That(
                async () => await _sut.DeleteEncounter(_admin.Id, soleAtTier.Id, Ct),
                Throws.TypeOf<BadRequestException>());

            Assert.That(await DbContext.EncounterDefinitions.AnyAsync(e => e.Id == soleAtTier.Id),
                Is.True, "a refused delete must leave the row alone");
        }

        [Test]
        public async Task FlippingTheOnlyRoamingEncounterToATrainingGround_IsRefused()
        {
            // The subtler version of the same failure: the tier still exists, but only on
            // historic ground. Checked against the set the save would produce, not the one
            // that exists — otherwise the old row still covers the tier and it passes.
            var tiers = await DbContext.EncounterDefinitions
                .Where(e => !e.IsTrainingGround)
                .GroupBy(e => e.Tier)
                .Select(g => new { Count = g.Count(), Id = g.Min(e => e.Id) })
                .ToListAsync();

            var sole = tiers.First(t => t.Count == 1);
            var definition = await DbContext.EncounterDefinitions.AsNoTracking()
                .FirstAsync(e => e.Id == sole.Id);

            Assert.That(
                async () => await _sut.SaveEncounter(_admin.Id, new SaveEncounterRequest
                {
                    Id = definition.Id,
                    Key = definition.Key,
                    Name = definition.Name,
                    Tier = definition.Tier,
                    MinCombatLevel = definition.MinCombatLevel,
                    IsTrainingGround = true,
                }, Ct),
                Throws.TypeOf<BadRequestException>());

            DbContext.ChangeTracker.Clear();

            var reloaded = await DbContext.EncounterDefinitions.FirstAsync(e => e.Id == definition.Id);

            Assert.That(reloaded.IsTrainingGround, Is.False,
                "the rejected edit must not have been persisted");
        }

        [Test]
        public async Task ATrainingGroundCanBeDeleted_BecauseRoamingStillCoversTheTier()
        {
            var ground = await _sut.SaveEncounter(_admin.Id, new SaveEncounterRequest
            {
                Key = "spare_castle",
                Name = "A Spare Ruin",
                Tier = 3,
                MinCombatLevel = 20,
                IsTrainingGround = true,
            }, Ct);

            await _sut.DeleteEncounter(_admin.Id, ground.Id, Ct);

            Assert.That(await DbContext.EncounterDefinitions.AnyAsync(e => e.Id == ground.Id), Is.False);
        }

        [Test]
        public async Task GatingTierOne_IsRefused()
        {
            // §5C.1: a level-1 player must meet something.
            Assert.That(
                async () => await _sut.SaveEncounter(_admin.Id, new SaveEncounterRequest
                {
                    Key = "gated_starter",
                    Name = "Gated",
                    Tier = 1,
                    MinCombatLevel = 25,
                }, Ct),
                Throws.TypeOf<BadRequestException>());
        }

        [Test]
        public async Task EncounterMutations_AreAudited()
        {
            var created = await _sut.SaveEncounter(_admin.Id, new SaveEncounterRequest
            {
                Key = "audited_fight",
                Name = "Audited",
                Tier = 4,
                MinCombatLevel = 35,
                IsTrainingGround = true,
            }, Ct);

            await _sut.DeleteEncounter(_admin.Id, created.Id, Ct);

            var forEncounter = (await _sut.GetAuditTrail(50, Ct))
                .Where(e => e.EntityType == "Encounter")
                .Select(e => e.Action)
                .ToList();

            Assert.That(forEncounter, Does.Contain("Created").And.Contains("Deleted"));
        }

        // ── Progression (task 8) ────────────────────────────────────────

        [Test]
        public async Task TheSeededProgression_LoadsWithNoLadderWarnings()
        {
            var progression = await _sut.GetProgression(Ct);

            Assert.Multiple(() =>
            {
                Assert.That(progression.Unlocks, Is.Not.Empty);
                Assert.That(progression.Upgrades, Is.Not.Empty);
                Assert.That(progression.LadderWarnings, Is.Empty);
            });
        }

        [Test]
        public async Task AnUpgrade_ReportsWhatItCostsToMax()
        {
            // The number a curve makes hard to eyeball, and the one that decides whether an
            // upgrade is worth buying at all.
            var saved = await _sut.SaveUpgrade(_admin.Id, new SaveUpgradeRequest
            {
                Key = "test_upgrade",
                Name = "Test Upgrade",
                Category = "Exploration",
                MaxRank = 3,
                CostCurve = "2,5,9",
                EffectPerRank = 1,
            }, Ct);

            Assert.That(saved.TotalCost, Is.EqualTo(16));
        }

        [Test]
        public async Task AMalformedCostCurve_IsRejectedAndNotStored()
        {
            // The crash this validator exists for: UpgradeDefinition.Costs parses with
            // int.Parse on every upgrades-screen load, so a stored typo takes that screen
            // down for every player, not just the admin who made it.
            Assert.That(
                async () => await _sut.SaveUpgrade(_admin.Id, new SaveUpgradeRequest
                {
                    Key = "broken_curve",
                    Name = "Broken",
                    MaxRank = 3,
                    CostCurve = "1,two,3",
                }, Ct),
                Throws.TypeOf<BadRequestException>());

            Assert.That(
                await DbContext.UpgradeDefinitions.AnyAsync(u => u.Key == "broken_curve"),
                Is.False);
        }

        [Test]
        public async Task ACurveThatDoesNotMatchMaxRank_IsRejected()
        {
            Assert.That(
                async () => await _sut.SaveUpgrade(_admin.Id, new SaveUpgradeRequest
                {
                    Key = "mismatched",
                    Name = "Mismatched",
                    MaxRank = 5,
                    CostCurve = "1,2",
                }, Ct),
                Throws.TypeOf<BadRequestException>());
        }

        [Test]
        public async Task AnUpgradePlayersHaveBought_CannotBeDeleted()
        {
            // Deleting it would take what they paid for without refunding the Bonus Points.
            var upgrade = await _sut.SaveUpgrade(_admin.Id, new SaveUpgradeRequest
            {
                Key = "purchased_upgrade",
                Name = "Purchased",
                MaxRank = 2,
                CostCurve = "1,2",
                EffectPerRank = 1,
            }, Ct);

            var player = await DbContext.Players.FirstAsync();

            DbContext.PlayerUpgrades.Add(new PlayerUpgrade
            {
                PlayerId = player.Id,
                UpgradeKey = upgrade.Key,
                Rank = 1,
            });
            await DbContext.SaveChangesAsync();

            Assert.That(
                async () => await _sut.DeleteUpgrade(_admin.Id, upgrade.Id, Ct),
                Throws.TypeOf<BadRequestException>());
        }

        [Test]
        public async Task AnUnboughtUpgradeCanBeDeleted()
        {
            var upgrade = await _sut.SaveUpgrade(_admin.Id, new SaveUpgradeRequest
            {
                Key = "unbought_upgrade",
                Name = "Unbought",
                MaxRank = 1,
                CostCurve = "3",
                EffectPerRank = 1,
            }, Ct);

            await _sut.DeleteUpgrade(_admin.Id, upgrade.Id, Ct);

            Assert.That(
                await DbContext.UpgradeDefinitions.AnyAsync(u => u.Id == upgrade.Id),
                Is.False);
        }

        [Test]
        public async Task TheSamePayloadAtTwoLevels_IsRejected()
        {
            // ApplyUnlocks skips what is already owned, so the second rung silently never
            // fires — the kind of dead configuration that is very hard to notice.
            var existing = await DbContext.UnlockDefinitions.AsNoTracking().FirstAsync();

            Assert.That(
                async () => await _sut.SaveUnlock(_admin.Id, new SaveUnlockRequest
                {
                    AdventurerLevel = existing.AdventurerLevel + 25,
                    UnlockType = existing.UnlockType,
                    Payload = existing.Payload,
                    DisplayName = "Duplicate rung",
                }, Ct),
                Throws.TypeOf<BadRequestException>());
        }

        [Test]
        public async Task ProgressionMutations_AreAudited()
        {
            var upgrade = await _sut.SaveUpgrade(_admin.Id, new SaveUpgradeRequest
            {
                Key = "audited_upgrade",
                Name = "Audited",
                MaxRank = 1,
                CostCurve = "1",
                EffectPerRank = 1,
            }, Ct);

            await _sut.DeleteUpgrade(_admin.Id, upgrade.Id, Ct);

            var actions = (await _sut.GetAuditTrail(50, Ct))
                .Where(e => e.EntityType == "Upgrade")
                .Select(e => e.Action)
                .ToList();

            Assert.That(actions, Does.Contain("Created").And.Contains("Deleted"));
        }

        // ── Audit trail (task 9) ────────────────────────────────────────

        [Test]
        public async Task EveryMutation_IsAudited()
        {
            var item = await _sut.SaveItem(_admin.Id, NewItem(), Ct);

            var update = NewItem();
            update.Id = item.Id;
            update.Name = "Changed";
            await _sut.SaveItem(_admin.Id, update, Ct);

            await _sut.UploadItemImage(_admin.Id, item.Id, Png(), "image/png", "i.png", Ct);
            await _sut.DeleteItemImage(_admin.Id, item.Id, Ct);
            await _sut.DeleteItem(_admin.Id, item.Id, Ct);

            var trail = await _sut.GetAuditTrail(100, Ct);
            var actions = trail.Select(e => e.Action).ToList();

            Assert.That(actions, Does.Contain("Created")
                .And.Contains("Updated")
                .And.Contains("ImageUploaded")
                .And.Contains("ImageDeleted")
                .And.Contains("Deleted"));
        }

        [Test]
        public async Task AnAuditEntry_RecordsWhoWhatAndWhen()
        {
            var before = DateTime.UtcNow.AddSeconds(-1);

            await _sut.SaveItem(_admin.Id, NewItem(), Ct);

            var entry = (await _sut.GetAuditTrail(1, Ct)).Single();

            Assert.Multiple(() =>
            {
                Assert.That(entry.AdminUsername, Is.EqualTo(_admin.Username));
                Assert.That(entry.EntityType, Is.EqualTo("Item"));
                Assert.That(entry.Detail, Does.Contain("test_charm"));
                Assert.That(entry.OccurredUtc, Is.GreaterThanOrEqualTo(before));
            });
        }

        [Test]
        public async Task TheAuditTrail_IsNewestFirst()
        {
            await _sut.SaveItem(_admin.Id, NewItem("first"), Ct);
            await _sut.SaveItem(_admin.Id, NewItem("second"), Ct);

            var trail = await _sut.GetAuditTrail(10, Ct);

            Assert.That(trail.First().Detail, Does.Contain("second"));
        }

        [Test]
        public async Task TheAuditLimit_IsClamped()
        {
            // An unbounded limit on a table that only grows is a way to ask the database for
            // everything by accident.
            await _sut.SaveItem(_admin.Id, NewItem(), Ct);

            Assert.Multiple(async () =>
            {
                Assert.That((await _sut.GetAuditTrail(int.MaxValue, Ct)).Count, Is.LessThanOrEqualTo(500));
                Assert.That((await _sut.GetAuditTrail(0, Ct)).Count, Is.GreaterThanOrEqualTo(1));
                Assert.That((await _sut.GetAuditTrail(-5, Ct)).Count, Is.GreaterThanOrEqualTo(1));
            });
        }

        [Test]
        public async Task ADeletionRecordsHowManyPlayersHeldIt()
        {
            // The destructive case: players lost something, and later someone will ask what.
            var item = await _sut.SaveItem(_admin.Id, NewItem(), Ct);

            var player = await DbContext.Players.FirstAsync();

            DbContext.PlayerItems.Add(new PlayerItem
            {
                PlayerId = player.Id,
                ItemId = item.Id,
                Quantity = 1,
            });
            await DbContext.SaveChangesAsync();

            await _sut.DeleteItem(_admin.Id, item.Id, Ct);

            var entry = (await _sut.GetAuditTrail(1, Ct)).Single();

            Assert.That(entry.Detail, Does.Contain("1 player"));
        }
    }
}
