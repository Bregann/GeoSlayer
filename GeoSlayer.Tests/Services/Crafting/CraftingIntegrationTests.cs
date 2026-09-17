using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.DTOs.Crafting.Responses;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Exceptions;
using GeoSlayer.Domain.Services.Crafting;
using GeoSlayer.Domain.Services.Materials;
using GeoSlayer.Domain.Services.Progression;
using GeoSlayer.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Tests.Services.Crafting
{
    /// <summary>
    /// Stage 06 criteria 2, 3, 4, 5, 7 and 8, against a real database.
    /// </summary>
    [TestFixture]
    public class CraftingIntegrationTests : DatabaseIntegrationTestBase
    {
        private ProgressionService _progression = null!;
        private MaterialService _materials = null!;
        private CraftingService _sut = null!;
        private Player _player = null!;

        private static CancellationToken Ct => CancellationToken.None;

        protected override async Task CustomSetUp()
        {
            var (_, player) = await TestDatabaseSeedHelper.SeedMinimalData(DbContext);
            _player = player;

            await TestDatabaseSeedHelper.SeedProgressionDefinitions(DbContext);
            await TestDatabaseSeedHelper.SeedMaterialDefinitions(DbContext);
            await TestDatabaseSeedHelper.SeedSkillDefinitions(DbContext);
            await TestDatabaseSeedHelper.SeedCraftingDefinitions(DbContext);

            _progression = TestDatabaseSeedHelper.CreateProgressionService(DbContext);
            _materials = TestDatabaseSeedHelper.CreateMaterialService(DbContext);

            await _progression.EnsureStartingUnlocks(_player.Id, Ct);

            _sut = TestDatabaseSeedHelper.CreateCraftingService(DbContext, _progression, _materials);
        }

        private async Task SetSkillLevel(SkillType skill, int level)
        {
            var row = await DbContext.PlayerSkills
                .FirstAsync(s => s.PlayerId == _player.Id && s.SkillType == skill);

            row.Level = level;
            row.Xp = XpCurve.XpForLevel(level);
            await DbContext.SaveChangesAsync();
        }

        private async Task GiveMaterial(string key, int quantity)
        {
            var material = await DbContext.Materials.FirstAsync(m => m.Key == key);

            var row = await DbContext.PlayerMaterials
                .FirstOrDefaultAsync(pm => pm.PlayerId == _player.Id && pm.MaterialId == material.Id);

            if (row is null)
            {
                row = new PlayerMaterial { PlayerId = _player.Id, MaterialId = material.Id };
                DbContext.PlayerMaterials.Add(row);
            }

            row.Quantity += quantity;
            await DbContext.SaveChangesAsync();
        }

        private async Task<long> Held(string key)
        {
            var material = await DbContext.Materials.FirstAsync(m => m.Key == key);

            return await DbContext.PlayerMaterials
                .Where(pm => pm.PlayerId == _player.Id && pm.MaterialId == material.Id)
                .Select(pm => pm.Quantity)
                .FirstOrDefaultAsync();
        }

        /// <summary>Move a craft's completion into the past, to simulate waiting.</summary>
        private async Task CompleteNow(int craftId)
        {
            var craft = await DbContext.PlayerCrafts.FirstAsync(c => c.Id == craftId);
            craft.CompletesUtc = DateTime.UtcNow.AddSeconds(-1);
            await DbContext.SaveChangesAsync();
        }

        // ── Criterion 2: inputs consumed at queue time, refunded on cancel ──

        [Test]
        public async Task QueueingConsumesInputsImmediately()
        {
            await GiveMaterial("wild_grass", 20);

            var before = await Held("wild_grass");
            await _sut.QueueCraft(_player.Id, "twine", Ct);
            var after = await Held("wild_grass");

            // Twine costs 5 Wild Grass. Consuming at completion instead would let a player
            // queue everything and spend the materials elsewhere first.
            Assert.That(after, Is.EqualTo(before - 5));
        }

        [Test]
        public async Task CancellingRefundsInputsInFull()
        {
            await GiveMaterial("wild_grass", 20);

            var before = await Held("wild_grass");
            var craft = await _sut.QueueCraft(_player.Id, "twine", Ct);

            await _sut.CancelCraft(_player.Id, craft.Id, Ct);

            Assert.Multiple(async () =>
            {
                Assert.That(await Held("wild_grass"), Is.EqualTo(before), "a cancel must refund fully");
                Assert.That(await DbContext.PlayerCrafts.CountAsync(c => c.PlayerId == _player.Id),
                    Is.Zero);
            });
        }

        [Test]
        public async Task QueueingWithoutMaterials_IsRejected()
        {
            await Assert.ThatAsync(
                () => _sut.QueueCraft(_player.Id, "twine", Ct),
                Throws.TypeOf<BadRequestException>());
        }

        [Test]
        public async Task QueueingWithoutMaterials_ConsumesNothing()
        {
            // Two of the five needed — the check must be all-or-nothing, not partial.
            await GiveMaterial("wild_grass", 2);

            try { await _sut.QueueCraft(_player.Id, "twine", Ct); } catch (BadRequestException) { }

            Assert.That(await Held("wild_grass"), Is.EqualTo(2), "a failed queue must not consume");
        }

        [Test]
        public async Task TheQueueLimit_IsEnforced()
        {
            await GiveMaterial("wild_grass", 100);

            await _sut.QueueCraft(_player.Id, "twine", Ct);

            // The base limit is one concurrent craft until a Craft Slot rank is bought.
            await Assert.ThatAsync(
                () => _sut.QueueCraft(_player.Id, "twine", Ct),
                Throws.TypeOf<BadRequestException>());
        }

        // ── Criterion 3: lazy completion ────────────────────────────────

        [Test]
        public async Task ACraftCompletesAfterItsDuration()
        {
            await GiveMaterial("wild_grass", 20);

            var craft = await _sut.QueueCraft(_player.Id, "twine", Ct);

            var early = await _sut.CollectCompletedCrafts(_player.Id, Ct);
            Assert.That(early.HasCollection, Is.False, "nothing should collect before it finishes");

            await CompleteNow(craft.Id);

            var collected = await _sut.CollectCompletedCrafts(_player.Id, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(collected.HasCollection, Is.True);
                Assert.That(collected.CompletedRecipes, Contains.Item("Twine"));
            });
        }

        [Test]
        public async Task CollectingTwice_DoesNotPayTwice()
        {
            await GiveMaterial("wild_grass", 20);

            var craft = await _sut.QueueCraft(_player.Id, "twine", Ct);
            await CompleteNow(craft.Id);

            await _sut.CollectCompletedCrafts(_player.Id, Ct);
            var second = await _sut.CollectCompletedCrafts(_player.Id, Ct);

            Assert.That(second.HasCollection, Is.False);
        }

        [Test]
        public async Task CollectingProducesTheOutputMaterial()
        {
            await GiveMaterial("wild_grass", 20);

            var before = await Held("fibre");

            var craft = await _sut.QueueCraft(_player.Id, "twine", Ct);
            await CompleteNow(craft.Id);
            await _sut.CollectCompletedCrafts(_player.Id, Ct);

            // Twine outputs 2 Plant Fibre.
            Assert.That(await Held("fibre"), Is.EqualTo(before + 2));
        }

        [Test]
        public async Task CollectingProducesTheOutputItem()
        {
            await SetSkillLevel(SkillType.Foraging, 10);
            await GiveMaterial("scrap", 20);
            await GiveMaterial("wild_grass", 20);

            var craft = await _sut.QueueCraft(_player.Id, "foraging_knife", Ct);
            await CompleteNow(craft.Id);

            var collected = await _sut.CollectCompletedCrafts(_player.Id, Ct);

            Assert.That(collected.Items.Any(i => i.Key == "foraging_knife"), Is.True);
        }

        // ── Criterion 8: crafting grants XP at the configured ratio ─────

        [Test]
        public async Task CraftingGrantsSkillAndAdventurerXp()
        {
            await GiveMaterial("wild_grass", 20);

            var craft = await _sut.QueueCraft(_player.Id, "twine", Ct);
            await CompleteNow(craft.Id);

            var collected = await _sut.CollectCompletedCrafts(_player.Id, Ct);

            Assert.Multiple(() =>
            {
                Assert.That(collected.SkillXpEarned, Is.GreaterThan(0));
                Assert.That(collected.AdventurerXpEarned, Is.GreaterThan(0),
                    "the flat cut should also land");
            });
        }

        // ── Criteria 4 & 5: lock reasons ────────────────────────────────

        [Test]
        public async Task ARecipeForALockedSkill_IsNotCraftable()
        {
            var list = await _sut.GetRecipes(_player.Id, Ct);

            // Every seeded recipe is Foraging, which is unlocked — so assert the mechanism
            // directly by locking one.
            var recipe = await DbContext.Recipes.FirstAsync(r => r.Key == "twine");
            recipe.SkillType = SkillType.Mining;
            await DbContext.SaveChangesAsync();

            list = await _sut.GetRecipes(_player.Id, Ct);
            var twine = list.Recipes.First(r => r.Key == "twine");

            Assert.Multiple(() =>
            {
                Assert.That(twine.CanCraft, Is.False);
                Assert.That(twine.LockReason, Is.EqualTo(RecipeLockReason.SkillLocked));
            });
        }

        [Test]
        public async Task ARecipeAboveLevel_IsLevelLocked()
        {
            var list = await _sut.GetRecipes(_player.Id, Ct);
            var lens = list.Recipes.First(r => r.Key == "surveyors_lens");

            Assert.Multiple(() =>
            {
                Assert.That(lens.CanCraft, Is.False);
                Assert.That(lens.LockReason, Is.EqualTo(RecipeLockReason.LevelLocked));
                Assert.That(lens.LockText, Does.Contain("level 15"));
            });
        }

        [Test]
        public async Task ATravelGatedRecipe_IsDistinguishedFromLevelGated()
        {
            // The Reliquary needs Blessed Water, a unique POI-only material. §4.2 wants this
            // visually distinct: "keep playing" and "go somewhere new" are different answers.
            await SetSkillLevel(SkillType.Foraging, 25);
            await GiveMaterial("timber_rough", 50);

            var list = await _sut.GetRecipes(_player.Id, Ct);
            var reliquary = list.Recipes.First(r => r.Key == "relic_reliquary");

            Assert.Multiple(() =>
            {
                Assert.That(reliquary.LockReason, Is.EqualTo(RecipeLockReason.TravelGated));
                Assert.That(reliquary.LockText, Does.Contain("rare places"));
                Assert.That(reliquary.Inputs.Any(i => i.IsTravelGated), Is.True);
            });
        }

        [Test]
        public async Task AMissingOrdinaryMaterial_IsNotTravelGated()
        {
            // Contrast with the test above: a shortage the player can simply go and gather
            // must not be reported as travel-gated.
            var list = await _sut.GetRecipes(_player.Id, Ct);
            var twine = list.Recipes.First(r => r.Key == "twine");

            Assert.That(twine.LockReason, Is.EqualTo(RecipeLockReason.MissingMaterials));
        }

        [Test]
        public async Task LockedRecipesAreStillReturned()
        {
            // §4.2: show locked recipes greyed with the requirement visible, so the player
            // can see what they are working toward. Filtering them out loses the roadmap.
            var list = await _sut.GetRecipes(_player.Id, Ct);

            Assert.That(list.Recipes.Count, Is.GreaterThan(1));
            Assert.That(list.Recipes.Any(r => !r.CanCraft), Is.True);
        }

        // ── Criterion 7: a building measurably raises stack caps ────────

        [Test]
        public async Task AStorehouse_MeasurablyRaisesStackCaps()
        {
            var storehouse = await DbContext.Items.FirstAsync(i => i.Key == "storehouse");

            await GiveMaterial("scrap", 5);

            var before = (await _materials.GetInventory(_player.Id, Ct))
                .Categories.SelectMany(c => c.Items)
                .First(i => i.Key == "scrap")
                .StackCap;

            DbContext.PlayerItems.Add(new PlayerItem
            {
                PlayerId = _player.Id,
                ItemId = storehouse.Id,
                Quantity = 1,
                IsEquipped = true,
                AcquiredUtc = DateTime.UtcNow,
            });
            await DbContext.SaveChangesAsync();

            var after = (await _materials.GetInventory(_player.Id, Ct))
                .Categories.SelectMany(c => c.Items)
                .First(i => i.Key == "scrap")
                .StackCap;

            Assert.That(after, Is.GreaterThan(before),
                "an equipped building that changes no behaviour is a bug (§4.3)");
        }

        [Test]
        public async Task AStorehouse_LetsMoreMaterialFitBeforeOverflowing()
        {
            var storehouse = await DbContext.Items.FirstAsync(i => i.Key == "storehouse");
            var scrap = await DbContext.Materials.FirstAsync(m => m.Key == "scrap");

            DbContext.PlayerItems.Add(new PlayerItem
            {
                PlayerId = _player.Id,
                ItemId = storehouse.Id,
                Quantity = 1,
                IsEquipped = true,
                AcquiredUtc = DateTime.UtcNow,
            });
            await DbContext.SaveChangesAsync();

            // Grant exactly the base cap. Without the Storehouse this would fill the stack
            // and overflow; with it there should be room to spare.
            var gains = await _materials.GrantMaterials(
                _player.Id, new Dictionary<int, int> { [scrap.Id] = scrap.StackCap }, Ct);

            var gain = gains.First(g => g.MaterialId == scrap.Id);

            Assert.Multiple(() =>
            {
                Assert.That(gain.Quantity, Is.EqualTo(scrap.StackCap), "all of it should fit");
                Assert.That(gain.OverflowConvertedToDust, Is.Zero);
            });
        }

        // ── Gear modifiers are read, not merely stored (§4.3) ───────────

        [Test]
        public async Task EquippingGear_ReportsItsModifierTotal()
        {
            var satchel = await DbContext.Items.FirstAsync(i => i.Key == "foragers_satchel");

            DbContext.PlayerItems.Add(new PlayerItem
            {
                PlayerId = _player.Id,
                ItemId = satchel.Id,
                Quantity = 1,
                IsEquipped = true,
                AcquiredUtc = DateTime.UtcNow,
            });
            await DbContext.SaveChangesAsync();

            var total = await _sut.GetModifierTotal(_player.Id, ItemModifier.SkillXpPercent, Ct);

            Assert.That(total, Is.EqualTo(0.2).Within(0.0001));
        }

        [Test]
        public async Task UnequippedGear_ContributesNothing()
        {
            var satchel = await DbContext.Items.FirstAsync(i => i.Key == "foragers_satchel");

            DbContext.PlayerItems.Add(new PlayerItem
            {
                PlayerId = _player.Id,
                ItemId = satchel.Id,
                Quantity = 1,
                IsEquipped = false,
                AcquiredUtc = DateTime.UtcNow,
            });
            await DbContext.SaveChangesAsync();

            var total = await _sut.GetModifierTotal(_player.Id, ItemModifier.SkillXpPercent, Ct);

            Assert.That(total, Is.Zero);
        }

        [Test]
        public async Task EquippingASlot_UnequipsWhatWasThere()
        {
            // Slot competition is what keeps gear conditional rather than merely additive
            // (§4.3) — two body items must not both apply.
            var satchel = await DbContext.Items.FirstAsync(i => i.Key == "foragers_satchel");

            var second = new Item
            {
                Key = "test_body",
                Name = "Test Body",
                Description = "",
                Kind = ItemKind.Gear,
                Slot = ItemSlot.Body,
                Modifier = ItemModifier.SkillXpPercent,
                ModifierValue = 0.5,
                Tier = 1,
            };

            DbContext.Items.Add(second);
            await DbContext.SaveChangesAsync();

            var first = new PlayerItem
            {
                PlayerId = _player.Id,
                ItemId = satchel.Id,
                Quantity = 1,
                IsEquipped = true,
                AcquiredUtc = DateTime.UtcNow,
            };

            var other = new PlayerItem
            {
                PlayerId = _player.Id,
                ItemId = second.Id,
                Quantity = 1,
                IsEquipped = false,
                AcquiredUtc = DateTime.UtcNow,
            };

            DbContext.PlayerItems.AddRange(first, other);
            await DbContext.SaveChangesAsync();

            await _sut.SetEquipped(_player.Id, other.Id, true, null, Ct);

            var total = await _sut.GetModifierTotal(_player.Id, ItemModifier.SkillXpPercent, Ct);

            Assert.That(total, Is.EqualTo(0.5).Within(0.0001),
                "only one body item should apply");
        }

        [Test]
        public async Task ABuildingRequiresAClaim()
        {
            var storehouse = await DbContext.Items.FirstAsync(i => i.Key == "storehouse");

            var owned = new PlayerItem
            {
                PlayerId = _player.Id,
                ItemId = storehouse.Id,
                Quantity = 1,
                IsEquipped = false,
                AcquiredUtc = DateTime.UtcNow,
            };

            DbContext.PlayerItems.Add(owned);
            await DbContext.SaveChangesAsync();

            await Assert.ThatAsync(
                () => _sut.SetEquipped(_player.Id, owned.Id, true, null, Ct),
                Throws.TypeOf<BadRequestException>());
        }

        // ── Recipe data integrity ───────────────────────────────────────

        [Test]
        public async Task EveryJsonRecipe_ResolvedItsMaterialsAndOutput()
        {
            // A recipe naming a material that does not exist is skipped by the seeder rather
            // than throwing, which is right for a typo in balance data but silent. This makes
            // it loud.
            var seeded = await DbContext.Recipes.CountAsync();

            Assert.That(seeded, Is.EqualTo(RecipeSeedData.Recipes.Count),
                "a recipe was skipped — check its material and item keys against the seed data");
        }

        [Test]
        public async Task EveryJsonItem_WasSeeded()
        {
            var seeded = await DbContext.Items.CountAsync();

            Assert.That(seeded, Is.EqualTo(RecipeSeedData.Items.Count));
        }
    }
}
