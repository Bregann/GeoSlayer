/**
 * Mirrors GeoSlayer.Domain/DTOs/Materials/Responses/MaterialDtos.cs.
 */

export interface MaterialGain {
  materialId: number;
  key: string;
  name: string;
  tier: number;
  category: number;
  quantity: number;
  /** Units that did not fit and became Dust instead (DESIGN.md §7.4). */
  overflowConvertedToDust: number;
}

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

export interface InventoryCategory {
  category: number;
  name: string;
  items: InventoryItem[];
}

export interface Inventory {
  categories: InventoryCategory[];
  distinctMaterials: number;
  nearCapCount: number;
}
