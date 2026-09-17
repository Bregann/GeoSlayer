import type { UnlockEvent } from '@/interfaces/api/progression/UnlockEvent';
import type { UnlockType } from '@/interfaces/api/progression/UnlockType';

/** A skill or system not yet reached — shown greyed with its unlock level. */
export interface LockedSkill {
  /** Matches UnlockEvent.displayName — both carry the ladder's DisplayName. */
  displayName: string;
  payload: string;
  unlockType: UnlockType | number;
  unlocksAtAdventurerLevel: number;
}
