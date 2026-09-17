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

/**
 * "1,204 held" — just the quantity.
 *
 * Stack caps were removed with the coin economy (§5.4), so there is no ceiling to warn
 * about any more. What replaced the warning is `priceLabel`: the interesting fact about a
 * stack is now what it is worth, not how close it is to overflowing.
 */
export function stackLabel(item: InventoryItem): string {
  return `${item.quantity.toLocaleString()} held`;
}

/** "480c" — what the whole stack fetches at a shop, the player's bonus included. */
export function priceLabel(item: InventoryItem): string {
  return `${item.stackPrice.toLocaleString()}c`;
}

/** "2c each" — shown so the player can judge a stack they have not gathered yet. */
export function unitPriceLabel(item: InventoryItem): string {
  return `${item.unitPrice.toLocaleString()}c each`;
}

/** Tier badge, e.g. "T3". Tier 1 is unremarkable and gets no badge. */
export function tierLabel(item: InventoryItem): string | null {
  return item.tier > 1 ? `T${item.tier}` : null;
}

/**
 * Items within a category, most valuable first.
 *
 * Was "fullest first", which sorted by what was about to be lost. With nothing to lose,
 * the useful order is what is worth carrying to a shop.
 */
export function sortByValue(items: InventoryItem[]): InventoryItem[] {
  return [...items].sort(
    (a, b) => b.stackPrice - a.stackPrice || a.name.localeCompare(b.name),
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

/** Total units held, for the screen header. */
export function totalUnits(inventory: Inventory): number {
  return inventory.categories
    .flatMap((c) => c.items)
    .reduce((sum, item) => sum + item.quantity, 0);
}
