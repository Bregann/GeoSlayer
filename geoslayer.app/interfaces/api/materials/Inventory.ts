import type { InventoryCategory } from '@/interfaces/api/materials/InventoryCategory';

export interface Inventory {
  categories: InventoryCategory[];
  distinctMaterials: number;
  /** What the whole haul would fetch at a shop. */
  totalSellValue: number;
}
