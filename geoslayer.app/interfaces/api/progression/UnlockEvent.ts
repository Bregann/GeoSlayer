import type { UnlockType } from '@/interfaces/api/progression/UnlockType';

/** A ladder rung the player just crossed (DESIGN.md §3.1c). */
export interface UnlockEvent {
  adventurerLevel: number;
  unlockType: UnlockType | number;
  payload: string;
  displayName: string;
}
