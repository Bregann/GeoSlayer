import type { ClueStep } from '@/interfaces/api/clues/ClueStep';

export interface ClueScroll {
  id: number;
  tier: string | number;
  tierName: string;
  currentStep: number;
  stepCount: number;
  isComplete: boolean;
  skipUsed: boolean;
  skipCost: number;
  startedUtc: string;
  completedUtc: string | null;
  steps: ClueStep[];
}
