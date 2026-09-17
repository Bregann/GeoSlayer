/**
 * Presentation logic for Uncharted Transit (DESIGN.md §7.1).
 *
 * The mechanic only works if the player understands it: they passed through somewhere,
 * it did not reveal, and walking near it will. That is a sentence the UI has to say.
 */

export interface BankedTransit {
  gridLat: number;
  gridLng: number;
  south: number;
  west: number;
  north: number;
  east: number;
  /** Under 1 for a cycling-pace cell, which banked only partially. */
  weight: number;
  expiresUtc: string;
}

export interface TransitRedemption {
  redeemed: number;
  cellsRevealed: number;
  remaining: number;
  expired: number;
}

/**
 * The headline for the map overlay.
 *
 * Returns null when there is nothing banked, so the overlay stays off rather than
 * announcing zero.
 */
export function transitSummary(cells: BankedTransit[]): string | null {
  if (cells.length === 0) return null;

  return `You passed through ${cells.length} place${cells.length === 1 ? '' : 's'}`;
}

/**
 * The explanation. This is the load-bearing string: without it a player sees hatched
 * cells and has no idea what to do about them.
 */
export function transitHint(cells: BankedTransit[]): string | null {
  if (cells.length === 0) return null;

  return 'Walk near them to reveal what you missed.';
}

/** Cells lapsing within a day — worth flagging, since transit is use-it-or-lose-it. */
export function expiringSoon(cells: BankedTransit[], now: Date = new Date()): BankedTransit[] {
  const cutoff = now.getTime() + 24 * 60 * 60 * 1000;

  return cells.filter((c) => new Date(c.expiresUtc).getTime() <= cutoff);
}

/**
 * A nudge when some are about to lapse, or null.
 *
 * Deliberately soft: §7.1 calls transit "an opportunity, not an obligation", so this
 * should not read as a deadline.
 */
export function expiryNudge(cells: BankedTransit[], now: Date = new Date()): string | null {
  const soon = expiringSoon(cells, now);
  if (soon.length === 0) return null;

  return `${soon.length} fading soon`;
}

/**
 * What a redemption did, for the sync toast.
 *
 * Returns null when nothing was redeemed, so an ordinary walk stays quiet.
 */
export function redemptionSummary(redemption: TransitRedemption | null | undefined): string | null {
  if (!redemption || redemption.redeemed <= 0) return null;

  const revealed = redemption.cellsRevealed;

  return revealed > 0
    ? `Revealed ${revealed} place${revealed === 1 ? '' : 's'} you had passed through`
    : `Redeemed ${redemption.redeemed} banked`;
}

/**
 * Opacity for a banked cell, from how much of it banked.
 *
 * A cycling-pace cell banked partially and should look fainter than a train-pace one —
 * the visual carries the speed grading rather than flattening it.
 */
export function transitOpacity(weight: number): number {
  return Math.max(0.12, Math.min(0.45, 0.12 + weight * 0.33));
}
