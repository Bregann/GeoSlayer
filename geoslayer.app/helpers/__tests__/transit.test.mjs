/**
 * Tests for helpers/transit.ts (DESIGN.md §7.1).
 *
 *   node helpers/__tests__/transit.test.mjs
 */

import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const source = readFileSync(join(here, '..', 'transit.ts'), 'utf8');

const js = source
  .replace(/^import type .*?;$/gms, '')
  .replace(/^export interface [\s\S]*?^\}$/gm, '')
  .replace(/^export /gm, '')
  .replace(/\)\s*:\s*[A-Za-z_$][\w$]*(\s*\|\s*[A-Za-z_$][\w$]*)*(\[\])?(\s*\|\s*null)?\s*\{/g, ') {')
  .replace(/:\s*[A-Za-z_$][\w$]*(\s*\|\s*[A-Za-z_$][\w$]*)*(\[\])?\s*=\s*new Date\(\)/g, ' = new Date()')
  .replace(/:\s*[A-Za-z_$][\w$]*(\s*\|\s*[A-Za-z_$][\w$]*)*(\[\])?(?=\s*[,)=\n])/g, '');

const exported = [
  'transitSummary', 'transitHint', 'expiringSoon',
  'expiryNudge', 'redemptionSummary', 'transitOpacity',
];

const module = await import(
  `data:text/javascript,${encodeURIComponent(`${js}\nexport { ${exported.join(', ')} };`)}`
);

const {
  transitSummary, transitHint, expiringSoon,
  expiryNudge, redemptionSummary, transitOpacity,
} = module;

let failures = 0;
function check(name, fn) {
  try { fn(); console.log(`  ok  ${name}`); }
  catch (err) { failures++; console.error(`FAIL  ${name}\n      ${err.message}`); }
}

function cell(over = {}) {
  return {
    gridLat: 1, gridLng: 1, south: 51.5, west: -0.1, north: 51.51, east: -0.09,
    weight: 1, expiresUtc: '2026-09-24T10:00:00Z',
    ...over,
  };
}

// ── The overlay stays quiet when there is nothing ────────────────

check('no banked cells means no summary', () => {
  assert.equal(transitSummary([]), null);
  assert.equal(transitHint([]), null);
});

check('banked cells are counted', () => {
  assert.equal(transitSummary([cell(), cell()]), 'You passed through 2 places');
});

check('one cell reads in the singular', () => {
  assert.equal(transitSummary([cell()]), 'You passed through 1 place');
});

check('the hint explains what to do', () => {
  // The load-bearing string: hatched cells with no explanation are just noise.
  const hint = transitHint([cell()]);
  assert.ok(hint.includes('Walk near'), `got: ${hint}`);
});

// ── Expiry is a nudge, not a deadline (§7.1) ─────────────────────

check('cells lapsing within a day are flagged', () => {
  const now = new Date('2026-09-23T12:00:00Z');
  const soon = expiringSoon([cell({ expiresUtc: '2026-09-24T10:00:00Z' })], now);

  assert.equal(soon.length, 1);
});

check('cells lapsing later are not flagged', () => {
  const now = new Date('2026-09-20T12:00:00Z');
  const soon = expiringSoon([cell({ expiresUtc: '2026-09-24T10:00:00Z' })], now);

  assert.equal(soon.length, 0);
});

check('nothing expiring means no nudge', () => {
  const now = new Date('2026-09-20T12:00:00Z');
  assert.equal(expiryNudge([cell()], now), null);
});

check('the nudge counts what is fading', () => {
  const now = new Date('2026-09-23T12:00:00Z');
  const nudge = expiryNudge([cell(), cell()], now);

  assert.equal(nudge, '2 fading soon');
});

check('the nudge does not read as a deadline', () => {
  // 7.1: "an opportunity, not an obligation". Urgent phrasing would break that.
  const now = new Date('2026-09-23T12:00:00Z');
  const nudge = expiryNudge([cell()], now);

  assert.ok(!/lose|lost|hurry|expires|deadline|!/i.test(nudge), `got: ${nudge}`);
});

// ── Redemption toast ─────────────────────────────────────────────

check('an ordinary walk stays quiet', () => {
  assert.equal(redemptionSummary(null), null);
  assert.equal(redemptionSummary({ redeemed: 0, cellsRevealed: 0, remaining: 0, expired: 0 }), null);
});

check('a redemption says what it revealed', () => {
  const text = redemptionSummary({ redeemed: 3, cellsRevealed: 3, remaining: 5, expired: 0 });

  assert.ok(text.includes('3 places'), `got: ${text}`);
  assert.ok(text.includes('passed through'), `got: ${text}`);
});

check('a redemption revealing nothing still reports', () => {
  // Redeemed cells that were already revealed by other means.
  const text = redemptionSummary({ redeemed: 2, cellsRevealed: 0, remaining: 1, expired: 0 });

  assert.ok(text.includes('2'), `got: ${text}`);
});

// ── Opacity carries the speed grading ────────────────────────────

check('a fully banked cell is more visible than a partial one', () => {
  // A cycling-pace cell banked partially and should look fainter, rather than every
  // banked cell rendering identically.
  assert.ok(transitOpacity(1) > transitOpacity(0.3));
});

check('opacity stays within a sane band', () => {
  for (const w of [0, 0.5, 1, 5, -1]) {
    const o = transitOpacity(w);
    assert.ok(o >= 0.12 && o <= 0.45, `weight ${w} gave ${o}`);
  }
});

console.log(
  failures === 0 ? '\nAll transit helper tests passed.' : `\n${failures} test(s) failed.`,
);

process.exit(failures === 0 ? 0 : 1);
