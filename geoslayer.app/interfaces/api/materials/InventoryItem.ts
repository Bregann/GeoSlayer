export interface InventoryItem {
  materialId: number;
  key: string;
  name: string;
  tier: number;
  category: number;
  skillType: number | null;
  quantity: number;
  stackCap: number;
  isUnique: boolean;
  /** At or above 90% of cap — warned before anything overflows. */
  isNearCap: boolean;
  isFull: boolean;
}
