/**
 * Parity check: helpers/xpCurve.ts must agree with the server's
 * GeoSlayer.Domain/Services/Progression/XpCurve.cs.
 *
 * The HUD draws its progress bar from the client copy while the server decides the
 * actual level, so a drift between the two shows up as a bar that disagrees with the
 * number beside it. The expected values below are the same ones asserted in
 * GeoSlayer.Tests/Services/Progression/XpCurveTests.cs.
 *
 * Runs on plain Node with no dependencies — the app has no test runner configured, and
 * xpCurve.ts imports nothing, so this strips the type annotations and executes it:
 *
 *   node helpers/__tests__/xpCurve.test.mjs
 *
 * If a real test runner is added later, port these cases and delete this file.
 */

import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const source = readFileSync(join(here, '..', 'xpCurve.ts'), 'utf8');

// Strip TypeScript annotations. Deliberately crude: it only has to handle this one
// dependency-free file, and it fails loudly if that stops being true.
const js = source
  .replace(/^export /gm, '')
  .replace(/new Array<number>/g, 'new Array')
  .replace(/: number\[\]/g, '')
  .replace(/: number/g, '');

const module = await import(
  `data:text/javascript,${encodeURIComponent(`${js}\nexport { xpForLevel, xpProgressPercent, xpToNextLevel };`)}`
);

const { xpForLevel, xpProgressPercent, xpToNextLevel } = module;

let failures = 0;
function check(name, fn) {
  try {
    fn();
    console.log(`  ok  ${name}`);
  } catch (err) {
    failures++;
    console.error(`FAIL  ${name}\n      ${err.message}`);
  }
}

// The same known values the C# tests assert.
const KNOWN_LEVELS = [
  [1, 0],
  [2, 83],
  [3, 174],
  [10, 1_154],
  [20, 4_470],
  [30, 13_363],
  [50, 101_333],
  [60, 273_742],
  [70, 737_627],
  [92, 6_517_253],
  [99, 13_034_431],
];

for (const [level, expected] of KNOWN_LEVELS) {
  check(`xpForLevel(${level}) === ${expected.toLocaleString()}`, () => {
    assert.equal(xpForLevel(level), expected);
  });
}

check('xpForLevel is 0 at and below level 1', () => {
  assert.equal(xpForLevel(1), 0);
  assert.equal(xpForLevel(0), 0);
  assert.equal(xpForLevel(-5), 0);
});

check('xpForLevel is strictly increasing', () => {
  for (let level = 2; level <= 200; level++) {
    assert.ok(
      xpForLevel(level) > xpForLevel(level - 1),
      `level ${level} is not above level ${level - 1}`,
    );
  }
});

check('xpForLevel keeps growing beyond the table', () => {
  assert.ok(xpForLevel(205) > xpForLevel(200));
});

// The bar is the reason this file exists: the old HUD divided by `level * 100`, which
// pegs at 100% once xp means cumulative lifetime XP.
check('progress is 0% at a level floor', () => {
  assert.equal(xpProgressPercent(xpForLevel(50), 50), 0);
});

check('progress is 50% mid-band', () => {
  const mid = (xpForLevel(50) + xpForLevel(51)) / 2;
  assert.ok(Math.abs(xpProgressPercent(mid, 50) - 50) < 0.01);
});

check('progress approaches but does not reach 100% below the next level', () => {
  const percent = xpProgressPercent(xpForLevel(51) - 1, 50);
  assert.ok(percent > 99 && percent < 100, `got ${percent}`);
});

check('progress is clamped to 0..100', () => {
  assert.equal(xpProgressPercent(0, 50), 0, 'below the band should clamp to 0');
  assert.equal(xpProgressPercent(xpForLevel(99), 50), 100, 'above the band should clamp to 100');
});

check('xpToNextLevel from zero is the level 2 requirement', () => {
  assert.equal(xpToNextLevel(0, 1), 83);
});

check('xpToNextLevel never goes negative', () => {
  assert.equal(xpToNextLevel(xpForLevel(99), 50), 0);
});

console.log(
  failures === 0
    ? `\nall checks passed (${KNOWN_LEVELS.length} known values + 8 properties)`
    : `\n${failures} check(s) failed`,
);

process.exit(failures === 0 ? 0 : 1);
