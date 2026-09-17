import type { Upgrade } from '@/interfaces/api/progression/Upgrade';

export interface PlayerUpgrades {
  bonusPointsEarned: number;
  bonusPointsSpent: number;
  bonusPointsAvailable: number;
  /** What the next respec will cost — it escalates with each use (§3.0a). */
  respecCost: number;
  upgrades: Upgrade[];
}
