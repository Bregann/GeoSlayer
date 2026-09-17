import type { MuseumRarity } from '@/interfaces/api/museum/MuseumRarity';
export interface MuseumEntry {
  key: string;
  wing: string | number;
  name: string;
  description: string;
  rarity: MuseumRarity | number;
  unlockCondition: string;
  isFound: boolean;
  firstAcquiredUtc: string | null;
  acquiredAtName: string | null;
  quantity: number;
  donatableQuantity: number;
}
