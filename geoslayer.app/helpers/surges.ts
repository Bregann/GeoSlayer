import type { Surge } from '@/interfaces/api/retention/Surge';
/**
 * Presentation logic for Surges (DESIGN.md §5.6).
 *
 * A surge is time-limited and local, which makes it the one retention mechanic that is
 * worthless if the player cannot see it: by the time they next open a menu it may have
 * ended. So it belongs on the map, and it has to say how long is left.
 *
 * The tone rule from §7.4 still applies — the idle layer must never punish you for
 * sleeping. A surge is an invitation to go out now, not a penalty for having been asleep,
 * so nothing here is phrased as loss.
 */


/** Minutes left, floored at zero. */
export function minutesRemaining(surge: Surge, now: Date = new Date()): number {
  const ms = new Date(surge.endsUtc).getTime() - now.getTime();
  return Math.max(0, Math.floor(ms / 60_000));
}

/** Surges that have not yet ended, soonest to expire first. */
export function activeSurges(surges: Surge[], now: Date = new Date()): Surge[] {
  return surges
    .filter((surge) => minutesRemaining(surge, now) > 0)
    .sort((a, b) => minutesRemaining(a, now) - minutesRemaining(b, now));
}

/** "2.5×" — the reason to care, so it leads the banner. */
export function multiplierLabel(surge: Surge): string {
  const rounded = Math.round(surge.multiplier * 10) / 10;
  return `${Number.isInteger(rounded) ? rounded : rounded.toFixed(1)}×`;
}

/** "45m left" / "2h 10m left" — never a bare timestamp. */
export function remainingLabel(surge: Surge, now: Date = new Date()): string {
  const minutes = minutesRemaining(surge, now);
  if (minutes <= 0) return 'ended';
  if (minutes < 60) return `${minutes}m left`;

  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;

  return rest === 0 ? `${hours}h left` : `${hours}h ${rest}m left`;
}

/**
 * The headline for the most urgent surge, or null when nothing is running.
 *
 * Returns null rather than an "all quiet" string: a persistent empty banner is chrome the
 * player learns to ignore, which would cost us the real ones.
 */
export function surgeSummary(surges: Surge[], now: Date = new Date()): string | null {
  const [next] = activeSurges(surges, now);
  if (!next) return null;

  return `⚡ ${multiplierLabel(next)} ${next.description}`;
}

/** The sub-line: how long is left, and how many others are running. */
export function surgeHint(surges: Surge[], now: Date = new Date()): string | null {
  const active = activeSurges(surges, now);
  if (active.length === 0) return null;

  const [next, ...rest] = active;
  const time = remainingLabel(next, now);

  return rest.length === 0
    ? `${time} — head out while it lasts`
    : `${time} · ${rest.length} other surge${rest.length === 1 ? '' : 's'} nearby`;
}
