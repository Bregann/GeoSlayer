using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Services.Admin;

namespace GeoSlayer.Tests.Services.Admin
{
    /// <summary>
    /// Recipe rules (Stage 18 task 7).
    ///
    /// <para>The split these tests are really about: structurally broken recipes are
    /// refused, questionable ones are only flagged. An admin mid-tune must be able to save
    /// something temporarily unattractive; they must not be able to save something that
    /// cannot function.</para>
    /// </summary>
    [TestFixture]
    public class RecipeValidationTests
    {
        private static Recipe Make(
            string key = "smith_thing",
            int? outputMaterial = null,
            int? outputItem = 1,
            double seconds = 600,
            double xp = 70,
            int outputQuantity = 1,
            int level = 1) => new()
            {
                Key = key,
                Name = "A Thing",
                Description = "",
                SkillType = SkillType.Smithing,
                LevelRequired = level,
                DurationSeconds = seconds,
                XpReward = xp,
                OutputMaterialId = outputMaterial,
                OutputItemId = outputItem,
                OutputQuantity = outputQuantity,
            };

        private static List<RecipeInput> Inputs(params (int MaterialId, int Quantity)[] rows) =>
            [.. rows.Select(r => new RecipeInput { MaterialId = r.MaterialId, Quantity = r.Quantity })];

        private static List<Recipe> NoOthers() => [];

        // ── Structurally sound ──────────────────────────────────────────

        [Test]
        public void AWellFormedRecipe_IsAccepted()
        {
            Assert.That(
                RecipeValidation.Reject(Make(), Inputs((5, 3)), NoOthers()),
                Is.Null);
        }

        // ── Identity ────────────────────────────────────────────────────

        [Test]
        public void ADuplicateKey_IsRejected()
        {
            var existing = new List<Recipe> { Make("smith_thing") };

            Assert.That(
                RecipeValidation.Reject(Make("smith_thing"), Inputs((5, 3)), existing),
                Is.Not.Null);
        }

        [Test]
        public void AMissingKeyOrName_IsRejected()
        {
            var noKey = Make();
            noKey.Key = " ";

            var noName = Make();
            noName.Name = "";

            Assert.Multiple(() =>
            {
                Assert.That(RecipeValidation.Reject(noKey, Inputs((5, 3)), NoOthers()), Is.Not.Null);
                Assert.That(RecipeValidation.Reject(noName, Inputs((5, 3)), NoOthers()), Is.Not.Null);
            });
        }

        // ── Output ──────────────────────────────────────────────────────

        [Test]
        public void ARecipeProducingNothing_IsRejected()
        {
            // A timer that consumes materials and gives back nothing is indistinguishable
            // from a bug when a player hits it.
            var nothing = Make(outputMaterial: null, outputItem: null);

            Assert.That(RecipeValidation.Reject(nothing, Inputs((5, 3)), NoOthers()), Is.Not.Null);
        }

        [Test]
        public void ARecipeProducingBoth_IsRejected()
        {
            var both = Make(outputMaterial: 2, outputItem: 1);

            Assert.That(RecipeValidation.Reject(both, Inputs((5, 3)), NoOthers()), Is.Not.Null);
        }

        [Test]
        public void ANonPositiveOutputQuantity_IsRejected()
        {
            Assert.Multiple(() =>
            {
                Assert.That(RecipeValidation.Reject(Make(outputQuantity: 0), Inputs((5, 3)), NoOthers()),
                    Is.Not.Null);
                Assert.That(RecipeValidation.Reject(Make(outputQuantity: -1), Inputs((5, 3)), NoOthers()),
                    Is.Not.Null);
            });
        }

        // ── Inputs ──────────────────────────────────────────────────────

        [Test]
        public void ARecipeWithNoInputs_IsRejected()
        {
            // Otherwise it produces something from nothing, on a timer.
            Assert.That(RecipeValidation.Reject(Make(), Inputs(), NoOthers()), Is.Not.Null);
        }

        [Test]
        public void ADuplicatedInput_IsRejected()
        {
            // The cost becomes ambiguous — one of the rows silently wins.
            Assert.That(
                RecipeValidation.Reject(Make(), Inputs((5, 3), (5, 2)), NoOthers()),
                Is.Not.Null);
        }

        [Test]
        public void AnInputWithNoQuantity_IsRejected()
        {
            Assert.That(
                RecipeValidation.Reject(Make(), Inputs((5, 0)), NoOthers()),
                Is.Not.Null);
        }

