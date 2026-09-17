import type { BankAccount } from '@/interfaces/api/economy/BankAccount';
import type { NearbyPoi } from '@/interfaces/api/journey/NearbyPoi';
import type { InventoryItem } from '@/interfaces/api/materials/InventoryItem';

/**
 * Presentation logic for coin (DESIGN.md §5.4, §5.4a).
 *
 * The framing this has to protect: selling is a **reason to walk somewhere**, not a chore
 * the game imposes. Nothing here nags about a full bag — there is no such thing any more
 * (§7.4 revised) — and nothing implies a player is doing it wrong by hoarding.
 */

/** "1,240c" — coin, everywhere, in one format. */
export function coinLabel(amount: number): string {
  return `${amount.toLocaleString()}c`;
}

/**
 * Shops the player is standing at, from the sync's nearby POIs.
 *
 * In range only: a shop 400m away is not somewhere you can sell, and listing it would be
 * an invitation the button then refuses.
 */
export function sellableAt(pois: NearbyPoi[]): NearbyPoi[] {
  return pois.filter((poi) => poi.skill === 'Trading' && poi.inRange);
}

/** Banks the player is standing at. */
export function bankableAt(pois: NearbyPoi[]): NearbyPoi[] {
  return pois.filter((poi) => poi.skill === 'Banking' && poi.inRange);
}

/**
 * The prompt when a shop is within reach, or null.
 *
 * Only ever shown when there is something to sell *and* somewhere to sell it — a standing
 * "sell your stuff" banner is chrome a player learns to ignore.
 */
export function sellPrompt(pois: NearbyPoi[], totalValue: number): string | null {
  const shops = sellableAt(pois);
  if (shops.length === 0 || totalValue <= 0) return null;

  return `${shops[0].name} will buy your haul — ${coinLabel(totalValue)}`;
}

/** Items worth listing to sell, most valuable first. */
export function sellableItems(items: InventoryItem[]): InventoryItem[] {
  return [...items]
    .filter((item) => item.quantity > 0)
    .sort((a, b) => b.stackPrice - a.stackPrice || a.name.localeCompare(b.name));
}

/** What the junk shortcut would fetch, so the button can say rather than surprise. */
export function junkValue(items: InventoryItem[]): number {
  return items.filter((item) => item.isJunk).reduce((sum, item) => sum + item.stackPrice, 0);
}

/** How many distinct materials the junk shortcut would clear. */
export function junkCount(items: InventoryItem[]): number {
  return items.filter((item) => item.isJunk && item.quantity > 0).length;
}

/**
 * What interest is currently worth, phrased honestly.
 *
 * The rate is deliberately tiny (§5.4a) — a nudge for locking money away, not an income.
 * Overselling it here would be the fastest way to make a player feel cheated when they
 * come back and find forty coins.
 */
export function interestSummary(account: BankAccount): string {
  if (account.deposited <= 0) {
    return 'Deposit coin to earn a little interest while you are away.';
  }

  if (account.interestPerFullWindow <= 0) {
    return 'Too small a balance to earn interest yet.';
  }

  return `Earning up to ${coinLabel(account.interestPerFullWindow)} per ${Math.round(account.capHours)}h away.`;
}

/** The line after a sale — names the shop, because walking there was the cost. */
export function saleSummary(coinEarned: number, poiName: string): string {
  return coinEarned > 0
    ? `Sold at ${poiName} for ${coinLabel(coinEarned)}`
    : `Nothing sold at ${poiName}`;
}
