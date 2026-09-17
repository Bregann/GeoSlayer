using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Services.Admin;

namespace GeoSlayer.Tests.Services.Admin
{
    /// <summary>
    /// §4.3's rule, made checkable: every <see cref="ItemModifier"/> must be read by the
    /// system it names.
    ///
    /// <para>The rule has held so far because the enum is small enough to audit by hand. An
    /// admin interface offering modifiers from a dropdown removes that safety, so these
    /// tests take over the job.</para>
    /// </summary>
    [TestFixture]
    public class ModifierReaderTests
    {
        private static string FindRepositoryRoot()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "GeoSlayer.sln")))
            {
                dir = dir.Parent;
            }

            Assert.That(dir, Is.Not.Null, "could not locate the repository root");

            return dir!.FullName;
        }

        [Test]
        public void EveryModifier_DeclaresAReader()
        {
            // The §4.3 rule stated directly. A modifier with no reader is an item that
            // changes no behaviour, which the design calls a bug.
            var undeclared = Enum.GetValues<ItemModifier>()
                .Where(m => !ModifierReaders.IsRead(m))
                .ToList();

            Assert.That(undeclared, Is.Empty,
                $"modifiers with no declared reader: {string.Join(", ", undeclared)}");
        }

        [Test]
        public void EveryDeclaredReader_ActuallyExists()
        {
            // The claim has to be verified or this file is just a second place to forget.
            // Each named type must exist somewhere under Services.
            var root = FindRepositoryRoot();
            var servicesDir = Path.Combine(root, "GeoSlayer.Domain", "Services");

            Assert.That(Directory.Exists(servicesDir), Is.True);

            var sourceFileNames = Directory
                .GetFiles(servicesDir, "*.cs", SearchOption.AllDirectories)
                .Select(Path.GetFileNameWithoutExtension)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missing = new List<string>();

            foreach (var (modifier, entry) in ModifierReaders.All)
            {
                // "WorkerService, EconomyService" and "MaterialService.SellPriceBonus" both
                // appear — split on the separators and check the type name.
                var readers = entry.Reader
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Select(r => r.Split('.')[0].Trim());

                foreach (var reader in readers)
                {
                    if (!sourceFileNames.Contains(reader))
                    {
                        missing.Add($"{modifier} names '{reader}', which has no source file");
                    }
                }
            }

            Assert.That(missing, Is.Empty, string.Join("; ", missing));
        }

        [Test]
        public void EveryModifier_HasAnEffectDescription()
        {
            // The text an admin reads when choosing a modifier. An empty one is a dropdown
            // entry nobody can evaluate.
            foreach (var modifier in Enum.GetValues<ItemModifier>())
            {
                var effect = ModifierReaders.EffectOf(modifier);

                Assert.That(effect, Is.Not.Empty, $"{modifier} has no description");
                Assert.That(effect, Does.Not.Contain("Nothing reads this modifier"),
                    $"{modifier} fell through to the unread fallback");
            }
        }

        [Test]
        public void AnUnreadModifier_SaysSoPlainly()
        {
            // The fallback path, reached only if someone adds an enum value without a reader.
            // It must be unambiguous rather than merely empty — silence would read as "fine".
            var unmapped = (ItemModifier)9999;

            Assert.That(ModifierReaders.EffectOf(unmapped), Does.Contain("Nothing reads"));
            Assert.That(ModifierReaders.IsRead(unmapped), Is.False);
        }
    }
}
