import type { LockedSkill } from '@/interfaces/api/progression/LockedSkill';
import type { Skill } from '@/interfaces/api/progression/Skill';

export interface PlayerSkills {
  adventurerLevel: number;
  adventurerXp: number;
  adventurerXpForCurrentLevel: number;
  adventurerXpForNextLevel: number;
  unlocked: Skill[];
  locked: LockedSkill[];
}
