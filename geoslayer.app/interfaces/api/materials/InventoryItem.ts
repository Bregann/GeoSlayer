export interface InventoryItem {
  materialId: number;
  key: string;
  name: string;
  tier: number;
  category: number;
  skillType: number | null;
  quantity: number;
  /** Coin for one unit, before the sell bonus (§5.4). */
  unitPrice: number;

  /** Coin the whole stack fetches, bonus included. */
  stackPrice: number;

  /** Safe for the "sell all junk" shortcut. */
  isJunk: boolean;
  isUnique: boolean;
}
