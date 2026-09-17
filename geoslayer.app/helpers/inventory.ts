import type { Inventory } from '@/interfaces/api/materials/Inventory';
import type { InventoryItem } from '@/interfaces/api/materials/InventoryItem';
import type { MaterialGain } from '@/interfaces/api/materials/MaterialGain';
/**
 * Presentation logic for the inventory screen (Stage 03 task 5).
 *
 * Pure and dependency-free for the same reason as helpers/progression.ts: it is the only
 * part of the app this environment can actually verify. The screen stays thin over it.
 */


/** Icon per material category, matching the server's MaterialCategory ordinals. */
export const CATEGORY_ICONS: Record<string, string> = {
  Dust: '✨',
  Woodland: '🌲',
  Water: '💧',
  Farmland: '🌾',
  Urban: '🏙️',
  Industrial: '⚙️',
  Rocky: '⛰️',
  Coastal: '🐚',
  Relic: '🏺',
};

export function categoryIcon(name: string): string {
  return CATEGORY_ICONS[name] ?? '📦';
}

/** How full a stack is, 0–100. */
export function stackPercent(item: InventoryItem): number {
  if (item.stackCap <= 0) return 0;
  return Math.max(0, Math.min(100, (item.quantity / item.stackCap) * 100));
}

/**
 * "120 / 1,000" — the stack against its cap.
 *
 * Always shows the cap: §7.4 makes overflow a real cost, so the player needs to see the
 * ceiling coming rather than discover it when materials start turning into Dust.
 */
export function stackLabel(item: InventoryItem): string {
  return `${item.quantity.toLocaleString()} / ${item.stackCap.toLocaleString()}`;
}

/**
 * The warning for a stack, or null when there is nothing to say.
 */
export function stackWarning(item: InventoryItem): string | null {
  if (item.isFull) return 'FULL — extra converts to Dust';
  if (item.isNearCap) return 'Nearly full';
  return null;
}

/** Tier badge, e.g. "T3". Tier 1 is unremarkable and gets no badge. */
export function tierLabel(item: InventoryItem): string | null {
  return item.tier > 1 ? `T${item.tier}` : null;
}

/**
 * Items within a category, fullest first so anything about to overflow reads at the top.
 */
export function sortByUrgency(items: InventoryItem[]): InventoryItem[] {
  return [...items].sort(
    (a, b) => stackPercent(b) - stackPercent(a) || a.name.localeCompare(b.name),
  );
}

/**
 * Collapse a sync's gains into one line per material, e.g. "+3 Scrap, +1 Oak Timber".
 *
 * Returns null when nothing was gained, so the caller can skip the toast entirely rather
 * than flashing an empty one on every sync.
 */
export function formatPickups(gains: MaterialGain[]): string | null {
  const real = gains.filter((g) => g.quantity > 0);
  if (real.length === 0) return null;

  return real.map((g) => `+${g.quantity} ${g.name}`).join(', ');
}

/**
 * Whether any of this sync's gains overflowed into Dust — worth telling the player,
 * since it means they are losing value to a full stack.
 */
export function hasOverflow(gains: MaterialGain[]): boolean {
  return gains.some((g) => g.overflowConvertedToDust > 0);
}

/** Total units held, for the screen header. */
export function totalUnits(inventory: Inventory): number {
  return inventory.categories
    .flatMap((c) => c.items)
    .reduce((sum, item) => sum + item.quantity, 0);
}
