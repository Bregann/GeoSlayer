/**
 * Tests for helpers/idle.ts — the welcome-back screen and worker management
 * (Stage 05 tasks 4–5).
 *
 *   node helpers/__tests__/idle.test.mjs
 */

import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const source = readFileSync(join(here, '..', 'idle.ts'), 'utf8');

const js = source
  .replace(/^import type .*?;$/gms, '')
  .replace(/^export interface [\s\S]*?^\}$/gm, '')
  .replace(/^export /gm, '')
  .replace(/\)\s*:\s*[A-Za-z_$][\w$]*(\s*\|\s*[A-Za-z_$][\w$]*)*(\[\])?(\s*\|\s*null)?\s*\{/g, ') {')
  .replace(/:\s*[A-Za-z_$][\w$]*(\s*\|\s*[A-Za-z_$][\w$]*)*(\[\])?\s*=\s*new Date\(\)/g, ' = new Date()')
  .replace(/:\s*[A-Za-z_$][\w$]*(\s*\|\s*[A-Za-z_$][\w$]*)*(\[\])?(?=\s*[,)=\n])/g, '');

const exported = [
  'shouldShowWelcomeBack',
  'formatDuration',
  'summariseAccrual',
  'capWarning',
  'workerStatus',
  'terrainNote',
  'hoursUntilCap',
];

const module = await import(
  `data:text/javascript,${encodeURIComponent(`${js}\nexport { ${exported.join(', ')} };`)}`
);

const {
  shouldShowWelcomeBack,
  formatDuration,
  summariseAccrual,
  capWarning,
  workerStatus,
  terrainNote,
  hoursUntilCap,
} = module;

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

function accrual(over = {}) {
  return {
    hasAccrual: true,
    hoursAccrued: 3,
    wasCapped: false,
    offlineCapHours: 4,
    materials: [],
    skills: [],
    unlocks: [],
    adventurerXpEarned: 5,
    bonusPointsGranted: 0,
    ...over,
  };
}

function worker(over = {}) {
  return {
    id: 1,
    name: 'Worker 1',
    tier: 1,
    claimId: 2,
    claimName: 'Home',
    assignedSkill: 15,
    assignedSkillName: 'Foraging',
    lastCollectedAtUtc: '2026-09-16T08:00:00Z',
    xpPerHour: 10.5,
    materialsPerHour: 3.5,
    terrainMultiplier: 1.75,
    terrainMatches: true,
    capReachedAtUtc: '2026-09-16T12:00:00Z',
    isAtCap: false,
    isIdle: false,
    ...over,
  };
}

// ── Never nag (task 4) ───────────────────────────────────────────

check('nothing accrued means no screen', () => {
  assert.equal(shouldShowWelcomeBack(accrual({ hasAccrual: false })), false);
});

check('a null accrual means no screen', () => {
  assert.equal(shouldShowWelcomeBack(null), false);
  assert.equal(shouldShowWelcomeBack(undefined), false);
});

check('XP alone is worth showing', () => {
  const result = shouldShowWelcomeBack(
    accrual({ skills: [{ skillType: 15, name: 'Foraging', xpEarned: 24, level: 3, levelledUp: false }] }),
  );
  assert.equal(result, true);
});

check('an all-overflow accrual with no real gain is not worth showing', () => {
  // Everything converted to Dust and no XP — there is nothing to celebrate.
  const result = shouldShowWelcomeBack(
    accrual({
      materials: [{ materialId: 1, key: 'x', name: 'X', tier: 1, category: 1, quantity: 0, overflowConvertedToDust: 5 }],
    }),
  );
  assert.equal(result, false);
});

// ── Durations ────────────────────────────────────────────────────

check('whole hours read naturally', () => {
  assert.equal(formatDuration(3), '3 hours');
  assert.equal(formatDuration(1), '1 hour');
});

check('part hours fall back to minutes', () => {
  assert.equal(formatDuration(0.5), '30 minutes');
  assert.equal(formatDuration(1 / 60), '1 minute');
});

