import type { Expedition } from '@/interfaces/api/retention/Expedition';
import type { ExpeditionDestination } from '@/interfaces/api/retention/ExpeditionDestination';
import type { Worker } from '@/interfaces/api/idle/Worker';
/**
 * Presentation logic for Worker Expeditions (DESIGN.md §5.4).
 *
 * §5.4's framing is the thing to preserve: a cathedral you visited on holiday is otherwise
 * dead to you forever. An expedition turns every trip already taken into a permanent
 * asset, so the screen should read as a list of *places you have been* rather than a menu
 * of targets.
 */


/** Only the field the expedition screen needs from a worker. */
export interface WorkerRef {
  id: number;
}


/** "2.4 km" / "340 m" — distance at a human scale. */
export function formatDistance(metres: number): string {
  if (metres < 1000) return `${Math.round(metres)} m`;
  return `${(metres / 1000).toFixed(metres < 10_000 ? 1 : 0)} km`;
}

/**
 * "6h" / "2d 4h" — how long a worker will be away.
 *
 * Deliberately terser than `formatDuration` in helpers/idle.ts, which writes prose
 * ("6 hours") for sentences. This one goes in a dense trade-off row where three values
 * share a line.
 */
export function formatAwayTime(hours: number): string {
  if (hours < 1) return `${Math.max(1, Math.round(hours * 60))}m`;
  if (hours < 24) return `${Math.round(hours)}h`;

  const days = Math.floor(hours / 24);
  const rest = Math.round(hours % 24);

  return rest === 0 ? `${days}d` : `${days}d ${rest}h`;
}

/** Countdown for a worker still away. */
export function timeRemaining(expedition: Expedition): string {
  if (expedition.hasReturned || expedition.secondsRemaining <= 0) return 'Returned';
  return formatAwayTime(expedition.secondsRemaining / 3600);
}

/**
 * When the player first found this place — the reason it is on the list at all.
 *
 * Shown rather than the visit count because the *date* is what makes it a memory: "you
 * were here in May" lands differently from "3 visits".
 */
export function firstVisitLabel(destination: ExpeditionDestination): string {
  const when = new Date(destination.firstVisitUtc).toLocaleDateString(undefined, {
    month: 'short',
    year: 'numeric',
  });

  return `First found ${when}`;
}

/**
 * The trade-off line: how far, how long, what comes back.
 *
 * All three together, because choosing between a close quick trip and a distant slow one
 * is the actual decision §5.4 wants the player making.
 */
export function tradeOffLabel(destination: ExpeditionDestination): string {
  return `${formatDistance(destination.distanceMetres)} · ${formatAwayTime(destination.durationHours)} · ~${destination.estimatedMaterials} materials`;
}

/**
 * Why a destination cannot be chosen, or null.
 *
 * A reason rather than a greyed row — "a worker is already there" is actionable.
 */
export function destinationBlockedReason(destination: ExpeditionDestination): string | null {
  return destination.isAvailable ? null : 'A worker is already there';
}

/**
 * Destinations worth showing, furthest first.
 *
 * The distant ones are the interesting choices, and putting them first is the whole
 * pitch: that trip you took last year still pays.
 */
export function sortDestinations(destinations: ExpeditionDestination[]): ExpeditionDestination[] {
  return [...destinations].sort(
    (a, b) => Number(a.isAvailable) === Number(b.isAvailable)
      ? b.distanceMetres - a.distanceMetres
      : Number(b.isAvailable) - Number(a.isAvailable),
  );
}

/** Returned expeditions first, then by soonest back. */
export function sortExpeditions(expeditions: Expedition[]): Expedition[] {
  return [...expeditions].sort(
    (a, b) => Number(b.hasReturned) - Number(a.hasReturned) || a.secondsRemaining - b.secondsRemaining,
  );
}

/** Workers with no expedition, available to send. */
export function idleWorkerIds(workers: WorkerRef[], expeditions: Expedition[]): number[] {
  const away = new Set(expeditions.map((e) => e.workerId));
  return workers.filter((w) => !away.has(w.id)).map((w) => w.id);
}

/** The empty state — it should explain how to get destinations, not just say "none". */
export function emptyStateMessage(destinations: ExpeditionDestination[]): string | null {
  if (destinations.length > 0) return null;

  return 'Visit a point of interest and it becomes a place you can send workers back to — for good.';
}
