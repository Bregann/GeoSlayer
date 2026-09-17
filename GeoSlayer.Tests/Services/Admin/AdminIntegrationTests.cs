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
        private StubGameSettings _settings = null!;
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

            _settings = new StubGameSettings();
            _sut = new AdminService(DbContext, _settings);
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

        // ── Tunable numbers (Stage 18) ──────────────────────────────────

        private async Task SeedGameSettings()
        {
            foreach (var setting in GameSettingKeys.All)
            {
                DbContext.GameSettings.Add(new GameSetting
                {
                    Key = setting.Key,
                    Value = setting.Value,
                    Default = setting.Default,
                    Category = setting.Category,
                    Description = setting.Description,
                    MinValue = setting.MinValue,
                    MaxValue = setting.MaxValue,
                });
            }

            await DbContext.SaveChangesAsync();
        }

        [Test]
        public async Task SettingsLoad_AndStartUnchanged()
        {
            await SeedGameSettings();

            var settings = await _sut.GetGameSettings(Ct);

            Assert.That(settings, Is.Not.Empty);
            Assert.That(settings.All(s => !s.IsChanged), Is.True,
                "a freshly seeded table has not been tuned by anyone");
        }

        [Test]
        public async Task ChangingASetting_MarksItAsChangedAndReloadsTheCache()
        {
            // The reload is the point: a setting that needed a restart to take effect would
            // be no better than the constant it replaced.
            await SeedGameSettings();

            var target = (await _sut.GetGameSettings(Ct))
                .First(s => s.Key == GameSettingKeys.DistanceSynergyXpPerKm);

            var before = _settings.ReloadCount;

            var saved = await _sut.SaveGameSetting(_admin.Id, new SaveGameSettingRequest
            {
                Id = target.Id,
                Value = "20",
            }, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(saved.Value, Is.EqualTo("20"));
                Assert.That(saved.IsChanged, Is.True);
                Assert.That(saved.Default, Is.EqualTo("12"), "the shipped value is still recorded");
                Assert.That(_settings.ReloadCount, Is.EqualTo(before + 1));
            });
        }

        [Test]
        public async Task ASettingOutsideItsBounds_IsRejected()
        {
            // Bounds are part of the definition, not advice — an interest rate of 10 per
            // hour is a way to break the game from a text box.
            await SeedGameSettings();

            var rate = (await _sut.GetGameSettings(Ct))
                .First(s => s.Key == GameSettingKeys.BankingInterestRate);

            Assert.That(
                async () => await _sut.SaveGameSetting(_admin.Id, new SaveGameSettingRequest
                {
                    Id = rate.Id,
                    Value = "10",
                }, Ct),
                Throws.TypeOf<BadRequestException>());
        }

        [Test]
        public async Task ANonNumericSetting_IsRejected()
        {
            await SeedGameSettings();

            var any = (await _sut.GetGameSettings(Ct)).First();

            Assert.That(
                async () => await _sut.SaveGameSetting(_admin.Id, new SaveGameSettingRequest
                {
                    Id = any.Id,
                    Value = "lots",
                }, Ct),
                Throws.TypeOf<BadRequestException>());
        }

        [Test]
        public async Task ASettingChange_RecordsBothValuesInTheAudit()
        {
            // "What was it before" is the question actually asked of an audit trail after a
            // balance change goes wrong.
            await SeedGameSettings();

            var target = (await _sut.GetGameSettings(Ct))
                .First(s => s.Key == GameSettingKeys.DistanceSynergyXpPerKm);

            await _sut.SaveGameSetting(_admin.Id, new SaveGameSettingRequest
            {
                Id = target.Id,
                Value = "30",
            }, Ct);

            var entry = (await _sut.GetAuditTrail(1, Ct)).Single();

            Assert.Multiple(() =>
            {
                Assert.That(entry.EntityType, Is.EqualTo("GameSetting"));
                Assert.That(entry.Detail, Does.Contain("12").And.Contains("30"));
            });
        }

        // ── Players (task 9, read-only) ─────────────────────────────────

        [Test]
        public async Task APlayerCanBeFoundByUsername()
        {
            var results = await _sut.SearchPlayers(_admin.Username, Ct);

            Assert.That(results.Any(r => r.Username == _admin.Username), Is.True);
        }

        [Test]
        public async Task APlayerCanBeFoundByEmail()
        {
            // Support questions arrive by email, so this is the field that ties a message
            // to an account.
            var results = await _sut.SearchPlayers(_admin.Email, Ct);

            Assert.That(results, Is.Not.Empty);
        }

        [Test]
        public async Task SearchIsCaseInsensitive()
        {
            var results = await _sut.SearchPlayers(_admin.Username.ToUpperInvariant(), Ct);

            Assert.That(results, Is.Not.Empty);
        }

        [Test]
        public async Task AVeryShortSearch_ReturnsNothing()
        {
            // A one-character search matches most of the table: a slow query and a useless
            // answer.
            Assert.Multiple(async () =>
            {
                Assert.That(await _sut.SearchPlayers("a", Ct), Is.Empty);
                Assert.That(await _sut.SearchPlayers("", Ct), Is.Empty);
                Assert.That(await _sut.SearchPlayers("  ", Ct), Is.Empty);
            });
        }

        [Test]
        public async Task APlayerViewCarriesTheirState()
        {
            var player = await DbContext.Players.FirstAsync();

            var view = await _sut.GetPlayer(player.Id, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(view.PlayerId, Is.EqualTo(player.Id));
                Assert.That(view.Username, Is.EqualTo(_admin.Username));
                Assert.That(view.Email, Is.EqualTo(_admin.Email));
                Assert.That(view.IsAdmin, Is.True);
                Assert.That(view.AdventurerLevel, Is.EqualTo(player.AdventurerLevel));
            });
        }

        [Test]
        public async Task APlayerViewIncludesWhatTheyHold()
        {
            var player = await DbContext.Players.FirstAsync();
            var material = await DbContext.Materials.FirstAsync();

            DbContext.PlayerMaterials.Add(new PlayerMaterial
            {
                PlayerId = player.Id,
                MaterialId = material.Id,
                Quantity = 42,
            });
            await DbContext.SaveChangesAsync();

            var view = await _sut.GetPlayer(player.Id, Ct);

            Assert.That(view.Materials.Any(m => m.Key == material.Key && m.Quantity == 42), Is.True);
        }

        [Test]
        public async Task EmptyHoldingsAreOmitted()
        {
            // A zero-quantity row is a leftover, not a holding — showing it would make an
            // empty inventory look full.
            var player = await DbContext.Players.FirstAsync();
            var material = await DbContext.Materials.FirstAsync();

            DbContext.PlayerMaterials.Add(new PlayerMaterial
            {
                PlayerId = player.Id,
                MaterialId = material.Id,
                Quantity = 0,
            });
            await DbContext.SaveChangesAsync();

            var view = await _sut.GetPlayer(player.Id, Ct);

            Assert.That(view.Materials.Any(m => m.Key == material.Key), Is.False);
        }

        [Test]
        public void AMissingPlayer_IsNotFound()
        {
            Assert.That(
                async () => await _sut.GetPlayer(999_999, Ct),
                Throws.TypeOf<NotFoundException>());
        }

        [Test]
        public async Task ReadingAPlayer_IsNotAudited()
        {
            // The trail records *changes*. Logging every read would bury the entries that
            // actually matter under routine support lookups.
            var player = await DbContext.Players.FirstAsync();
            var before = (await _sut.GetAuditTrail(100, Ct)).Count;

            await _sut.GetPlayer(player.Id, Ct);
            await _sut.SearchPlayers(_admin.Username, Ct);

            Assert.That((await _sut.GetAuditTrail(100, Ct)).Count, Is.EqualTo(before));
        }

        // ── Sprites (task 6, generalised) ───────────────────────────────

        [Test]
        public async Task AMaterialSpriteCanBeUploadedAndRead()
        {
            // The generalisation working: what items already had, now for materials.
            var material = await DbContext.Materials.FirstAsync();

            await _sut.UploadSprite(
                _admin.Id, SpriteOwner.Material, material.Id, Png(), "image/png", "ore.png", Ct);

            var sprite = await _sut.GetSprite(SpriteOwner.Material, material.Id, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(sprite, Is.Not.Null);
                Assert.That(sprite!.Value.ContentType, Is.EqualTo("image/png"));
            });
        }

        [Test]
        public async Task SpritesOfDifferentKinds_DoNotCollideOnId()
        {
            // The risk a polymorphic key introduces: item 1 and material 1 are different
            // things, and must not share a sprite.
            var item = await _sut.SaveItem(_admin.Id, NewItem("sprite_item"), Ct);
            var material = await DbContext.Materials.FirstAsync();

            await _sut.UploadSprite(
                _admin.Id, SpriteOwner.Item, item.Id, Png(16), "image/png", "item.png", Ct);
            await _sut.UploadSprite(
                _admin.Id, SpriteOwner.Material, material.Id, Png(64), "image/png", "material.png", Ct);

            var itemSprite = await DbContext.Sprites
                .FirstAsync(s => s.OwnerType == SpriteOwner.Item && s.OwnerId == item.Id);
            var materialSprite = await DbContext.Sprites
                .FirstAsync(s => s.OwnerType == SpriteOwner.Material && s.OwnerId == material.Id);

            Assert.Multiple(() =>
            {
                Assert.That(itemSprite.FileName, Is.EqualTo("item.png"));
                Assert.That(materialSprite.FileName, Is.EqualTo("material.png"));
            });
        }

        [Test]
        public async Task UploadingASpriteAgain_ReplacesIt()
        {
            var material = await DbContext.Materials.FirstAsync();

            await _sut.UploadSprite(
                _admin.Id, SpriteOwner.Material, material.Id, Png(16), "image/png", "first.png", Ct);
            await _sut.UploadSprite(
                _admin.Id, SpriteOwner.Material, material.Id, Png(64), "image/png", "second.png", Ct);

            var rows = await DbContext.Sprites
                .CountAsync(s => s.OwnerType == SpriteOwner.Material && s.OwnerId == material.Id);

            Assert.That(rows, Is.EqualTo(1));
        }

        [Test]
        public async Task ASpriteForSomethingThatDoesNotExist_IsRejected()
        {
            // A polymorphic key buys no referential integrity, so the check has to be
            // explicit — otherwise a typo'd id uploads a sprite that is invisible forever.
            Assert.That(
                async () => await _sut.UploadSprite(
                    _admin.Id, SpriteOwner.Material, 999_999, Png(), "image/png", "x.png", Ct),
                Throws.TypeOf<NotFoundException>());
        }

        [Test]
        public async Task AnInvalidSprite_IsRejectedAndNothingIsStored()
        {
            var material = await DbContext.Materials.FirstAsync();
            var html = "<html><script>alert(1)</script></html>"u8.ToArray();

            Assert.That(
                async () => await _sut.UploadSprite(
                    _admin.Id, SpriteOwner.Material, material.Id, html, "image/png", "x.png", Ct),
                Throws.TypeOf<BadRequestException>());

            Assert.That(
                await DbContext.Sprites.AnyAsync(s => s.OwnerId == material.Id),
                Is.False);
        }

        [Test]
        public async Task MaterialsReportWhetherTheyHaveArtwork()
        {
            var material = await DbContext.Materials.FirstAsync();

            await _sut.UploadSprite(
                _admin.Id, SpriteOwner.Material, material.Id, Png(), "image/png", "ore.png", Ct);

            var listed = (await _sut.GetMaterials(Ct)).First(m => m.Id == material.Id);

            Assert.That(listed.HasSprite, Is.True);
        }

        [Test]
        public async Task SpriteMutations_AreAudited()
        {
            var material = await DbContext.Materials.FirstAsync();

            await _sut.UploadSprite(
                _admin.Id, SpriteOwner.Material, material.Id, Png(), "image/png", "ore.png", Ct);
            await _sut.DeleteSprite(_admin.Id, SpriteOwner.Material, material.Id, Ct);

            var actions = (await _sut.GetAuditTrail(50, Ct))
                .Where(e => e.EntityType == "Material")
                .Select(e => e.Action)
                .ToList();

            Assert.That(actions, Does.Contain("SpriteUploaded").And.Contains("SpriteDeleted"));
        }

        // ── Player adjustments (task 9, write) ──────────────────────────

        [Test]
        public async Task CoinCanBeGrantedAndRemoved()
        {
            var player = await DbContext.Players.FirstAsync();

            var granted = await _sut.AdjustPlayerCoin(_admin.Id, new AdjustPlayerRequest
            {
                PlayerId = player.Id,
                Delta = 500,
                Reason = "Support ticket 42",
            }, Ct);

            Assert.That(granted.Coin, Is.EqualTo(500));

            var removed = await _sut.AdjustPlayerCoin(_admin.Id, new AdjustPlayerRequest
            {
                PlayerId = player.Id,
                Delta = -200,
                Reason = "Reverting an over-grant",
            }, Ct);

            Assert.That(removed.Coin, Is.EqualTo(300));
        }

        [Test]
        public async Task RemovingMoreCoinThanHeld_ClampsAtZero()
        {
            // No game rule produces a negative balance and nothing downstream expects one —
            // EconomyService assumes a purse is empty at worst, not overdrawn.
            var player = await DbContext.Players.FirstAsync();

            player.Coin = 100;
            await DbContext.SaveChangesAsync();

            var result = await _sut.AdjustPlayerCoin(_admin.Id, new AdjustPlayerRequest
            {
                PlayerId = player.Id,
                Delta = -999_999,
                Reason = "Clearing a bad grant",
            }, Ct);

            Assert.That(result.Coin, Is.Zero);
        }

        [Test]
        public async Task AnAdjustmentWithoutAReason_IsRejected()
        {
            // The point of an audit entry is answering "why did this happen" months later,
            // and a bare delta does not.
            var player = await DbContext.Players.FirstAsync();

            Assert.Multiple(() =>
            {
                Assert.That(async () => await _sut.AdjustPlayerCoin(_admin.Id, new AdjustPlayerRequest
                {
                    PlayerId = player.Id,
                    Delta = 100,
                    Reason = "  ",
                }, Ct), Throws.TypeOf<BadRequestException>());

                Assert.That(async () => await _sut.AdjustPlayerCoin(_admin.Id, new AdjustPlayerRequest
                {
                    PlayerId = player.Id,
                    Delta = 100,
                    Reason = "",
                }, Ct), Throws.TypeOf<BadRequestException>());
            });
        }

        [Test]
        public async Task AZeroAdjustment_IsRejected()
        {
            var player = await DbContext.Players.FirstAsync();

            Assert.That(
                async () => await _sut.AdjustPlayerCoin(_admin.Id, new AdjustPlayerRequest
                {
                    PlayerId = player.Id,
                    Delta = 0,
                    Reason = "Nothing",
                }, Ct),
                Throws.TypeOf<BadRequestException>());
        }

        [Test]
        public async Task AMaterialCanBeGrantedToSomeoneWhoHasNeverHeldIt()
        {
            // The common support case, so the row is created rather than the request refused.
            var player = await DbContext.Players.FirstAsync();
            var material = await DbContext.Materials.FirstAsync();

            var result = await _sut.AdjustPlayerMaterial(_admin.Id, new AdjustPlayerMaterialRequest
            {
                PlayerId = player.Id,
                MaterialId = material.Id,
                Delta = 25,
                Reason = "Lost to a sync bug",
            }, Ct);

            Assert.That(result.Materials.Any(m => m.Key == material.Key && m.Quantity == 25),
                Is.True);
        }

        [Test]
        public async Task TakingAnEquippedItemToZero_UnequipsIt()
        {
            // Otherwise a modifier keeps being read from something the player no longer
            // owns — §4.3's rule in reverse.
            var player = await DbContext.Players.FirstAsync();
            var item = await _sut.SaveItem(_admin.Id, NewItem("removable"), Ct);

            DbContext.PlayerItems.Add(new PlayerItem
            {
                PlayerId = player.Id,
                ItemId = item.Id,
                Quantity = 1,
                IsEquipped = true,
            });
            await DbContext.SaveChangesAsync();

            await _sut.AdjustPlayerItem(_admin.Id, new AdjustPlayerItemRequest
            {
                PlayerId = player.Id,
                ItemId = item.Id,
                Delta = -1,
                Reason = "Granted in error",
            }, Ct);

            DbContext.ChangeTracker.Clear();

            var row = await DbContext.PlayerItems
                .FirstAsync(pi => pi.PlayerId == player.Id && pi.ItemId == item.Id);

            Assert.Multiple(() =>
            {
                Assert.That(row.Quantity, Is.Zero);
                Assert.That(row.IsEquipped, Is.False, "an unowned item cannot stay equipped");
            });
        }

        [Test]
        public async Task EveryAdjustment_RecordsBeforeAfterAndReason()
        {
            // "What was it, what is it now, and why" — the three things actually asked of an
            // audit trail after a balance change goes wrong.
            var player = await DbContext.Players.FirstAsync();

            await _sut.AdjustPlayerCoin(_admin.Id, new AdjustPlayerRequest
            {
                PlayerId = player.Id,
                Delta = 750,
                Reason = "Compensation for ticket 99",
            }, Ct);

            var entry = (await _sut.GetAuditTrail(1, Ct)).Single();

            Assert.Multiple(() =>
            {
                Assert.That(entry.Action, Is.EqualTo("CoinAdjusted"));
                Assert.That(entry.Detail, Does.Contain("+750"));
                Assert.That(entry.Detail, Does.Contain("750"), "the resulting balance");
                Assert.That(entry.Detail, Does.Contain("ticket 99"), "the reason, verbatim");
            });
        }

        [Test]
        public async Task AdjustingAMissingPlayer_IsNotFound()
        {
            Assert.That(
                async () => await _sut.AdjustPlayerCoin(_admin.Id, new AdjustPlayerRequest
                {
                    PlayerId = 999_999,
                    Delta = 100,
                    Reason = "Testing",
                }, Ct),
                Throws.TypeOf<NotFoundException>());
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
