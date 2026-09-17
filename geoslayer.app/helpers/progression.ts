import type { LockedSkill } from '@/interfaces/api/progression/LockedSkill';
import type { PlayerSkills } from '@/interfaces/api/progression/PlayerSkills';
import type { Skill } from '@/interfaces/api/progression/Skill';
import type { Upgrade } from '@/interfaces/api/progression/Upgrade';
/**
 * Presentation logic for the skills and upgrade screens.
 *
 * Kept out of the components so it can be tested on plain Node — the app has no test
 * runner and this file, like helpers/xpCurve.ts, imports nothing.
 *
 * None of this decides progression. The server owns every number; these functions only
 * choose how to arrange and label what it sent.
 */


/** Icon per skill, keyed by the server's SkillType name. */
export const SKILL_ICONS: Record<string, string> = {
  Exploration: '🗺️',
  Foraging: '🌿',
  Fishing: '🎣',
  Woodcutting: '🪓',
  Cooking: '🍳',
  Mining: '⛏️',
  Smithing: '🔨',
  Farming: '🌾',
  Trading: '💰',
  Prayer: '🙏',
  Knowledge: '📚',
  Healing: '❤️',
  Athletics: '🏃',
  Tavern: '🍺',
  Banking: '🏦',
  Combat: '⚔️',
};

export function skillIcon(name: string): string {
  return SKILL_ICONS[name] ?? '❓';
}

/**
 * Progress through the current level, 0–100.
 *
 * Uses the band the server sent rather than recomputing the curve: if the two ever
 * disagree, the server is right by definition.
 */
export function levelProgressPercent(
  xp: number,
  xpForCurrentLevel: number,
  xpForNextLevel: number,
): number {
  const band = xpForNextLevel - xpForCurrentLevel;
  if (band <= 0) return 0;

  const progress = ((xp - xpForCurrentLevel) / band) * 100;
  return Math.max(0, Math.min(100, progress));
}

/** XP still needed for the next level. Never negative. */
export function xpRemaining(xp: number, xpForNextLevel: number): number {
  return Math.max(0, xpForNextLevel - xp);
}

/**
 * The locked ladder, ascending, with rungs at the same level grouped together.
 *
 * §3.1c wants the ladder read as a roadmap, so the next rung must come first and
 * everything arriving at one level must read as a single event.
 */
export function groupLockedByLevel(
  locked: LockedSkill[],
): { level: number; entries: LockedSkill[] }[] {
  const byLevel = new Map<number, LockedSkill[]>();

  for (const entry of locked) {
    const existing = byLevel.get(entry.unlocksAtAdventurerLevel);
    if (existing) existing.push(entry);
    else byLevel.set(entry.unlocksAtAdventurerLevel, [entry]);
  }

  return [...byLevel.entries()]
    .sort((a, b) => a[0] - b[0])
    .map(([level, entries]) => ({ level, entries }));
}

/** Unlocked skills, highest level first — the player's strongest reads at the top. */
export function sortSkillsByLevel(skills: Skill[]): Skill[] {
  return [...skills].sort((a, b) => b.level - a.level || b.xp - a.xp || a.name.localeCompare(b.name));
}

/** Upgrades grouped by category, preserving the server's ordering within each. */
export function groupUpgradesByCategory(
  upgrades: Upgrade[],
): { category: string; upgrades: Upgrade[] }[] {
  const byCategory = new Map<string, Upgrade[]>();

  for (const upgrade of upgrades) {
    const existing = byCategory.get(upgrade.category);
    if (existing) existing.push(upgrade);
    else byCategory.set(upgrade.category, [upgrade]);
  }

  return [...byCategory.entries()].map(([category, list]) => ({ category, upgrades: list }));
}

/**
 * How a rank reads on the button: "3 / 5", or "MAX" when there is nothing left to buy.
 */
export function rankLabel(upgrade: Upgrade): string {
  return upgrade.rank >= upgrade.maxRank ? 'MAX' : `${upgrade.rank} / ${upgrade.maxRank}`;
}

/**
 * Why an upgrade cannot be bought right now, or null when it can.
 *
 * Returning the reason rather than a boolean means the button can say *why* it is
 * disabled — "Needs level 3" is actionable where a greyed button is not.
 */
export function purchaseBlockedReason(upgrade: Upgrade): string | null {
  if (upgrade.rank >= upgrade.maxRank) return 'Fully upgraded';
  if (!upgrade.isAvailable) return `Unlocks at Adventurer level ${upgrade.minAdventurerLevel}`;
  if (!upgrade.canAfford) {
    const cost = upgrade.nextRankCost ?? 0;
    return `Costs ${cost} ${cost === 1 ? 'point' : 'points'}`;
  }
  return null;
}

/**
 * Effect text for a rank, formatted by unit.
 *
 * The server sends a bare number whose unit depends on the upgrade (cells, hours, a
 * fraction), so the unit lives here with the rest of the presentation.
 */
export function formatEffect(upgrade: Upgrade, value: number): string {
  switch (upgrade.key) {
    case 'reveal_radius':
      return `+${value} cell${value === 1 ? '' : 's'} reveal radius`;
    case 'worker_slot':
      return `+${value} worker slot${value === 1 ? '' : 's'}`;
    case 'offline_cap':
      return `+${value}h offline accrual`;
    case 'scholar':
      return `+${Math.round(value * 100)}% skill XP`;
    default:
      return `+${value}`;
  }
}

/** Current total effect, or null at rank 0 where there is nothing to show. */
export function currentEffectLabel(upgrade: Upgrade): string | null {
  if (upgrade.rank <= 0) return null;
  return formatEffect(upgrade, upgrade.currentEffect);
}

/**
 * A one-line summary for the skills screen header.
 *
 * Deliberately mentions the next unlock rather than only the level: §3.1's whole point
 * is that the ladder is visible, so the player always knows what they are walking toward.
 */
export function nextUnlockSummary(skills: PlayerSkills): string | null {
  const grouped = groupLockedByLevel(skills.locked);
  if (grouped.length === 0) return null;

  const next = grouped[0];
  const names = next.entries.map((e) => e.displayName).join(' + ');
  const levels = next.level - skills.adventurerLevel;

  if (levels <= 0) return `${names} ready to unlock`;

  return `${names} at level ${next.level} (${levels} level${levels === 1 ? '' : 's'} away)`;
}

/**
 * What a skill unlocks next, for the skills screen.
 *
 * SKILL-TEMPLATE.md asks for the next tier to always be in view — a bar with no stated
 * destination is just a number going up.
 */
export function nextTierLabel(skill: Skill): string | null {
  if (!skill.nextTierName || skill.nextTierLevel === null) return null;

  const levels = skill.nextTierLevel - skill.level;

  return levels <= 0
    ? `${skill.nextTierName} available now`
    : `${skill.nextTierName} at level ${skill.nextTierLevel}`;
}
