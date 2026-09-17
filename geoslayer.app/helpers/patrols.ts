/**
 * Presentation logic for Patrol Routes (DESIGN.md §5.7).
 *
 * §5.7's split is the thing this has to communicate: novelty is the only route to
 * *progress*, and routine is the route to *maintenance*. A patrol pays food upkeep, never
 * XP, and once per day — so the screen must not read as a grind target. It is the commute
 * you already walk, paying for the workers you already have.
 */

export interface PatrolWaypoint {
  latitude: number;
  longitude: number;
}

export interface PatrolRoute {
  id: number;
  name: string;
  waypointCount: number;
  completionCount: number;
  lastCompletedUtc: string | null;
  upkeepReward: number;
  waypoints: PatrolWaypoint[];
}

/** Matches the server's one-per-day rule in RetentionService. */
export const COOLDOWN_HOURS = 20;

/** The minimum the server will accept. */
export const MINIMUM_WAYPOINTS = 2;

/** Hours since a route was last completed, or null if it never has been. */
export function hoursSinceCompletion(
  route: PatrolRoute,
  now: Date = new Date(),
): number | null {
  if (!route.lastCompletedUtc) return null;
  return (now.getTime() - new Date(route.lastCompletedUtc).getTime()) / 3_600_000;
}

/** Whether walking this route again would pay. */
export function isReady(route: PatrolRoute, now: Date = new Date()): boolean {
  const since = hoursSinceCompletion(route, now);
  return since === null || since >= COOLDOWN_HOURS;
}

/**
 * Status line for a route.
 *
 * A route on cooldown says when it is next worth walking rather than that it is
 * unavailable — the player may well be walking it anyway.
 */
export function routeStatus(route: PatrolRoute, now: Date = new Date()): string {
  const since = hoursSinceCompletion(route, now);

  if (since === null) return 'Never walked — complete the circuit to claim upkeep';
  if (since >= COOLDOWN_HOURS) return 'Ready — walk the circuit to claim upkeep';

  const left = Math.ceil(COOLDOWN_HOURS - since);

  return left <= 1 ? 'Claimed today — ready within the hour' : `Claimed today — ready in ${left}h`;
}

/** "4 stops · 8 rations a day" — the shape of the route and what it pays. */
export function routeSummary(route: PatrolRoute): string {
  const stops = `${route.waypointCount} stop${route.waypointCount === 1 ? '' : 's'}`;
  return `${stops} · ${route.upkeepReward} rations a day`;
}

/** How many times it has been walked, for routes that have been. */
export function completionLabel(route: PatrolRoute): string | null {
  if (route.completionCount === 0) return null;

  return `Walked ${route.completionCount} time${route.completionCount === 1 ? '' : 's'}`;
}

/** Ready routes first, then those closest to coming back. */
export function sortRoutes(routes: PatrolRoute[], now: Date = new Date()): PatrolRoute[] {
  return [...routes].sort((a, b) => {
    const readyDiff = Number(isReady(b, now)) - Number(isReady(a, now));
    if (readyDiff !== 0) return readyDiff;

    return (hoursSinceCompletion(b, now) ?? 0) - (hoursSinceCompletion(a, now) ?? 0);
  });
}

/** Why a draft route cannot be saved yet, or null. */
export function draftBlockedReason(waypoints: PatrolWaypoint[]): string | null {
  if (waypoints.length < MINIMUM_WAYPOINTS) {
    const needed = MINIMUM_WAYPOINTS - waypoints.length;
    return `Add ${needed} more stop${needed === 1 ? '' : 's'} — a patrol needs at least ${MINIMUM_WAYPOINTS}.`;
  }

  return null;
}

/**
 * The empty state.
 *
 * Explains what a patrol is *for*, because the feature is easy to mistake for a second
 * XP source — and §5.7 is explicit that it is not one.
 */
export function emptyStateMessage(routes: PatrolRoute[]): string | null {
  if (routes.length > 0) return null;

  return 'Mark the stops on a walk you already make. Completing it pays rations to feed your workers — upkeep, not XP.';
}
