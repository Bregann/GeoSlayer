using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.DTOs.Admin.Requests;
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
