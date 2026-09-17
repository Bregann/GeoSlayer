export interface Upgrade {
  key: string;
  name: string;
  category: string;
  description: string;
  rank: number;
  maxRank: number;
  effectPerRank: number;
  currentEffect: number;
  /** Cost of the next rank, or null at max rank. */
  nextRankCost: number | null;
  minAdventurerLevel: number;
  /** False when below the minimum level — shown, but not purchasable. */
  isAvailable: boolean;
  canAfford: boolean;
}
