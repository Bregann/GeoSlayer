import type { Encounter } from '@/interfaces/api/combat/Encounter';

/**
 * Presentation logic for combat encounters (DESIGN.md §5C).
 *
 * The framing §5C.2 insists on has to survive into the UI: training grounds are a
 * *boost*, not a requirement. So nothing here implies a player without one is playing
 * wrong — roaming encounters are presented as the normal way to train, and a training
 * ground as a convenience for people who happen to live near a ruin.
 */

/** "85%" — the odds, shown before the walk so the trade-off is visible. */
export function winChanceLabel(encounter: Encounter): string {
  return `${Math.round(encounter.winChance * 100)}%`;
}

/** Minutes until a roaming encounter lapses, or null for a training ground. */
export function minutesRemaining(
  encounter: Encounter,
  now: Date = new Date(),
): number | null {
  if (!encounter.expiresUtc) return null;

  const ms = new Date(encounter.expiresUtc).getTime() - now.getTime();

  return Math.max(0, Math.floor(ms / 60_000));
}

/**
 * How long is left, or that there is no deadline at all.
 *
 * A training ground says "always here" rather than showing no timer: the permanence is
 * the feature, and silence would read as missing information.
 */
export function lifetimeLabel(encounter: Encounter, now: Date = new Date()): string {
  if (encounter.isTrainingGround) return 'Always here';

  const minutes = minutesRemaining(encounter, now);

  if (minutes === null) return 'Always here';
  if (minutes <= 0) return 'Moved on';
  if (minutes < 60) return `${minutes}m left`;

  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;

  return rest === 0 ? `${hours}h left` : `${hours}h ${rest}m left`;
}

/** "340 m away" / "in range" — whether the player can act yet. */
export function rangeLabel(encounter: Encounter): string {
  if (encounter.isInRange) return 'In range';

  const metres = encounter.distanceMetres;

  return metres < 1000
    ? `${Math.round(metres)} m away`
    : `${(metres / 1000).toFixed(1)} km away`;
}

/** Why this encounter cannot be fought right now, or null. */
export function blockedReason(encounter: Encounter): string | null {
  return encounter.isInRange ? null : 'Walk closer to fight this';
}

/**
 * Expiring encounters first, then by distance.
 *
 * Training grounds sink to the bottom because they will still be there tomorrow — the
 * roaming ones are the only things with a deadline.
 */
export function sortEncounters(
  encounters: Encounter[],
  now: Date = new Date(),
): Encounter[] {
  return [...encounters].sort((a, b) => {
    if (a.isTrainingGround !== b.isTrainingGround) {
      return Number(a.isTrainingGround) - Number(b.isTrainingGround);
    }

    const aLeft = minutesRemaining(a, now);
    const bLeft = minutesRemaining(b, now);

    if (aLeft !== null && bLeft !== null && aLeft !== bLeft) return aLeft - bLeft;

    return a.distanceMetres - b.distanceMetres;
  });
}

/**
 * The empty state.
 *
 * Says encounters find *you*, because the alternative reading — that the player must go
 * hunting for them — is both wrong and discouraging for someone in a quiet area.
 */
export function emptyStateMessage(encounters: Encounter[]): string | null {
  if (encounters.length > 0) return null;

  return 'Nothing about right now. Encounters turn up as you move around — anywhere, not just at old ruins.';
}

/* ------------------------------------------------------------------ */
/*  Map presentation                                                   */
/* ------------------------------------------------------------------ */

/**
 * Encounters worth drawing on the map.
 *
 * Lapsed roaming encounters are dropped rather than greyed out: the map is already the
 * busiest surface in the app, and a marker for something that has moved on is noise the
 * player has to learn to ignore.
 */
export function mappableEncounters(
  encounters: Encounter[],
  now: Date = new Date(),
): Encounter[] {
  return encounters.filter((e) => e.isTrainingGround || (minutesRemaining(e, now) ?? 0) > 0);
}

/**
 * The marker glyph. A training ground is a fixed place, a roaming encounter is a thing
 * that turned up — so they must not look identical on the map (§5C.1).
 */
export function mapIcon(encounter: Encounter): string {
  return encounter.isTrainingGround ? '🏰' : '⚔️';
}

/**
 * Marker colour, by urgency rather than by tier.
 *
 * Tier is already visible in the label, and what a player needs to see at a glance is
 * what is about to disappear. A training ground is deliberately the muted one: it will
 * still be there tomorrow.
 */
export function mapColor(encounter: Encounter, now: Date = new Date()): string {
  if (encounter.isTrainingGround) return '#c0a060';

  const minutes = minutesRemaining(encounter, now);

  if (minutes !== null && minutes <= 30) return '#ff4500';

  return '#dc143c';
}

/**
 * The one-line label under a map marker.
 *
 * Tier and odds, because those are what decide whether the walk is worth it. The name
 * is on the encounters screen, which is one tap away.
 */
export function mapLabel(encounter: Encounter): string {
  return `T${encounter.tier} · ${winChanceLabel(encounter)}`;
}
