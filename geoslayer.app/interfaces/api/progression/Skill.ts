export interface Skill {
  skillType: number;
  name: string;
  xp: number;
  level: number;
  xpForCurrentLevel: number;
  xpForNextLevel: number;
  /** The next material tier, so there is always something in view. */
  nextTierName: string | null;
  nextTierLevel: number | null;
}
