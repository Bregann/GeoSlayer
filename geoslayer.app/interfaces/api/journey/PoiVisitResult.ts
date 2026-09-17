import type { MaterialGain } from '@/interfaces/api/materials/MaterialGain';
import type { UnlockEvent } from '@/interfaces/api/progression/UnlockEvent';

export interface PoiVisitResult {
  sessionToken: string;
  poiId: number;
  poiName: string;
  skill: string;
  skillXpEarned: number;
  adventurerXpEarned: number;
  skillLevel: number;
  levelledUp: boolean;
  isFirstVisit: boolean;
  visitCount: number;
  totalVisits: number;
  /** 0.05-1.0 decay applied to this visit (DESIGN.md §3.4). */
  decayMultiplier: number;
  materials: MaterialGain[];
  unlocks: UnlockEvent[];
}
