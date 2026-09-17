import type { Craft } from '@/interfaces/api/crafting/Craft';
import type { PlayerItem } from '@/interfaces/api/crafting/PlayerItem';
import type { Recipe } from '@/interfaces/api/crafting/Recipe';
import type { RecipeInput } from '@/interfaces/api/crafting/RecipeInput';
/**
 * Presentation logic for the crafting and equipment screens (Stage 06 task 4).
 *
 * Pure and dependency-free, like the other helpers.
 */






/** "5m" / "1h 30m" / "2h" — a craft duration at a glance. */
export function formatDuration(seconds: number): string {
  if (seconds <= 0) return 'instant';

  const totalMinutes = Math.round(seconds / 60);

  if (totalMinutes < 60) return `${totalMinutes}m`;

  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;

  return minutes === 0 ? `${hours}h` : `${hours}h ${minutes}m`;
}

/** Countdown text for a running craft. */
export function craftTimeRemaining(craft: Craft): string {
  return craft.isComplete || craft.secondsRemaining <= 0
    ? 'Ready to collect'
    : formatDuration(craft.secondsRemaining);
}

/**
 * How a locked recipe should be badged.
 *
 * Travel-gated is deliberately its own category rather than folded into "missing
 * materials": §4.2 wants the player to know the difference between "keep playing" and
 * "go somewhere new".
 */
export function lockBadge(recipe: Recipe): string | null {
  if (recipe.canCraft) return null;

  switch (recipe.lockReason) {
    case 'SkillLocked':
    case 1:
      return 'SKILL LOCKED';
    case 'LevelLocked':
    case 2:
      return 'LEVEL LOCKED';
    case 'TravelGated':
    case 3:
      return 'TRAVEL GATED';
    case 'MissingMaterials':
    case 4:
      return 'NEED MATERIALS';
    default:
      return null;
  }
}

/** True when the block should read as "go somewhere", not "gather more". */
export function isTravelGated(recipe: Recipe): boolean {
  return recipe.lockReason === 'TravelGated' || recipe.lockReason === 3;
}

/** "3 / 5" for one input, so shortfalls are obvious at a glance. */
export function inputLabel(input: RecipeInput): string {
  return `${input.held.toLocaleString()} / ${input.quantity.toLocaleString()}`;
}

/**
 * Recipes ordered so the actionable ones lead: craftable first, then the nearest
 * unlocks, with travel-gated last since they need a trip rather than more play.
 */
export function sortRecipes(recipes: Recipe[]): Recipe[] {
  const rank = (r: Recipe): number => {
    if (r.canCraft) return 0;
    if (isTravelGated(r)) return 3;
    if (r.lockReason === 'MissingMaterials' || r.lockReason === 4) return 1;
    return 2;
  };

  return [...recipes].sort(
    (a, b) => rank(a) - rank(b) || a.levelRequired - b.levelRequired || a.name.localeCompare(b.name),
  );
}

/** Items grouped by kind, so gear, tools and buildings read separately. */
export function groupItemsByKind(items: PlayerItem[]): { kind: string; items: PlayerItem[] }[] {
  const names: Record<string, string> = { 0: 'Gear', 1: 'Tool', 2: 'Building' };
  const byKind = new Map<string, PlayerItem[]>();

  for (const item of items) {
    const kind = typeof item.kind === 'number' ? (names[item.kind] ?? 'Other') : item.kind;

    const existing = byKind.get(kind);
    if (existing) existing.push(item);
    else byKind.set(kind, [item]);
  }

  return [...byKind.entries()].map(([kind, list]) => ({ kind, items: list }));
}

/** Whether a building still needs somewhere to go. */
export function needsClaim(item: PlayerItem): boolean {
  const kind = typeof item.kind === 'number' ? item.kind : item.kind === 'Building' ? 2 : 0;
  return kind === 2 && item.claimId === null;
}
