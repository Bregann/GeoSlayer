using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Interfaces.Helpers;
using GeoSlayer.Domain.Services.Progression;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Domain.Helpers
{
    public class DatabaseSeedHelper
    {
        public static async Task SeedDatabase(AppDbContext context, IEnvironmentalSettingHelper settingsHelper, IServiceProvider serviceProvider)
        {
            await SeedSetting(context, EnvironmentalSettingEnum.HangfireUsername, "admin");
            await SeedSetting(context, EnvironmentalSettingEnum.HangfirePassword, "password");

            await SeedProgressionSettings(context);
            await SeedUnlockLadder(context);
            await SeedUpgrades(context);

            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Progression constants (§3.0b, §3.3). Seeded rather than compiled in so the
        /// whole progression spine can be retuned without a deploy.
        /// </summary>
        private static async Task SeedProgressionSettings(AppDbContext context)
        {
            await SeedSetting(context, EnvironmentalSettingEnum.GlobalXpRatio,
                Invariant(ProgressionDefaults.GlobalXpRatio));

            await SeedSetting(context, EnvironmentalSettingEnum.IdleXpRatioMultiplier,
                Invariant(ProgressionDefaults.IdleXpRatioMultiplier));

            await SeedSetting(context, EnvironmentalSettingEnum.RespecBaseCost,
                ProgressionDefaults.RespecBaseCost.ToString());

            await SeedSetting(context, EnvironmentalSettingEnum.MilestoneXpNewCell,
                ProgressionDefaults.MilestoneXp[MilestoneType.NewCell].ToString());

            await SeedSetting(context, EnvironmentalSettingEnum.MilestoneXpFirstPoiVisit,
                ProgressionDefaults.MilestoneXp[MilestoneType.FirstPoiVisit].ToString());

            await SeedSetting(context, EnvironmentalSettingEnum.MilestoneXpNewRegion,
                ProgressionDefaults.MilestoneXp[MilestoneType.NewRegion].ToString());

            await SeedSetting(context, EnvironmentalSettingEnum.MilestoneXpClaimTerritory,
                ProgressionDefaults.MilestoneXp[MilestoneType.ClaimTerritory].ToString());

            await SeedSetting(context, EnvironmentalSettingEnum.MilestoneXpCraftComplete,
                ProgressionDefaults.MilestoneXp[MilestoneType.CraftComplete].ToString());

            await SeedSetting(context, EnvironmentalSettingEnum.MilestoneXpSkillUnlock,
                ProgressionDefaults.MilestoneXp[MilestoneType.SkillUnlock].ToString());
        }

        /// <summary>
        /// The unlock ladder (§3.1). Inserts missing rungs and leaves existing rows alone,
        /// so a retuned live ladder is not stamped back to defaults on the next boot.
        /// </summary>
        private static async Task SeedUnlockLadder(AppDbContext context)
        {
            var existing = await context.UnlockDefinitions
                .Select(u => new { u.AdventurerLevel, u.Payload })
                .ToListAsync();

            var have = existing
                .Select(e => (e.AdventurerLevel, e.Payload))
                .ToHashSet();

            foreach (var rung in ProgressionSeedData.Ladder)
            {
                if (have.Contains((rung.AdventurerLevel, rung.Payload))) continue;

                context.UnlockDefinitions.Add(new UnlockDefinition
                {
                    AdventurerLevel = rung.AdventurerLevel,
                    UnlockType = rung.UnlockType,
                    Payload = rung.Payload,
                    DisplayName = rung.DisplayName,
                });
            }
        }

        /// <summary>The four Stage 02 Bonus Point upgrades (§3.0a).</summary>
        private static async Task SeedUpgrades(AppDbContext context)
        {
            var have = (await context.UpgradeDefinitions.Select(u => u.Key).ToListAsync())
                .ToHashSet();

            foreach (var upgrade in ProgressionSeedData.Upgrades)
            {
                if (have.Contains(upgrade.Key)) continue;

                context.UpgradeDefinitions.Add(new UpgradeDefinition
                {
                    Key = upgrade.Key,
                    Name = upgrade.Name,
                    Category = upgrade.Category,
                    MaxRank = upgrade.MaxRank,
                    CostCurve = upgrade.CostCurve,
                    EffectPerRank = upgrade.EffectPerRank,
                    MinAdventurerLevel = upgrade.MinAdventurerLevel,
                    Description = upgrade.Description,
                });
            }
        }

        private static async Task SeedSetting(AppDbContext context, EnvironmentalSettingEnum key, string value)
        {
            var name = key.ToString();

            if (await context.EnvironmentalSettings.AnyAsync(s => s.Key == name)) return;

            await context.EnvironmentalSettings.AddAsync(new EnvironmentalSetting
            {
                Key = name,
                Value = value
            });
        }

        private static string Invariant(double value) =>
            value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
