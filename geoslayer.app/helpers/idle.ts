/**
 * Presentation logic for the idle layer (Stage 05 tasks 4–5).
 *
 * Pure and dependency-free, like the other helpers.
 */

import type { MaterialGain } from '@/types/inventory';

export interface SkillAccrual {
  skillType: number;
  name: string;
  xpEarned: number;
  level: number;
  levelledUp: boolean;
}

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

/**
 * Whether the welcome-back screen is worth showing.
 *
 * §5 task 4: show it only when something meaningful accrued, never nag. An empty
 * screen on every sync would train the player to dismiss it without reading.
 */
export function shouldShowWelcomeBack(accrual: OfflineAccrual | null | undefined): boolean {
  if (!accrual || !accrual.hasAccrual) return false;

  return accrual.skills.length > 0 || accrual.materials.some((m) => m.quantity > 0);
}

/** "3 hours" / "45 minutes" — how long the workers were running. */
export function formatDuration(hours: number): string {
  if (hours <= 0) return 'no time';

  if (hours < 1) {
    const minutes = Math.max(1, Math.round(hours * 60));
    return `${minutes} minute${minutes === 1 ? '' : 's'}`;
  }

  const rounded = Math.round(hours * 10) / 10;
  const whole = Number.isInteger(rounded) ? rounded : rounded.toFixed(1);

  return `${whole} hour${rounded === 1 ? '' : 's'}`;
}

/**
 * The headline line for the welcome-back screen.
 *
 * Deliberately specific — "340 Timber, Foraging reached level 14" rather than "you have
 * rewards", which is what makes closing the app feel good rather than like quitting.
 */
export function summariseAccrual(accrual: OfflineAccrual): string {
  const parts: string[] = [];

  for (const skill of accrual.skills) {
    if (skill.levelledUp) parts.push(`${skill.name} reached level ${skill.level}`);
  }

  const materials = accrual.materials.filter((m) => m.quantity > 0);

  for (const material of materials.slice(0, 3)) {
    parts.push(`${material.quantity} ${material.name}`);
  }

  if (materials.length > 3) parts.push(`+${materials.length - 3} more`);

  if (parts.length === 0) {
    const xp = accrual.skills.reduce((sum, s) => sum + s.xpEarned, 0);
    if (xp > 0) parts.push(`${xp} XP`);
  }

  return parts.join(', ');
}

/**
 * Whether to nudge the player about the cap.
 *
 * Only when they actually lost time to it — nagging about a cap they never reached is
 * the kind of thing that makes an idle game feel like a chore.
 */
export function capWarning(accrual: OfflineAccrual): string | null {
  if (!accrual.wasCapped) return null;

  return `Workers stopped after ${formatDuration(accrual.offlineCapHours)} — upgrade Offline Cap to bank more`;
}

/** How a worker's current state reads on the management screen. */
export function workerStatus(worker: Worker): string {
  if (worker.isIdle) return 'Idle — assign to a Claim';
  if (worker.isAtCap) return 'At cap — collect by syncing';

  return `${worker.assignedSkillName} on ${worker.claimName}`;
}

/**
 * The terrain note for a worker.
 *
 * A mismatch says "base rate", never "cannot work" — terrain multiplies, it never gates
 * (§5.2), and the wording should not imply otherwise.
 */
export function terrainNote(worker: Worker): string | null {
  if (worker.isIdle) return null;

  return worker.terrainMatches
    ? `Good terrain — ${worker.terrainMultiplier.toFixed(2)}× yield`
    : 'Base rate — this skill suits other terrain better';
}

/** Hours until a worker stops accruing, or 0 when already there. */
export function hoursUntilCap(worker: Worker, now: Date = new Date()): number {
  const cap = new Date(worker.capReachedAtUtc).getTime();
  const remaining = (cap - now.getTime()) / 3_600_000;

  return remaining <= 0 ? 0 : remaining;
}
