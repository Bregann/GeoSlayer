export interface PlayerItem {
  id: number;
  itemId: number;
  key: string;
  name: string;
  description: string;
  kind: string | number;
  slot: string | number;
  modifierText: string;
  tier: number;
  quantity: number;
  isEquipped: boolean;
  claimId: number | null;
  claimName: string | null;

  /** Whether an admin has uploaded artwork for this item (Stage 18 task 3). */
  hasImage: boolean;
}
