export interface Worker {
  id: number;
  name: string;
  tier: number;
  claimId: number | null;
  claimName: string | null;
  assignedSkill: number | null;
  assignedSkillName: string | null;
  lastCollectedAtUtc: string;
  xpPerHour: number;
  materialsPerHour: number;
  terrainMultiplier: number;
  terrainMatches: boolean;
  capReachedAtUtc: string;
  isAtCap: boolean;
  isIdle: boolean;
}
