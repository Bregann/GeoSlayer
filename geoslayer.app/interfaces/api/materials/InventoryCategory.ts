import type { InventoryItem } from '@/interfaces/api/materials/InventoryItem';

export interface InventoryCategory {
  category: number;
  name: string;
  items: InventoryItem[];
}
