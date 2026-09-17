/**
 * Presentation logic for clue scrolls (Stage 13 task 5).
 *
 * §5B.3: "no time limits — clues are for savouring, not stressing". Nothing here counts
 * down, and that absence is deliberate.
 */

export type ClueStepType =
  | 'Direct' | 'Category' | 'Coordinate'
  | 'Terrain' | 'Cryptic' | 'Relational' | 'Sequence';

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

const TIER_NAMES = ['Wandering', 'Roaming', 'Pilgrim', 'Odyssey'] as const;

export function tierName(tier: string | number): string {
  return typeof tier === 'number' ? (TIER_NAMES[tier] ?? 'Wandering') : tier;
}

export const TIER_ICONS: Record<string, string> = {
  Wandering: '🚶',
  Roaming: '🥾',
  Pilgrim: '⛰️',
  Odyssey: '🧭',
};

export function tierIcon(tier: string | number): string {
  return TIER_ICONS[tierName(tier)] ?? '📜';
}

/** "Step 2 of 4" — the only progress language a clue needs. */
export function stepProgress(scroll: ClueScroll): string {
  const step = Math.min(scroll.currentStep + 1, scroll.stepCount);
  return `Step ${step} of ${scroll.stepCount}`;
}

/** The step the player is currently hunting, or null on a finished scroll. */
export function currentStep(scroll: ClueScroll): ClueStep | null {
  if (scroll.isComplete) return null;
  return scroll.steps.find((s) => s.stepIndex === scroll.currentStep) ?? null;
}

/** Whether a step puts a search area on the map. */
export function hasSearchArea(step: ClueStep): boolean {
  return step.searchLat !== null && step.searchLng !== null && step.searchRadius !== null;
}

/**
 * Why skipping is unavailable, or null when it can be used.
 *
 * §5B.3 allows exactly one skip per scroll so a clue can never become a permanent
 * blocker — the button should explain itself rather than just greying out.
 */
export function skipBlockedReason(scroll: ClueScroll, curation: number): string | null {
  if (scroll.isComplete) return 'This scroll is finished';
  if (scroll.skipUsed) return 'You have already skipped a step on this scroll';
  if (curation < scroll.skipCost) return `Needs ${scroll.skipCost} curation`;
  return null;
}

/** Active scrolls first, then completed, each by tier. */
export function sortScrolls(scrolls: ClueScroll[]): ClueScroll[] {
  return [...scrolls].sort(
    (a, b) =>
      Number(a.isComplete) - Number(b.isComplete) ||
      TIER_NAMES.indexOf(tierName(a.tier) as never) - TIER_NAMES.indexOf(tierName(b.tier) as never),
  );
}

/** Tiers the player is not currently carrying, so the UI can offer them. */
export function availableTiers(scrolls: ClueScroll[]): string[] {
  const active = new Set(
    scrolls.filter((s) => !s.isComplete).map((s) => tierName(s.tier)),
  );

  return TIER_NAMES.filter((t) => !active.has(t));
}

/** How a finished step reads in the trail behind the player. */
export function stepSummary(step: ClueStep): string {
  if (step.wasSkipped) return 'Skipped';
  if (step.isSolved) return 'Found';
  return 'Searching';
}
