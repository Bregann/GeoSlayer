import type { Upgrade } from '@/interfaces/api/progression/Upgrade';

export interface PlayerUpgrades {
  bonusPointsEarned: number;
  bonusPointsSpent: number;
  bonusPointsAvailable: number;
  /** What the next respec will cost — it escalates with each use (§3.0a). */
  /** Coin for the next respec (§5D.4). Doubles each time. */
  respecCost: number;
  upgrades: Upgrade[];
}
