import type { MaterialGain } from '@/interfaces/api/materials/MaterialGain';
import type { SkillAccrual } from '@/interfaces/api/idle/SkillAccrual';

export interface OfflineAccrual {
  hasAccrual: boolean;
  hoursAccrued: number;
  wasCapped: boolean;
  offlineCapHours: number;
  materials: MaterialGain[];
  skills: SkillAccrual[];
  unlocks: { displayName: string; payload: string; adventurerLevel: number }[];
  adventurerXpEarned: number;
  bonusPointsGranted: number;
}
