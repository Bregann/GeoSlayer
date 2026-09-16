/**
 * Mirrors GeoSlayer.Domain/DTOs/Progression/Responses/ProgressionDtos.cs.
 *
 * The server is the authority on every number here — these screens render what it
 * already decided and never compute progression themselves.
 */

export type UnlockType = 'Skill' | 'System';

/** A ladder rung the player just crossed (DESIGN.md §3.1c). */
export interface UnlockEvent {
  adventurerLevel: number;
  unlockType: UnlockType | number;
  payload: string;
  displayName: string;
}

export interface Skill {
  skillType: number;
  name: string;
  xp: number;
  level: number;
  xpForCurrentLevel: number;
  xpForNextLevel: number;
}

/** A skill or system not yet reached — shown greyed with its unlock level. */
export interface LockedSkill {
  /** Matches UnlockEvent.displayName — both carry the ladder's DisplayName. */
  displayName: string;
  payload: string;
  unlockType: UnlockType | number;
  unlocksAtAdventurerLevel: number;
}

export interface PlayerSkills {
  adventurerLevel: number;
  adventurerXp: number;
  adventurerXpForCurrentLevel: number;
  adventurerXpForNextLevel: number;
  unlocked: Skill[];
  locked: LockedSkill[];
}

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

export interface PlayerUpgrades {
  bonusPointsEarned: number;
  bonusPointsSpent: number;
  bonusPointsAvailable: number;
  /** What the next respec will cost — it escalates with each use (§3.0a). */
  respecCost: number;
  upgrades: Upgrade[];
}
