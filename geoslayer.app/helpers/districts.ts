import type { DistrictStatus } from '@/interfaces/api/retention/DistrictStatus';
/**
 * Presentation logic for Districts (DESIGN.md §5.5).
 *
 * §5.5 requires *varied* terrain on purpose — without it the optimal play is nine
 * identical cells of your best ground and the map stops mattering. But that rule is
 * invisible: a player whose Claims are all woodland sees "no District" and has no way to
 * learn why. So the unformed case is the one that has to communicate, and these helpers
 * exist mostly to make it say something actionable.
 */


/** Whether a District is currently formed. */
export function hasDistrict(status: DistrictStatus | undefined): boolean {
  return !!status?.districtKey;
}

/** "+15% output" — the bonus in the terms it is actually paid in. */
export function bonusLabel(status: DistrictStatus): string {
  return `+${Math.round(status.outputBonus * 100)}% output`;
}

/** Joins terrain names the way a sentence would: "Water and Urban". */
export function joinTerrains(terrains: string[]): string {
  if (terrains.length === 0) return '';
  if (terrains.length === 1) return terrains[0];

  return `${terrains.slice(0, -1).join(', ')} and ${terrains[terrains.length - 1]}`;
}

/** The headline: the District's name, or the fact that there isn't one yet. */
export function districtTitle(status: DistrictStatus | undefined): string {
  if (!status) return 'No District';
  return hasDistrict(status) ? (status.name ?? 'District') : 'No District yet';
}

/**
 * What the player should do next, or what they have.
 *
 * With a District, this is the bonus. Without one, it is the specific shortfall — which
 * terrain to go and claim — because "no District" on its own teaches nothing.
 */
export function districtHint(status: DistrictStatus | undefined): string {
  if (!status) return 'Claim territory to start forming a District.';

  if (hasDistrict(status)) {
    return `${bonusLabel(status)} — ${status.description ?? 'your Claims work together'}`;
  }

  if (!status.nearestName) {
    return 'Claim neighbouring territory of differing terrain to form a District.';
  }

  const needs: string[] = [];

  if (status.missingTerrains.length > 0) {
    needs.push(`${joinTerrains(status.missingTerrains)} terrain`);
  }

  if (status.missingClaims > 0) {
    needs.push(`${status.missingClaims} more Claim${status.missingClaims === 1 ? '' : 's'}`);
  }

  if (needs.length === 0) {
    return `${status.nearestName} needs your Claims to be neighbours.`;
  }

  return `Closest is ${status.nearestName} — needs ${joinTerrains(needs)} nearby.`;
}
