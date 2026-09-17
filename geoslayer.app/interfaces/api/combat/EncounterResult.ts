import type { MaterialGain } from '@/interfaces/api/materials/MaterialGain';
import type { UnlockEvent } from '@/interfaces/api/progression/UnlockEvent';

/** The outcome of a fight (DESIGN.md §5C.3). */
export interface EncounterResult {
  encounterId: number;
  name: string;
  won: boolean;
  skillXpEarned: number;
  adventurerXpEarned: number;
  combatLevel: number;
  levelledUp: boolean;
  /** Empty on a loss — losing costs time, never materials. */
  materials: MaterialGain[];
  unlocks: UnlockEvent[];
  message: string;
}
