/**
 * The RuneScape experience curve, mirroring GeoSlayer.Domain/Services/Progression/XpCurve.cs.
 *
 * `xp` from the server is *cumulative lifetime* XP, not progress within the current
 * level, so the HUD has to know the curve to draw a progress bar. The server remains
 * the authority — this only renders what it already decided.
 *
 * XP for level L = floor( (1/4) * sum(n=1..L-1) floor(n + 300 * 2^(n/7)) ).
 * Level 1 = 0, level 2 = 83, level 50 = 101,333, level 99 = 13,034,431.
 */

const TABLE_MAX_LEVEL = 200;

/** Cumulative XP needed for each level, indexed by level. Index 0 is unused. */
const CUMULATIVE: number[] = (() => {
  const table = new Array<number>(TABLE_MAX_LEVEL + 1).fill(0);
  let points = 0;

  for (let level = 1; level < TABLE_MAX_LEVEL; level++) {
    points += Math.floor(level + 300 * Math.pow(2, level / 7));
    table[level + 1] = Math.floor(points / 4);
  }

  return table;
})();

/** Total XP required to reach a level from scratch. */
export function xpForLevel(level: number): number {
  if (level <= 1) return 0;
  if (level <= TABLE_MAX_LEVEL) return CUMULATIVE[level];

  let points = 0;
  for (let n = 1; n < level; n++) {
    points += Math.floor(n + 300 * Math.pow(2, n / 7));
  }

  return Math.floor(points / 4);
}

/** Progress through the current level, as a percentage from 0 to 100. */
export function xpProgressPercent(totalXp: number, level: number): number {
  const floor = xpForLevel(level);
  const ceiling = xpForLevel(level + 1);
  const band = ceiling - floor;

  if (band <= 0) return 0;

  const progress = ((totalXp - floor) / band) * 100;
  return Math.max(0, Math.min(100, progress));
}

/** XP still needed to reach the next level. */
export function xpToNextLevel(totalXp: number, level: number): number {
  return Math.max(0, xpForLevel(level + 1) - totalXp);
}
