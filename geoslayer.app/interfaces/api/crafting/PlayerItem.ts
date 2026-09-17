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
}
