using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.DTOs.Progression.Responses;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Exceptions;
using GeoSlayer.Domain.Interfaces.Api;
using GeoSlayer.Domain.Interfaces.Helpers;
using Microsoft.EntityFrameworkCore;

namespace GeoSlayer.Domain.Services.Progression;

/// <summary>
/// The one path XP takes into the game (DESIGN.md §3.0b).
///
/// Everything that makes a grant more than "add a number" lives here — the Adventurer
/// cut, the Scholar modifier, level recalculation, the unlock ladder and Bonus Points.
/// That is precisely why callers must not bypass it.
/// </summary>
public class ProgressionService(
    AppDbContext db,
    IEnvironmentalSettingHelper settings) : IProgressionService
{
    public async Task<XpGrantResult> GrantXp(
        int playerId, SkillType? skill, long amount, XpSource source, CancellationToken ct)
    {
        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct)
            ?? throw new NotFoundException($"Player {playerId} not found.");

        var result = new XpGrantResult { Skill = skill };

        // Negative grants would silently roll a player backwards past an unlock they
        // already celebrated.  There is no legitimate caller for one.
        if (amount < 0)
            throw new BadRequestException("XP grants cannot be negative.");

        if (amount == 0)
        {
            Populate(result, player);
            return result;
        }

        var skillXp = amount;

        if (skill.HasValue)
        {
            // Scholar (+5%/rank) applies to skill XP, and therefore flows into the
            // Adventurer cut below.  §3.0a: the upgrade must change actual XP.
            var scholar = await GetUpgradeEffect(playerId, ProgressionDefaults.UpgradeKeys.Scholar, ct);

            // Equipped gear stacks with Scholar (§4.3): different acquisition routes to
            // the same stat. Read here rather than via ICraftingService to avoid a cycle.
            var gearXp = await db.PlayerItems
                .Include(pi => pi.Item)
                .Where(pi => pi.PlayerId == playerId
                          && pi.IsEquipped
                          && pi.Quantity > 0
                          && pi.Item.Modifier == ItemModifier.SkillXpPercent)
                .SumAsync(pi => pi.Item.ModifierValue, ct);

            var xpBonus = scholar + gearXp;

            if (xpBonus > 0)
                skillXp = (long)Math.Floor(skillXp * (1 + xpBonus));

            var row = await db.PlayerSkills
                .FirstOrDefaultAsync(s => s.PlayerId == playerId && s.SkillType == skill.Value, ct);

            // Row exists <=> unlocked.  XP for a locked skill is dropped rather than
            // silently unlocking it — the ladder is the only way in (§3.1).
            if (row is not null)
            {
                var before = row.Level;
                row.Xp += skillXp;
                row.Level = XpCurve.LevelForXp(row.Xp);

                result.SkillXpEarned = skillXp;
                result.SkillLevel = row.Level;
                result.SkillLevelledUp = row.Level > before;
            }
            else
            {
                skillXp = 0;
            }
        }

        // The flat cut.  Idle sources pay a reduced ratio (§3.3) so a player who never
        // leaves the house still progresses, just slowly.
        var ratio = GetGlobalXpRatio();
        if (source == XpSource.Idle)
            ratio *= GetIdleMultiplier();

        var adventurerXp = skill.HasValue
            ? (long)Math.Floor(skillXp * ratio)
            : (long)Math.Floor(amount * ratio);

        await ApplyAdventurerXp(player, adventurerXp, result, ct);

        await db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<XpGrantResult> GrantMilestone(
        int playerId, MilestoneType milestone, int count, CancellationToken ct)
    {
        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct)
            ?? throw new NotFoundException($"Player {playerId} not found.");

        var result = new XpGrantResult();

        if (count <= 0)
        {
            Populate(result, player);
            return result;
        }

        // Milestone values are already Adventurer XP — no cut is taken (§3.0b).
        var per = GetMilestoneXp(milestone);
        await ApplyAdventurerXp(player, per * count, result, ct);

        await db.SaveChangesAsync(ct);
        return result;
    }

    /// <summary>
    /// Add Adventurer XP, recompute the level, and pay out everything a level-up owes:
    /// one Bonus Point per level (§3.0a) and any ladder rungs crossed (§3.1).
    /// </summary>
    private async Task ApplyAdventurerXp(Player player, long xp, XpGrantResult result, CancellationToken ct)
    {
        var levelBefore = player.AdventurerLevel;

        player.AdventurerXp += xp;
        player.AdventurerLevel = XpCurve.LevelForXp(player.AdventurerXp);

        result.AdventurerXpEarned += xp;

        var gained = player.AdventurerLevel - levelBefore;

        if (gained > 0)
        {
            // Exactly one point per level, so a multi-level grant still pays correctly.
            player.BonusPointsEarned += gained;
            result.BonusPointsGranted += gained;
            result.AdventurerLevelledUp = true;

            var unlocks = await ApplyUnlocks(player, levelBefore, player.AdventurerLevel, ct);
            result.Unlocks.AddRange(unlocks);
        }

        Populate(result, player);
    }

    /// <summary>
    /// Grant every ladder rung in <c>(fromLevel, toLevel]</c>. Called inside the same
    /// SaveChanges as the level change, so a level-up can never persist without its unlocks.
    /// </summary>
    private async Task<List<UnlockEventDto>> ApplyUnlocks(
        Player player, int fromLevel, int toLevel, CancellationToken ct)
    {
        var due = await db.UnlockDefinitions
            .Where(u => u.AdventurerLevel > fromLevel && u.AdventurerLevel <= toLevel)
            .OrderBy(u => u.AdventurerLevel)
            .ToListAsync(ct);

        if (due.Count == 0) return [];

        var existing = await db.PlayerSkills
            .Where(s => s.PlayerId == player.Id)
            .Select(s => s.SkillType)
            .ToListAsync(ct);

        var owned = existing.ToHashSet();
        var events = new List<UnlockEventDto>();
        var now = DateTime.UtcNow;

        foreach (var def in due)
        {
            if (def.UnlockType == UnlockType.Skill)
            {
                if (!Enum.TryParse<SkillType>(def.Payload, out var skillType))
                    continue;   // Seed data naming a skill that no longer exists.

                if (!owned.Add(skillType))
                    continue;   // Already unlocked — re-running the ladder is harmless.

                db.PlayerSkills.Add(new PlayerSkill
                {
                    PlayerId = player.Id,
                    SkillType = skillType,
                    Xp = 0,
                    Level = 1,
                    UnlockedAtUtc = now,
                });
            }

            events.Add(new UnlockEventDto
            {
                AdventurerLevel = def.AdventurerLevel,
                UnlockType = def.UnlockType,
                Payload = def.Payload,
                DisplayName = def.DisplayName,
            });
        }

        return events;
    }

    public async Task<IReadOnlyList<UnlockEventDto>> EnsureStartingUnlocks(int playerId, CancellationToken ct)
    {
        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct)
            ?? throw new NotFoundException($"Player {playerId} not found.");

        // From level 0 so that level-1 rungs are included; ApplyUnlocks skips anything
        // already owned, which is what makes this safe to call on every login.
        var events = await ApplyUnlocks(player, 0, player.AdventurerLevel, ct);

        if (events.Count > 0)
            await db.SaveChangesAsync(ct);

        return events;
    }

    public async Task<PlayerSkillsDto> GetSkills(int playerId, CancellationToken ct)
    {
        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct)
            ?? throw new NotFoundException($"Player {playerId} not found.");

        var skills = await db.PlayerSkills
            .Where(s => s.PlayerId == playerId)
            .OrderBy(s => s.SkillType)
            .ToListAsync(ct);

        var unlockedTypes = skills.Select(s => s.SkillType.ToString()).ToHashSet();

        // The next material tier per skill, so the screen always has something to show
        // the player they are working toward. Driven off the seeded ladder, so it costs
        // nothing to add a skill.
        var skillTypes = skills.Select(s => s.SkillType).ToList();

        var ladder = await db.Materials
            .Where(m => m.SkillType != null
                     && skillTypes.Contains(m.SkillType!.Value)
                     && !m.IsUnique)
            .Select(m => new { Skill = m.SkillType!.Value, m.Name, m.LevelRequired })
            .ToListAsync(ct);

        // The road ahead: every rung above the current level, plus any level-1 rung not
        // yet taken.  Shown greyed with its unlock level — the ladder is a roadmap (§3.1c).
        var locked = await db.UnlockDefinitions
            .Where(u => u.AdventurerLevel > player.AdventurerLevel
                     || (u.UnlockType == UnlockType.Skill && !unlockedTypes.Contains(u.Payload)))
            .OrderBy(u => u.AdventurerLevel)
            .ThenBy(u => u.DisplayName)
            .ToListAsync(ct);

        return new PlayerSkillsDto
        {
            AdventurerLevel = player.AdventurerLevel,
            AdventurerXp = player.AdventurerXp,
            AdventurerXpForCurrentLevel = XpCurve.XpForLevel(player.AdventurerLevel),
            AdventurerXpForNextLevel = XpCurve.XpForLevel(player.AdventurerLevel + 1),
            Unlocked = skills.Select(s =>
            {
                var next = ladder
                    .Where(m => m.Skill == s.SkillType && m.LevelRequired > s.Level)
                    .OrderBy(m => m.LevelRequired)
                    .FirstOrDefault();

                return new SkillDto
                {
                    SkillType = s.SkillType,
                    Name = s.SkillType.ToString(),
                    Xp = s.Xp,
                    Level = s.Level,
                    XpForCurrentLevel = XpCurve.XpForLevel(s.Level),
                    XpForNextLevel = XpCurve.XpForLevel(s.Level + 1),
                    NextTierName = next?.Name,
                    NextTierLevel = next?.LevelRequired,
                };
            }).ToList(),
            Locked = locked.Select(u => new LockedSkillDto
            {
                DisplayName = u.DisplayName,
                Payload = u.Payload,
                UnlockType = u.UnlockType,
                UnlocksAtAdventurerLevel = u.AdventurerLevel,
            }).ToList(),
        };
    }

    public async Task<PlayerUpgradesDto> GetUpgrades(int playerId, CancellationToken ct)
    {
        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct)
            ?? throw new NotFoundException($"Player {playerId} not found.");

        var definitions = await db.UpgradeDefinitions
            .OrderBy(u => u.Category)
            .ThenBy(u => u.Name)
            .ToListAsync(ct);

        var ranks = await db.PlayerUpgrades
            .Where(u => u.PlayerId == playerId)
            .ToDictionaryAsync(u => u.UpgradeKey, u => u.Rank, ct);

        var available = player.BonusPointsEarned - player.BonusPointsSpent;

        return new PlayerUpgradesDto
        {
            BonusPointsEarned = player.BonusPointsEarned,
            BonusPointsSpent = player.BonusPointsSpent,
            BonusPointsAvailable = available,
            RespecCost = RespecCostFor(player, GetRespecBaseCost()),
            Upgrades = definitions.Select(d =>
            {
                var rank = ranks.GetValueOrDefault(d.Key, 0);
                var next = d.CostOfNextRank(rank);

                return new UpgradeDto
                {
                    Key = d.Key,
                    Name = d.Name,
                    Category = d.Category,
                    Description = d.Description,
                    Rank = rank,
                    MaxRank = d.MaxRank,
                    EffectPerRank = d.EffectPerRank,
                    CurrentEffect = d.EffectPerRank * rank,
                    NextRankCost = next,
                    MinAdventurerLevel = d.MinAdventurerLevel,
                    IsAvailable = player.AdventurerLevel >= d.MinAdventurerLevel,
                    CanAfford = next.HasValue
                             && next.Value <= available
                             && player.AdventurerLevel >= d.MinAdventurerLevel,
                };
            }).ToList(),
        };
    }

    public async Task<PlayerUpgradesDto> PurchaseUpgrade(int playerId, string upgradeKey, CancellationToken ct)
    {
        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct)
            ?? throw new NotFoundException($"Player {playerId} not found.");

        var definition = await db.UpgradeDefinitions.FirstOrDefaultAsync(u => u.Key == upgradeKey, ct)
            ?? throw new NotFoundException($"Upgrade '{upgradeKey}' not found.");

        if (player.AdventurerLevel < definition.MinAdventurerLevel)
            throw new BadRequestException(
                $"'{definition.Name}' unlocks at Adventurer level {definition.MinAdventurerLevel}.");

        var row = await db.PlayerUpgrades
            .FirstOrDefaultAsync(u => u.PlayerId == playerId && u.UpgradeKey == upgradeKey, ct);

        var rank = row?.Rank ?? 0;

        var cost = definition.CostOfNextRank(rank)
            ?? throw new BadRequestException($"'{definition.Name}' is already at max rank.");

        var availablePoints = player.BonusPointsEarned - player.BonusPointsSpent;

        if (cost > availablePoints)
            throw new BadRequestException(
                $"'{definition.Name}' rank {rank + 1} costs {cost} points; you have {availablePoints}.");

        if (row is null)
        {
            db.PlayerUpgrades.Add(new PlayerUpgrade
            {
                PlayerId = playerId,
                UpgradeKey = upgradeKey,
                Rank = 1,
            });
        }
        else
        {
            row.Rank += 1;
        }

        player.BonusPointsSpent += cost;

        await db.SaveChangesAsync(ct);
        return await GetUpgrades(playerId, ct);
    }

    public async Task<PlayerUpgradesDto> Respec(int playerId, CancellationToken ct)
    {
        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct)
            ?? throw new NotFoundException($"Player {playerId} not found.");

        var rows = await db.PlayerUpgrades.Where(u => u.PlayerId == playerId).ToListAsync(ct);

        var cost = RespecCostFor(player, GetRespecBaseCost());

        // The refund returns every spent point, then the fee is charged against the
        // refunded total.  A respec the player cannot afford must not clear their tree.
        if (cost > player.BonusPointsSpent && rows.Count > 0)
            throw new BadRequestException(
                $"Respec costs {cost} points and you have only {player.BonusPointsSpent} invested.");

        db.PlayerUpgrades.RemoveRange(rows);

        player.BonusPointsSpent = cost;
        player.RespecCount += 1;

        await db.SaveChangesAsync(ct);
        return await GetUpgrades(playerId, ct);
    }

    public async Task<double> GetUpgradeEffect(int playerId, string upgradeKey, CancellationToken ct)
    {
        var rank = await db.PlayerUpgrades
            .Where(u => u.PlayerId == playerId && u.UpgradeKey == upgradeKey)
            .Select(u => (int?)u.Rank)
            .FirstOrDefaultAsync(ct);

        if (rank is null or 0) return 0;

        var effect = await db.UpgradeDefinitions
            .Where(u => u.Key == upgradeKey)
            .Select(u => (double?)u.EffectPerRank)
            .FirstOrDefaultAsync(ct);

        return (effect ?? 0) * rank.Value;
    }

    /// <summary>§3.0a: cheap the first time, then escalating.</summary>
    private static int RespecCostFor(Player player, int baseCost) => baseCost * (player.RespecCount + 1);

    private static void Populate(XpGrantResult result, Player player)
    {
        result.AdventurerLevel = player.AdventurerLevel;
        result.AdventurerXp = player.AdventurerXp;
    }

    // Config reads.  Settings are seeded, but a missing row must not take the game down —
    // fall back to the documented default rather than throwing on a hot path.
    private double GetGlobalXpRatio() =>
        ReadDouble(EnvironmentalSettingEnum.GlobalXpRatio, ProgressionDefaults.GlobalXpRatio);

    private double GetIdleMultiplier() =>
        ReadDouble(EnvironmentalSettingEnum.IdleXpRatioMultiplier, ProgressionDefaults.IdleXpRatioMultiplier);

    private int GetRespecBaseCost() =>
        (int)ReadDouble(EnvironmentalSettingEnum.RespecBaseCost, ProgressionDefaults.RespecBaseCost);

    private long GetMilestoneXp(MilestoneType milestone)
    {
        var key = milestone switch
        {
            MilestoneType.NewCell => EnvironmentalSettingEnum.MilestoneXpNewCell,
            MilestoneType.FirstPoiVisit => EnvironmentalSettingEnum.MilestoneXpFirstPoiVisit,
            MilestoneType.NewRegion => EnvironmentalSettingEnum.MilestoneXpNewRegion,
            MilestoneType.ClaimTerritory => EnvironmentalSettingEnum.MilestoneXpClaimTerritory,
            MilestoneType.CraftComplete => EnvironmentalSettingEnum.MilestoneXpCraftComplete,
            MilestoneType.SkillUnlock => EnvironmentalSettingEnum.MilestoneXpSkillUnlock,
            _ => throw new ArgumentOutOfRangeException(nameof(milestone)),
        };

        return (long)ReadDouble(key, ProgressionDefaults.MilestoneXp[milestone]);
    }

    private double ReadDouble(EnvironmentalSettingEnum key, double fallback)
    {
        var raw = settings.TryGetEnviromentalSettingValue(key);

        return double.TryParse(raw, System.Globalization.NumberStyles.Float,
                               System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }
}
