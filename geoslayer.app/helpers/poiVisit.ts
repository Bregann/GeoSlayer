import type { PoiVisitResult } from '@/interfaces/api/journey/PoiVisitResult';
/**
 * Presentation logic for POI visits (Stage 04 task 4).
 *
 * Pure and dependency-free, like the other helpers — it is the part of the app this
 * environment can actually verify.
 */



/**
 * How the decay state reads to the player.
 *
 * Shown *before* they walk to a POI, so a reduced reward is never a surprise. Returning
 * null for a fresh POI keeps the common case uncluttered.
 */
export function decayLabel(visitCount: number): string | null {
  if (visitCount <= 0) return null;

  const percent = Math.round(decayMultiplier(visitCount) * 100);

  return `Visited ${visitCount} time${visitCount === 1 ? '' : 's'} — ${percent}% yield`;
}

/**
 * Mirrors GeoSlayer.Domain/Services/Skills/VisitDecay.cs.
 *
 * The server remains the authority; this only previews what a visit is worth so the app
 * can show it before the player walks there.
 */
export function decayMultiplier(visitCount: number): number {
  if (visitCount <= 0) return 1;
  return Math.max(0.05, 1 / (1 + 0.5 * visitCount));
}

/** Expected XP from a visit, for the pre-visit preview. */
export function previewXp(baseXpReward: number, visitCount: number): number {
  return Math.max(1, Math.floor(baseXpReward * decayMultiplier(visitCount)));
}

/**
 * Why a POI cannot be visited right now, or null when it can.
 *
 * Returning the reason rather than a boolean lets the button explain itself — "Move
 * closer — 120m away" is actionable where a greyed button is not.
 */
export function visitBlockedReason(
  poi: { inRange: boolean; distanceMetres: number },
): string | null {
  if (!poi.inRange) {
    return `Move closer — ${Math.round(poi.distanceMetres)}m away`;
  }
  return null;
}

/** One line summarising what a visit produced. */
export function summariseVisit(result: PoiVisitResult): string {
  const parts = [`+${result.skillXpEarned} ${result.skill} XP`];

  if (result.isFirstVisit) parts.push('First visit!');
  else if (result.decayMultiplier < 1) {
    parts.push(`${Math.round(result.decayMultiplier * 100)}% yield`);
  }

  if (result.levelledUp) parts.push(`Level ${result.skillLevel}!`);

  return parts.join(' · ');
}
