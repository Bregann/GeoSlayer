import type { InventoryCategory } from '@/interfaces/api/materials/InventoryCategory';

export interface Inventory {
  categories: InventoryCategory[];
  distinctMaterials: number;
  nearCapCount: number;
}
