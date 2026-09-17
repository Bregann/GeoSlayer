/**
 * Tests for helpers/clues.ts (Stage 13 task 5).
 *
 *   node helpers/__tests__/clues.test.mjs
 */

import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const source = readFileSync(join(here, '..', 'clues.ts'), 'utf8');

const js = source
  .replace(/^import type .*?;$/gms, '')
  .replace(/^export type [\s\S]*?;$/gm, '')
  .replace(/^export interface [\s\S]*?^\}$/gm, '')
  .replace(/^const TIER_NAMES[\s\S]*?;$/gm, "const TIER_NAMES = ['Wandering','Roaming','Pilgrim','Odyssey'];")
  .replace(/ as never/g, '')
  .replace(/^export /gm, '')
  .replace(/\)\s*:\s*[A-Za-z_$][\w$]*(\s*\|\s*[A-Za-z_$][\w$]*)*(\[\])?(\s*\|\s*null)?\s*\{/g, ') {')
  .replace(/:\s*Record<[^>]*>/g, '')
  .replace(/:\s*[A-Za-z_$][\w$]*(\s*\|\s*[A-Za-z_$][\w$]*)*(\[\])?(?=\s*[,)=\n])/g, '');

const exported = [
  'tierName', 'tierIcon', 'stepProgress', 'currentStep', 'hasSearchArea',
  'skipBlockedReason', 'sortScrolls', 'availableTiers', 'stepSummary',
];

const module = await import(
  `data:text/javascript,${encodeURIComponent(`${js}\nexport { ${exported.join(', ')} };`)}`
);

const {
  tierName, tierIcon, stepProgress, currentStep, hasSearchArea,
  skipBlockedReason, sortScrolls, availableTiers, stepSummary,
} = module;

let failures = 0;
function check(name, fn) {
  try { fn(); console.log(`  ok  ${name}`); }
  catch (err) { failures++; console.error(`FAIL  ${name}\n      ${err.message}`); }
}

function step(over = {}) {
  return {
    stepIndex: 0, stepType: 'Direct', riddleText: 'Seek out the old church.',
    searchLat: null, searchLng: null, searchRadius: null,
    isSolved: false, wasSkipped: false, solvedUtc: null,
    ...over,
  };
}

function scroll(over = {}) {
  return {
    id: 1, tier: 'Wandering', tierName: 'Wandering',
    currentStep: 0, stepCount: 2, isComplete: false,
    skipUsed: false, skipCost: 5,
    startedUtc: '2026-09-17T10:00:00Z', completedUtc: null,
    steps: [step()],
    ...over,
  };
}

// ── Tiers ────────────────────────────────────────────────────────

check('tier names survive an ordinal', () => {
  assert.equal(tierName(3), 'Odyssey');
  assert.equal(tierName('Pilgrim'), 'Pilgrim');
});

check('every tier has an icon', () => {
  for (const t of ['Wandering','Roaming','Pilgrim','Odyssey']) {
    assert.notEqual(tierIcon(t), '📜', `${t} has no icon`);
  }
});

// ── Progress ─────────────────────────────────────────────────────

check('progress reads as "step n of m"', () => {
  assert.equal(stepProgress(scroll({ currentStep: 1, stepCount: 4 })), 'Step 2 of 4');
});

check('a finished scroll does not overshoot its step count', () => {
  assert.equal(stepProgress(scroll({ currentStep: 2, stepCount: 2 })), 'Step 2 of 2');
});

check('the current step is the one being hunted', () => {
  const s = scroll({ currentStep: 1, steps: [step({ stepIndex: 0 }), step({ stepIndex: 1 })] });
  assert.equal(currentStep(s).stepIndex, 1);
});

check('a complete scroll has no current step', () => {
  assert.equal(currentStep(scroll({ isComplete: true })), null);
});

// ── Search areas: only coordinate steps expose one ───────────────

check('a coordinate step has a search area', () => {
  assert.equal(
    hasSearchArea(step({ searchLat: 51.5, searchLng: -0.1, searchRadius: 120 })),
    true,
  );
});

check('a direct step exposes no position', () => {
  // Handing over the coordinates would turn the riddle into a map pin.
  assert.equal(hasSearchArea(step()), false);
});

// ── Skip (5B.3: one per scroll, so it can never block) ───────────

check('skipping is available with enough curation', () => {
  assert.equal(skipBlockedReason(scroll(), 100), null);
});

check('a used skip says so', () => {
  const reason = skipBlockedReason(scroll({ skipUsed: true }), 100);
  assert.ok(reason.includes('already skipped'), `got: ${reason}`);
});

check('too little curation names the cost', () => {
  const reason = skipBlockedReason(scroll({ skipCost: 10 }), 3);
  assert.equal(reason, 'Needs 10 curation');
});

check('a finished scroll cannot be skipped', () => {
  assert.ok(skipBlockedReason(scroll({ isComplete: true }), 100).includes('finished'));
});

// ── Ordering and availability ────────────────────────────────────

check('active scrolls lead, completed follow', () => {
  const sorted = sortScrolls([
    scroll({ id: 1, isComplete: true, tier: 'Wandering' }),
    scroll({ id: 2, isComplete: false, tier: 'Odyssey' }),
  ]);

  assert.deepEqual(sorted.map((s) => s.id), [2, 1]);
});

check('active scrolls order by tier', () => {
  const sorted = sortScrolls([
    scroll({ id: 1, tier: 'Pilgrim' }),
    scroll({ id: 2, tier: 'Wandering' }),
  ]);

  assert.deepEqual(sorted.map((s) => s.id), [2, 1]);
});

check('sorting does not mutate the input', () => {
  const input = [scroll({ id: 1, isComplete: true }), scroll({ id: 2 })];
  sortScrolls(input);
  assert.equal(input[0].id, 1);
});

check('a carried tier is not offered again', () => {
  // One active scroll per tier (5B.1).
  const available = availableTiers([scroll({ tier: 'Wandering' })]);

  assert.ok(!available.includes('Wandering'));
  assert.ok(available.includes('Roaming'));
});

check('a completed tier is offered again', () => {
  const available = availableTiers([scroll({ tier: 'Wandering', isComplete: true })]);
  assert.ok(available.includes('Wandering'));
});

check('with nothing carried, every tier is offered', () => {
  assert.equal(availableTiers([]).length, 4);
});

// ── Step summary ─────────────────────────────────────────────────

check('a skipped step is distinguished from a found one', () => {
  assert.equal(stepSummary(step({ isSolved: true, wasSkipped: true })), 'Skipped');
  assert.equal(stepSummary(step({ isSolved: true })), 'Found');
  assert.equal(stepSummary(step()), 'Searching');
});

console.log(
  failures === 0 ? '\nAll clue helper tests passed.' : `\n${failures} test(s) failed.`,
);

process.exit(failures === 0 ? 0 : 1);
