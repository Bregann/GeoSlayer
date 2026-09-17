/**
 * Tests for helpers/patrols.ts (DESIGN.md §5.7).
 *
 *   node helpers/__tests__/patrols.test.mjs
 */

import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const source = readFileSync(join(here, '..', 'patrols.ts'), 'utf8');

const js = source
  .replace(/^import type .*?;$/gms, '')
  .replace(/^export interface [\s\S]*?^\}$/gm, '')
  .replace(/^export /gm, '')
  .replace(/^\s*now:\s*Date\s*=\s*new Date\(\),$/gm, '  now = new Date(),')
  .replace(/^\s*route:\s*PatrolRoute,$/gm, '  route,')
  .replace(/\)\s*:\s*[A-Za-z_$][\w$]*(\s*\|\s*[A-Za-z_$][\w$]*)*(\[\])?(\s*\|\s*null)?\s*\{/g, ') {')
  .replace(/:\s*[A-Za-z_$][\w$]*(\s*\|\s*[A-Za-z_$][\w$]*)*(\[\])?\s*=\s*new Date\(\)/g, ' = new Date()')
  .replace(/:\s*[A-Za-z_$][\w$]*(\s*\|\s*[A-Za-z_$][\w$]*)*(\[\])?(?=\s*[,)=\n])/g, '');

const exported = [
  'COOLDOWN_HOURS', 'MINIMUM_WAYPOINTS', 'hoursSinceCompletion', 'isReady',
  'routeStatus', 'routeSummary', 'completionLabel', 'sortRoutes',
  'draftBlockedReason', 'emptyStateMessage',
];

const module = await import(
  `data:text/javascript,${encodeURIComponent(`${js}\nexport { ${exported.join(', ')} };`)}`
);

const {
  COOLDOWN_HOURS, MINIMUM_WAYPOINTS, hoursSinceCompletion, isReady,
  routeStatus, routeSummary, completionLabel, sortRoutes,
  draftBlockedReason, emptyStateMessage,
} = module;

let failures = 0;
function check(name, fn) {
  try { fn(); console.log(`  ok  ${name}`); }
  catch (err) { failures++; console.error(`FAIL  ${name}\n      ${err.message}`); }
}

const now = new Date('2026-09-17T12:00:00Z');

function hoursAgo(n) {
  return new Date(now.getTime() - n * 3_600_000).toISOString();
}

function route(over = {}) {
  return {
    id: 1, name: 'Morning loop', waypointCount: 4, completionCount: 3,
    lastCompletedUtc: hoursAgo(30), upkeepReward: 8,
    waypoints: [], ...over,
  };
}

// --- cooldown ---------------------------------------------------------------

check('the cooldown matches the server rule', () => {
  assert.equal(COOLDOWN_HOURS, 20);
});

check('a never-walked route is ready', () => {
  assert.equal(hoursSinceCompletion(route({ lastCompletedUtc: null }), now), null);
  assert.equal(isReady(route({ lastCompletedUtc: null }), now), true);
});

check('a route walked today is not ready again', () => {
  assert.equal(isReady(route({ lastCompletedUtc: hoursAgo(3) }), now), false);
});

check('a route past the cooldown is ready', () => {
  assert.equal(isReady(route({ lastCompletedUtc: hoursAgo(21) }), now), true);
});

check('the cooldown boundary counts as ready', () => {
  assert.equal(isReady(route({ lastCompletedUtc: hoursAgo(20) }), now), true);
});

// --- status ------------------------------------------------------------------

check('a never-walked route says so', () => {
  assert.match(routeStatus(route({ lastCompletedUtc: null }), now), /Never walked/);
});

check('a ready route invites the walk', () => {
  assert.match(routeStatus(route({ lastCompletedUtc: hoursAgo(25) }), now), /Ready/);
});

check('a route on cooldown says when it returns, not that it is unavailable', () => {
  const status = routeStatus(route({ lastCompletedUtc: hoursAgo(8) }), now);
  assert.match(status, /ready in 12h/);
  assert.doesNotMatch(status, /unavailable|locked|cannot/i);
});

check('the last hour of cooldown reads naturally', () => {
  assert.match(routeStatus(route({ lastCompletedUtc: hoursAgo(19.5) }), now), /within the hour/);
});

// --- labels -------------------------------------------------------------------

check('the summary gives the shape and the pay', () => {
  const text = routeSummary(route({ waypointCount: 4, upkeepReward: 8 }));
  assert.match(text, /4 stops/);
  assert.match(text, /8 rations a day/);
});

check('a one-stop route reads singular', () => {
  assert.match(routeSummary(route({ waypointCount: 1 })), /1 stop\b/);
});

check('the pay is described as rations, never as XP', () => {
  // §5.7: routine is maintenance, novelty is progress. Calling this XP would invert it.
  assert.doesNotMatch(routeSummary(route()), /\bXP\b/i);
  assert.doesNotMatch(emptyStateMessage([]), /earns? XP|XP source/i);
});

check('completion count is shown once there is one', () => {
  assert.equal(completionLabel(route({ completionCount: 0 })), null);
  assert.match(completionLabel(route({ completionCount: 1 })), /1 time\b/);
  assert.match(completionLabel(route({ completionCount: 5 })), /5 times/);
});

// --- ordering -----------------------------------------------------------------

check('ready routes come first', () => {
  const sorted = sortRoutes([
    route({ id: 1, lastCompletedUtc: hoursAgo(2) }),
    route({ id: 2, lastCompletedUtc: hoursAgo(40) }),
    route({ id: 3, lastCompletedUtc: null }),
  ], now);

  assert.deepEqual(sorted.slice(0, 2).map((r) => r.id).sort(), [2, 3]);
  assert.equal(sorted[2].id, 1);
});

check('sorting does not mutate the caller array', () => {
  const input = [
    route({ id: 1, lastCompletedUtc: hoursAgo(2) }),
    route({ id: 2, lastCompletedUtc: null }),
  ];
  sortRoutes(input, now);
  assert.deepEqual(input.map((r) => r.id), [1, 2]);
});

// --- drafting -----------------------------------------------------------------

check('a draft below the minimum says how many more are needed', () => {
  assert.match(draftBlockedReason([]), /Add 2 more stops/);
  assert.match(draftBlockedReason([{ latitude: 1, longitude: 1 }]), /Add 1 more stop\b/);
});

check('a draft at the minimum is allowed', () => {
  const two = [{ latitude: 1, longitude: 1 }, { latitude: 2, longitude: 2 }];
  assert.equal(draftBlockedReason(two), null);
  assert.equal(MINIMUM_WAYPOINTS, 2);
});

// --- empty state ---------------------------------------------------------------

check('the empty state explains what a patrol is for', () => {
  const message = emptyStateMessage([]);
  assert.match(message, /upkeep/i);
  assert.ok(message.length > 40);
});

check('with routes there is no empty state', () => {
  assert.equal(emptyStateMessage([route()]), null);
});

console.log(
  failures === 0 ? '\nAll patrol helper tests passed.' : `\n${failures} test(s) failed.`,
);

process.exit(failures === 0 ? 0 : 1);
