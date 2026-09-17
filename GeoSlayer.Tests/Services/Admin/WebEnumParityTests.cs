using GeoSlayer.Domain.Enums;
using System.Text.RegularExpressions;

namespace GeoSlayer.Tests.Services.Admin
{
    /// <summary>
    /// The admin web client mirrors three enums as ordered arrays, because a dropdown has to
    /// show names while the API expects the numeric value.
    ///
    /// <para><b>That coupling is silent when it breaks.</b> Reorder an enum in C#, or insert
    /// a value in the middle, and the web client keeps compiling and keeps sending numbers —
    /// they just mean something else. An admin picks "Trinket" and saves "Feet".</para>
    ///
    /// <para>These tests read the TypeScript and compare it to the enum, so the drift fails a
    /// build instead of corrupting data.</para>
    /// </summary>
    [TestFixture]
    public class WebEnumParityTests
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

        private static string EnumsFilePath() =>
            Path.Combine(FindRepositoryRoot(), "geoslayer.web", "interfaces", "api", "admin", "ItemEnums.ts");

        /// <summary>Reads one `export const Name = [...] as const` array from the file.</summary>
        private static List<string> ReadArray(string source, string name)
        {
            var match = Regex.Match(
                source,
                $@"export const {name} = \[(?<body>.*?)\] as const",
                RegexOptions.Singleline);

            Assert.That(match.Success, Is.True, $"could not find '{name}' in ItemEnums.ts");

            return [.. Regex.Matches(match.Groups["body"].Value, @"'([^']+)'")
                .Select(m => m.Groups[1].Value)];
        }

        private static void AssertMatches<TEnum>(string arrayName) where TEnum : struct, Enum
        {
            var path = EnumsFilePath();

            Assert.That(File.Exists(path), Is.True, $"expected the web enums at {path}");

            var fromWeb = ReadArray(File.ReadAllText(path), arrayName);

            // Ordered, not sorted: the index *is* the wire value, so order is the contract.
            var fromCSharp = Enum.GetValues<TEnum>()
                .OrderBy(v => Convert.ToInt32(v))
                .Select(v => v.ToString()!)
                .ToList();

            Assert.That(fromWeb, Is.EqualTo(fromCSharp),
                $"{arrayName} in ItemEnums.ts no longer matches {typeof(TEnum).Name}. " +
                "The array index is the value sent to the API, so a mismatch silently saves " +
                "the wrong thing.");
        }

        [Test]
        public void ItemKinds_MatchTheEnum() => AssertMatches<ItemKind>("ItemKinds");

        [Test]
        public void ItemSlots_MatchTheEnum() => AssertMatches<ItemSlot>("ItemSlots");

        [Test]
        public void ItemModifiers_MatchTheEnum() => AssertMatches<ItemModifier>("ItemModifiers");
    }
}
