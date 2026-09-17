using GeoSlayer.Domain.DTOs.Progression.Responses;
using GeoSlayer.Domain.Enums;

namespace GeoSlayer.Domain.Interfaces.Api.Progression
{
    /// <summary>
    /// The single path by which XP enters the game (DESIGN.md §3.0b, Stage 02 task 1).
    ///
    /// <b>Every</b> XP grant goes through <see cref="GrantXp"/> — there are no exceptions and
    /// later stages depend on that. A grant that bypasses this skips the Adventurer cut, the
    /// unlock ladder and the Scholar upgrade, all of which are applied here.
    /// </summary>
    public interface IProgressionService
    {
        /// <summary>
        /// Award XP. Pays twice: into <paramref name="skill"/> if one is given, and a flat
        /// cut into the Adventurer pool (§3.0b). Applies any unlocks the resulting Adventurer
        /// level crosses, and grants one Bonus Point per level gained.
        /// </summary>
        /// <param name="amount">Base skill XP before upgrade modifiers.</param>
        Task<XpGrantResult> GrantXp(int playerId, SkillType? skill, long amount, XpSource source, CancellationToken ct);

        /// <summary>
        /// Award a discrete Adventurer-only milestone grant (§3.0b). Takes no skill cut —
        /// the value is already Adventurer XP.
        /// </summary>
        Task<XpGrantResult> GrantMilestone(int playerId, MilestoneType milestone, int count, CancellationToken ct);

        /// <summary>
        /// Ensure a new player has their level-1 unlocks. Idempotent, so it is safe to call
        /// on every login as a backstop against a player created before the ladder existed.
        /// </summary>
        Task<IReadOnlyList<UnlockEventDto>> EnsureStartingUnlocks(int playerId, CancellationToken ct);

        /// <summary>Unlocked skills plus the locked ladder ahead — the roadmap (§3.1c).</summary>
        Task<PlayerSkillsDto> GetSkills(int playerId, CancellationToken ct);

        /// <summary>The Bonus Point tree with the player's ranks and what they can afford.</summary>
        Task<PlayerUpgradesDto> GetUpgrades(int playerId, CancellationToken ct);

        /// <summary>Buy one rank. Validates available points, rank cap and minimum level.</summary>
        Task<PlayerUpgradesDto> PurchaseUpgrade(int playerId, string upgradeKey, CancellationToken ct);

        /// <summary>Refund every spent point and clear all ranks, for an escalating cost (§3.0a).</summary>
        Task<PlayerUpgradesDto> Respec(int playerId, CancellationToken ct);

        /// <summary>
        /// Total effect magnitude of an upgrade for a player — <c>EffectPerRank × rank</c>.
        /// The read path for systems that must honour upgrades (§3.0a: an upgrade that
        /// displays but does nothing is worse than no upgrade).
        /// </summary>
        Task<double> GetUpgradeEffect(int playerId, string upgradeKey, CancellationToken ct);
    }
}