check('fractional hours keep one decimal', () => {
  assert.equal(formatDuration(3.25), '3.3 hours');
});

check('zero reads as no time', () => {
  assert.equal(formatDuration(0), 'no time');
});

// ── The headline must be specific (task 4) ───────────────────────

check('a level-up leads the summary', () => {
  const text = summariseAccrual(accrual({
    skills: [{ skillType: 15, name: 'Foraging', xpEarned: 200, level: 14, levelledUp: true }],
  }));

  assert.ok(text.includes('Foraging reached level 14'), `got: ${text}`);
});

check('materials are named and counted', () => {
  const text = summariseAccrual(accrual({
    materials: [
      { materialId: 1, key: 'timber', name: 'Timber', tier: 1, category: 1, quantity: 340, overflowConvertedToDust: 0 },
    ],
  }));

  assert.ok(text.includes('340 Timber'), `got: ${text}`);
});

check('a long material list is truncated', () => {
  const many = Array.from({ length: 6 }, (_, i) => ({
    materialId: i, key: `m${i}`, name: `Mat${i}`, tier: 1, category: 1,
    quantity: 10, overflowConvertedToDust: 0,
  }));

  const text = summariseAccrual(accrual({ materials: many }));

  assert.ok(text.includes('+3 more'), `got: ${text}`);
});

check('XP-only accrual still says something specific', () => {
  const text = summariseAccrual(accrual({
    skills: [{ skillType: 15, name: 'Foraging', xpEarned: 24, level: 3, levelledUp: false }],
  }));

  assert.equal(text, '24 XP');
});

// ── Cap nudge ────────────────────────────────────────────────────

check('no cap warning when the cap was not reached', () => {
  assert.equal(capWarning(accrual({ wasCapped: false })), null);
});

check('a capped run suggests the upgrade', () => {
  const warning = capWarning(accrual({ wasCapped: true, offlineCapHours: 4 }));
  assert.ok(warning.includes('4 hours'), `got: ${warning}`);
  assert.ok(warning.includes('Offline Cap'), `got: ${warning}`);
});

// ── Worker status ────────────────────────────────────────────────

check('an idle worker asks to be assigned', () => {
  assert.ok(workerStatus(worker({ isIdle: true })).includes('Idle'));
});

check('a working worker names its skill and Claim', () => {
  assert.equal(workerStatus(worker()), 'Foraging on Home');
});

check('a capped worker says so', () => {
  assert.ok(workerStatus(worker({ isAtCap: true })).includes('At cap'));
});

// ── Terrain wording must not imply a gate (§5.2) ─────────────────

check('matching terrain is called out as a bonus', () => {
  const note = terrainNote(worker({ terrainMatches: true }));
  assert.ok(note.includes('Good terrain'), `got: ${note}`);
});

check('mismatched terrain says base rate, never "cannot"', () => {
  const note = terrainNote(worker({ terrainMatches: false, terrainMultiplier: 1 }));

  assert.ok(note.includes('Base rate'), `got: ${note}`);
  // The wording must not suggest the worker is blocked - terrain never gates.
  assert.ok(!/cannot|can't|unable|blocked|requires/i.test(note), `got: ${note}`);
});

check('an idle worker has no terrain note', () => {
  assert.equal(terrainNote(worker({ isIdle: true })), null);
});

// ── Time to cap ──────────────────────────────────────────────────

check('hours until cap counts down', () => {
  const now = new Date('2026-09-16T10:00:00Z');
  assert.equal(hoursUntilCap(worker(), now), 2);
});

check('a passed cap reads as zero, not negative', () => {
  const now = new Date('2026-09-16T18:00:00Z');
  assert.equal(hoursUntilCap(worker(), now), 0);
});

console.log(
  failures === 0
    ? '\nAll idle helper tests passed.'
    : `\n${failures} test(s) failed.`,
);

process.exit(failures === 0 ? 0 : 1);
