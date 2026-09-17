import type { ClueStepType } from '@/interfaces/api/clues/ClueStepType';
export interface ClueStep {
  stepIndex: number;
  stepType: ClueStepType | number;
  riddleText: string;
  /** Only a coordinate step has these — never the answer to a riddle. */
  searchLat: number | null;
  searchLng: number | null;
  searchRadius: number | null;
  isSolved: boolean;
  wasSkipped: boolean;
  solvedUtc: string | null;
}