        [Test]
        public void ARecipeConsumingItsOwnOutput_IsRejected()
        {
            // An infinite loop with a timer attached.
            var loop = Make(outputMaterial: 7, outputItem: null);

            Assert.That(
                RecipeValidation.Reject(loop, Inputs((7, 1)), NoOthers()),
                Is.Not.Null);
        }

        // ── Timing and gating ───────────────────────────────────────────

        [Test]
        public void AnInstantCraft_IsRejected()
        {
            // §4.2: crafting is time-gated, not tap-gated. Zero seconds is a tap.
            Assert.Multiple(() =>
            {
                Assert.That(RecipeValidation.Reject(Make(seconds: 0), Inputs((5, 3)), NoOthers()),
                    Is.Not.Null);
                Assert.That(RecipeValidation.Reject(Make(seconds: -10), Inputs((5, 3)), NoOthers()),
                    Is.Not.Null);
            });
        }

        [Test]
        public void NegativeXpOrLevel_IsRejected()
        {
            Assert.Multiple(() =>
            {
                Assert.That(RecipeValidation.Reject(Make(xp: -1), Inputs((5, 3)), NoOthers()),
                    Is.Not.Null);
                Assert.That(RecipeValidation.Reject(Make(level: 0), Inputs((5, 3)), NoOthers()),
                    Is.Not.Null);
            });
        }

        // ── Warnings: flagged, never blocked ────────────────────────────

        [Test]
        public void ALossMakingRecipe_IsWarnedAboutButNotRefused()
        {
            // §5D.1 expects produced goods to beat their parts. But an admin mid-tune should
            // be able to save a half-finished recipe and come back to it.
            var warnings = RecipeValidation.Warn(Make(), inputCost: 500, outputValue: 100);

            Assert.That(warnings, Is.Not.Empty);
            Assert.That(warnings.Any(w => w.Contains("loses money")), Is.True);

            // The same recipe is still structurally saveable.
            Assert.That(RecipeValidation.Reject(Make(), Inputs((5, 3)), NoOthers()), Is.Null);
        }

        [Test]
        public void AProfitableRecipe_IsNotWarnedAbout()
        {
            var warnings = RecipeValidation.Warn(Make(), inputCost: 100, outputValue: 500);

            Assert.That(warnings.Any(w => w.Contains("loses money")), Is.False);
        }

        [Test]
        public void BreakingEvenCountsAsALoss()
        {
            // Equal value still means the craft time bought nothing.
            var warnings = RecipeValidation.Warn(Make(), inputCost: 200, outputValue: 200);

            Assert.That(warnings.Any(w => w.Contains("loses money")), Is.True);
        }

        [Test]
        public void ALongCraftForAlmostNoXp_IsWarnedAbout()
        {
            var warnings = RecipeValidation.Warn(Make(seconds: 7200, xp: 5), 10, 100);

            Assert.That(warnings.Any(w => w.Contains("long wait")), Is.True);
        }

        [Test]
        public void AVeryShortCraft_IsWarnedAbout()
        {
            // Legal, but it undercuts §4.2's whole framing.
            var warnings = RecipeValidation.Warn(Make(seconds: 5), 10, 100);

            Assert.That(warnings.Any(w => w.Contains("instant")), Is.True);
        }

        [Test]
        public void AReasonableRecipe_ProducesNoWarnings()
        {
            Assert.That(RecipeValidation.Warn(Make(), inputCost: 50, outputValue: 400), Is.Empty);
        }

        [Test]
        public void UnpricedInputs_DoNotTriggerALossWarning()
        {
            // Zero cost means "not known", not "free" — warning on it would fire on every
            // recipe whose inputs were not priced yet.
            Assert.That(
                RecipeValidation.Warn(Make(), inputCost: 0, outputValue: 0).Any(w => w.Contains("loses money")),
                Is.False);
        }

        // ── Cost calculation ────────────────────────────────────────────

        [Test]
        public void CostOf_UsesTheSamePricingAsTheShop()
        {
            // The warning must not be able to disagree with what a player would be paid.
            var ore = new Material
            {
                Key = "ore",
                Name = "Ore",
                Tier = 3,
                Category = MaterialCategory.Mined,
            };

            var cost = RecipeValidation.CostOf([(ore, 4)]);

            Assert.That(cost,
                Is.EqualTo(Domain.Services.Economy.CoinPricing.UnitPrice(3, MaterialCategory.Mined) * 4));
        }

        [Test]
        public void CostOf_WithNoInputs_IsZero()
        {
            Assert.That(RecipeValidation.CostOf([]), Is.Zero);
        }
    }
}
