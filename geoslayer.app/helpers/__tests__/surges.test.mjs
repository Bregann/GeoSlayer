/**
 * Tests for helpers/surges.ts (DESIGN.md §5.6).
 *
 *   node helpers/__tests__/surges.test.mjs
 */

import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const source = readFileSync(join(here, '..', 'surges.ts'), 'utf8');

const js = source
  .replace(/^import type .*?;$/gms, '')
  .replace(/^export interface [\s\S]*?^\}$/gm, '')
  .replace(/^export /gm, '')
  .replace(/\)\s*:\s*[A-Za-z_$][\w$]*(\s*\|\s*[A-Za-z_$][\w$]*)*(\[\])?(\s*\|\s*null)?\s*\{/g, ') {')
  .replace(/:\s*[A-Za-z_$][\w$]*(\s*\|\s*[A-Za-z_$][\w$]*)*(\[\])?\s*=\s*new Date\(\)/g, ' = new Date()')
  .replace(/:\s*[A-Za-z_$][\w$]*(\s*\|\s*[A-Za-z_$][\w$]*)*(\[\])?(?=\s*[,)=\n])/g, '');

const exported = [
  'minutesRemaining', 'activeSurges', 'multiplierLabel',
  'remainingLabel', 'surgeSummary', 'surgeHint',
];

const module = await import(
  `data:text/javascript,${encodeURIComponent(`${js}\nexport { ${exported.join(', ')} };`)}`
);

const {
  minutesRemaining, activeSurges, multiplierLabel,
  remainingLabel, surgeSummary, surgeHint,
} = module;

let failures = 0;
function check(name, fn) {
  try { fn(); console.log(`  ok  ${name}`); }
  catch (err) { failures++; console.error(`FAIL  ${name}\n      ${err.message}`); }
}

const now = new Date('2026-09-17T12:00:00Z');

function surge(over = {}) {
  return {
    id: 1, description: 'Mining in the hills', targetTerrain: 'Rocky',
    targetSkill: 'Mining', multiplier: 2, endsUtc: '2026-09-17T13:00:00Z', ...over,
  };
}

function inMinutes(n) {
  return new Date(now.getTime() + n * 60_000).toISOString();
}

// --- time -------------------------------------------------------------------

check('minutes remaining counts down from the end time', () => {
  assert.equal(minutesRemaining(surge({ endsUtc: inMinutes(45) }), now), 45);
});

check('an ended surge never reports negative time', () => {
  assert.equal(minutesRemaining(surge({ endsUtc: inMinutes(-30) }), now), 0);
});

check('short remainders read in minutes', () => {
  assert.equal(remainingLabel(surge({ endsUtc: inMinutes(45) }), now), '45m left');
});

check('longer remainders read in hours and minutes', () => {
  assert.equal(remainingLabel(surge({ endsUtc: inMinutes(130) }), now), '2h 10m left');
  assert.equal(remainingLabel(surge({ endsUtc: inMinutes(120) }), now), '2h left');
});

// --- filtering and ordering -------------------------------------------------

check('ended surges are filtered out', () => {
  const active = activeSurges([
    surge({ id: 1, endsUtc: inMinutes(-1) }),
    surge({ id: 2, endsUtc: inMinutes(30) }),
  ], now);

  assert.deepEqual(active.map((s) => s.id), [2]);
});

check('the soonest to expire leads', () => {
  const active = activeSurges([
    surge({ id: 1, endsUtc: inMinutes(180) }),
    surge({ id: 2, endsUtc: inMinutes(20) }),
    surge({ id: 3, endsUtc: inMinutes(90) }),
  ], now);

  assert.deepEqual(active.map((s) => s.id), [2, 3, 1]);
});

check('filtering does not mutate the caller array', () => {
  const input = [
    surge({ id: 1, endsUtc: inMinutes(180) }),
    surge({ id: 2, endsUtc: inMinutes(20) }),
  ];
  activeSurges(input, now);
  assert.deepEqual(input.map((s) => s.id), [1, 2]);
});

// --- labels -----------------------------------------------------------------

check('whole multipliers lose the decimal', () => {
  assert.equal(multiplierLabel(surge({ multiplier: 2 })), '2×');
});

check('fractional multipliers keep one decimal', () => {
  assert.equal(multiplierLabel(surge({ multiplier: 2.5 })), '2.5×');
});

check('the summary leads with the multiplier and what it applies to', () => {
  const text = surgeSummary([surge({ endsUtc: inMinutes(30) })], now);
  assert.match(text, /2×/);
  assert.match(text, /Mining in the hills/);
});

check('with nothing running there is no banner at all', () => {
  assert.equal(surgeSummary([], now), null);
  assert.equal(surgeHint([], now), null);
  assert.equal(surgeSummary([surge({ endsUtc: inMinutes(-5) })], now), null);
});

check('a single surge is invited, not demanded', () => {
  const hint = surgeHint([surge({ endsUtc: inMinutes(30) })], now);
  assert.match(hint, /30m left/);
  assert.doesNotMatch(hint, /lose|lost|hurry|miss|wasted|!/i);
});

check('other nearby surges are counted rather than listed', () => {
  const hint = surgeHint([
    surge({ id: 1, endsUtc: inMinutes(20) }),
    surge({ id: 2, endsUtc: inMinutes(50) }),
    surge({ id: 3, endsUtc: inMinutes(80) }),
  ], now);

  assert.match(hint, /20m left/);
  assert.match(hint, /2 other surges/);
});

check('exactly one other surge reads singular', () => {
  const hint = surgeHint([
    surge({ id: 1, endsUtc: inMinutes(20) }),
    surge({ id: 2, endsUtc: inMinutes(50) }),
  ], now);

  assert.match(hint, /1 other surge\b/);
  assert.doesNotMatch(hint, /surges/);
});

console.log(
  failures === 0 ? '\nAll surge helper tests passed.' : `\n${failures} test(s) failed.`,
);

process.exit(failures === 0 ? 0 : 1);
