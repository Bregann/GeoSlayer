using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Services.Crafting
{
    /// <summary>
    /// Loads recipes and items from the embedded JSON (DESIGN.md §4.2).
    ///
    /// <para>JSON rather than hardcoded C# because balance is retuned constantly and must not
    /// need a redeploy. Embedded rather than a content file so the API has nothing to
    /// deploy alongside it.</para>
    /// </summary>
    public static class RecipeSeedData
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,

            // Enums are written as names in the JSON ("Gear", "Foraging") rather than
            // ordinals: an ordinal in a balance file is unreadable and silently wrong if the
            // enum is ever reordered.
            Converters = { new JsonStringEnumConverter() },
        };

        public class ItemDefinition
        {
            public string Key { get; set; } = null!;
            public string Name { get; set; } = null!;
            public string Description { get; set; } = "";
            public ItemKind Kind { get; set; }
            public ItemSlot Slot { get; set; }
            public ItemModifier Modifier { get; set; }
            public double ModifierValue { get; set; }
            public ItemModifier? SecondaryModifier { get; set; }
            public double SecondaryModifierValue { get; set; }
            public int Tier { get; set; } = 1;
        }

        public class RecipeInputDefinition
        {
            public string Material { get; set; } = null!;
            public int Quantity { get; set; }
        }

        public class RecipeDefinition
        {
            public string Key { get; set; } = null!;
            public string Name { get; set; } = null!;
            public string Description { get; set; } = "";
            public SkillType Skill { get; set; }
            public int LevelRequired { get; set; } = 1;
            public double DurationSeconds { get; set; }
            public double XpReward { get; set; }

            /// <summary>Material key, when this recipe outputs a material.</summary>
            public string? OutputMaterial { get; set; }

            /// <summary>Item key, when this recipe outputs an item.</summary>
            public string? OutputItem { get; set; }

            public int OutputQuantity { get; set; } = 1;

            public List<RecipeInputDefinition> Inputs { get; set; } = [];
        }

        private class ItemsFile { public List<ItemDefinition> Items { get; set; } = []; }
        private class RecipesFile { public List<RecipeDefinition> Recipes { get; set; } = []; }

        private static readonly Lazy<List<ItemDefinition>> LazyItems =
            new(() => Load<ItemsFile>("items.json").Items);

        private static readonly Lazy<List<RecipeDefinition>> LazyRecipes =
            new(() => Load<RecipesFile>("recipes.json").Recipes);

        public static IReadOnlyList<ItemDefinition> Items => LazyItems.Value;

        public static IReadOnlyList<RecipeDefinition> Recipes => LazyRecipes.Value;

        private static T Load<T>(string fileName) where T : new()
        {
            var assembly = Assembly.GetExecutingAssembly();

            var resource = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

            if (resource is null)
                throw new InvalidOperationException(
                    $"Embedded resource '{fileName}' not found. Check the EmbeddedResource glob in the csproj.");

            using var stream = assembly.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException($"Could not open '{resource}'.");

            return JsonSerializer.Deserialize<T>(stream, Options)
                ?? throw new InvalidOperationException($"'{fileName}' deserialised to null.");
        }
    }
}
