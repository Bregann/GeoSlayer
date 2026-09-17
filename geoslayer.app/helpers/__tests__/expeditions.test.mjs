/**
 * Tests for helpers/expeditions.ts (DESIGN.md §5.4).
 *
 *   node helpers/__tests__/expeditions.test.mjs
 */

import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const source = readFileSync(join(here, '..', 'expeditions.ts'), 'utf8');

const js = source
  .replace(/^import type .*?;$/gms, '')
  .replace(/^export interface [\s\S]*?^\}$/gm, '')
  .replace(/^export /gm, '')
  .replace(/\)\s*:\s*[A-Za-z_$][\w$]*(\s*\|\s*[A-Za-z_$][\w$]*)*(\[\])?(\s*\|\s*null)?\s*\{/g, ') {')
  .replace(/:\s*[A-Za-z_$][\w$]*(\s*\|\s*[A-Za-z_$][\w$]*)*(\[\])?(?=\s*[,)=\n])/g, '');

const exported = [
  'formatDistance', 'formatAwayTime', 'timeRemaining', 'firstVisitLabel',
  'tradeOffLabel', 'destinationBlockedReason', 'sortDestinations',
  'sortExpeditions', 'idleWorkerIds', 'emptyStateMessage',
];

const module = await import(
  `data:text/javascript,${encodeURIComponent(`${js}\nexport { ${exported.join(', ')} };`)}`
);

const {
  formatDistance, formatAwayTime, timeRemaining, firstVisitLabel,
  tradeOffLabel, destinationBlockedReason, sortDestinations,
  sortExpeditions, idleWorkerIds, emptyStateMessage,
} = module;

let failures = 0;
function check(name, fn) {
  try { fn(); console.log(`  ok  ${name}`); }
  catch (err) { failures++; console.error(`FAIL  ${name}\n      ${err.message}`); }
}

function destination(over = {}) {
  return {
    poiId: 1, name: 'St Mary the Virgin', skill: 'Prayer', skillName: 'Prayer',
    firstVisitUtc: '2025-05-14T10:00:00Z', totalVisits: 3,
    distanceMetres: 2400, durationHours: 6, estimatedMaterials: 18,
    isAvailable: true, ...over,
  };
}

function expedition(over = {}) {
  return {
    id: 1, workerId: 1, poiId: 1, poiName: 'St Mary the Virgin',
    dispatchedUtc: '2026-09-17T08:00:00Z', returnsUtc: '2026-09-17T14:00:00Z',
    distanceMetres: 2400, hasReturned: false, secondsRemaining: 7200, ...over,
  };
}

// --- distance ---------------------------------------------------------------

check('short distances read in metres', () => {
  assert.equal(formatDistance(340), '340 m');
  assert.equal(formatDistance(999), '999 m');
});

check('longer distances read in kilometres', () => {
  assert.equal(formatDistance(2400), '2.4 km');
  assert.equal(formatDistance(1000), '1.0 km');
});

check('very long distances drop the decimal', () => {
  assert.equal(formatDistance(48000), '48 km');
});

// --- duration ---------------------------------------------------------------

check('sub-hour durations read in minutes', () => {
  assert.equal(formatAwayTime(0.5), '30m');
});

check('a duration under a day reads in hours', () => {
  assert.equal(formatAwayTime(6), '6h');
});

check('multi-day durations read as days and hours', () => {
  assert.equal(formatAwayTime(28), '1d 4h');
  assert.equal(formatAwayTime(48), '2d');
});

check('a duration never rounds away to nothing', () => {
  assert.equal(formatAwayTime(0.001), '1m');
});

// --- countdown --------------------------------------------------------------

check('a worker still away shows time left', () => {
  assert.equal(timeRemaining(expedition({ secondsRemaining: 7200 })), '2h');
});

check('a returned worker says so rather than counting down', () => {
  assert.equal(timeRemaining(expedition({ hasReturned: true })), 'Returned');
  assert.equal(timeRemaining(expedition({ secondsRemaining: -50 })), 'Returned');
});

// --- labels -----------------------------------------------------------------

check('the first-visit label names when the place was found', () => {
  const label = firstVisitLabel(destination());
  assert.match(label, /First found/);
  assert.match(label, /2025/);
});

check('the trade-off line carries distance, time and yield together', () => {
  const label = tradeOffLabel(destination());
  assert.match(label, /2\.4 km/);
  assert.match(label, /6h/);
  assert.match(label, /18 materials/);
});

check('an unavailable destination explains why, not just that', () => {
  assert.equal(destinationBlockedReason(destination()), null);
  assert.match(
    destinationBlockedReason(destination({ isAvailable: false })),
    /worker is already there/,
  );
});

// --- ordering ---------------------------------------------------------------

check('destinations are ordered furthest first', () => {
  const sorted = sortDestinations([
    destination({ poiId: 1, distanceMetres: 500 }),
    destination({ poiId: 2, distanceMetres: 90000 }),
    destination({ poiId: 3, distanceMetres: 4000 }),
  ]);

  assert.deepEqual(sorted.map((d) => d.poiId), [2, 3, 1]);
});

check('unavailable destinations sink below available ones', () => {
  const sorted = sortDestinations([
    destination({ poiId: 1, distanceMetres: 90000, isAvailable: false }),
    destination({ poiId: 2, distanceMetres: 500 }),
  ]);

  assert.deepEqual(sorted.map((d) => d.poiId), [2, 1]);
});

check('sorting does not mutate the caller array', () => {
  const input = [
    destination({ poiId: 1, distanceMetres: 500 }),
    destination({ poiId: 2, distanceMetres: 9000 }),
  ];
  sortDestinations(input);
  assert.deepEqual(input.map((d) => d.poiId), [1, 2]);
});

check('returned expeditions come before those still away', () => {
  const sorted = sortExpeditions([
    expedition({ id: 1, secondsRemaining: 60 }),
    expedition({ id: 2, hasReturned: true, secondsRemaining: 0 }),
    expedition({ id: 3, secondsRemaining: 30 }),
  ]);

  assert.deepEqual(sorted.map((e) => e.id), [2, 3, 1]);
});

// --- worker availability ----------------------------------------------------

check('workers already away are not offered again', () => {
  const idle = idleWorkerIds(
    [{ id: 1 }, { id: 2 }, { id: 3 }],
    [expedition({ workerId: 2 })],
  );

  assert.deepEqual(idle, [1, 3]);
});

check('with every worker away nothing can be sent', () => {
  const idle = idleWorkerIds([{ id: 1 }], [expedition({ workerId: 1 })]);
  assert.deepEqual(idle, []);
});

// --- empty state ------------------------------------------------------------

check('the empty state explains how to get destinations', () => {
  const message = emptyStateMessage([]);
  assert.match(message, /[Vv]isit/);
  assert.ok(message.length > 30, 'should explain, not just say "none"');
});

check('with destinations there is no empty state', () => {
  assert.equal(emptyStateMessage([destination()]), null);
});

console.log(
  failures === 0 ? '\nAll expedition helper tests passed.' : `\n${failures} test(s) failed.`,
);

process.exit(failures === 0 ? 0 : 1);
