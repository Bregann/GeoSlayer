/**
 * Tests for helpers/poiVisit.ts (Stage 04 task 4).
 *
 * Also a parity check: decayMultiplier here must agree with the server's
 * GeoSlayer.Domain/Services/Skills/VisitDecay.cs, or the app previews a reward the
 * server will not pay.
 *
 *   node helpers/__tests__/poiVisit.test.mjs
 */

import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const source = readFileSync(join(here, '..', 'poiVisit.ts'), 'utf8');

// Same structural stripper as the other helper suites, plus interface removal.
const js = source
  .replace(/^import type .*?;$/gms, '')
  .replace(/^export interface [\s\S]*?^\}$/gm, '')
  .replace(/^export /gm, '')
  .replace(/\)\s*:\s*[A-Za-z_$][\w$]*(\s*\|\s*[A-Za-z_$][\w$]*)*(\[\])?(\s*\|\s*null)?\s*\{/g, ') {')
  .replace(/:\s*\{[^{}]*\}\s*,?\s*\)/g, ')')
  .replace(/:\s*[A-Za-z_$][\w$]*(\s*\|\s*[A-Za-z_$][\w$]*)*(\[\])?(?=\s*[,)=\n])/g, '');

const exported = ['decayLabel', 'decayMultiplier', 'previewXp', 'visitBlockedReason', 'summariseVisit'];

const module = await import(
  `data:text/javascript,${encodeURIComponent(`${js}\nexport { ${exported.join(', ')} };`)}`
);

const { decayLabel, decayMultiplier, previewXp, visitBlockedReason, summariseVisit } = module;

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

// ── Decay parity with the server (§3.4) ──────────────────────────

check('first visit pays full', () => {
  assert.equal(decayMultiplier(0), 1);
});

check('second visit is ~67%', () => {
  assert.ok(Math.abs(decayMultiplier(1) - 0.667) < 0.001);
});

check('fifth visit is ~29%', () => {
  assert.ok(Math.abs(decayMultiplier(5) - 0.286) < 0.001);
});

check('decay floors at 5%', () => {
  assert.equal(decayMultiplier(1000), 0.05);
});

check('decay never rises', () => {
  for (let n = 1; n < 200; n++) {
    assert.ok(decayMultiplier(n) <= decayMultiplier(n - 1), `rose at ${n}`);
  }
});

check('a negative count does not pay more than full', () => {
  assert.equal(decayMultiplier(-3), 1);
});

// ── Preview XP matches the server's floor-then-clamp ─────────────

check('preview XP applies the multiplier', () => {
  assert.equal(previewXp(100, 0), 100);
  assert.equal(previewXp(100, 1), 66);
  assert.equal(previewXp(100, 5), 28);
});

check('preview XP never reaches zero', () => {
  // Matches VisitDecay.XpForVisit, which floors at 1 rather than 0.
  assert.equal(previewXp(1, 10000), 1);
});

// ── Labels ───────────────────────────────────────────────────────

check('a fresh POI has no decay label', () => {
  assert.equal(decayLabel(0), null);
});

check('a visited POI explains its reduced yield', () => {
  const label = decayLabel(3);
  assert.ok(label.includes('3 times'), `got: ${label}`);
  assert.ok(label.includes('40%'), `got: ${label}`);
});

check('one visit is singular', () => {
  assert.ok(decayLabel(1).includes('1 time —'), `got: ${decayLabel(1)}`);
});

// ── Range gating ─────────────────────────────────────────────────

check('an in-range POI is not blocked', () => {
  assert.equal(visitBlockedReason({ inRange: true, distanceMetres: 10 }), null);
});

check('an out-of-range POI says how far', () => {
  const reason = visitBlockedReason({ inRange: false, distanceMetres: 123.6 });
  assert.equal(reason, 'Move closer — 124m away');
});

// ── Visit summary ────────────────────────────────────────────────

function result(over = {}) {
  return {
    sessionToken: 'abc',
    poiId: 1,
    poiName: 'Test Garden',
    skill: 'Foraging',
    skillXpEarned: 40,
    adventurerXpEarned: 10,
    skillLevel: 3,
    levelledUp: false,
    isFirstVisit: false,
    visitCount: 1,
    totalVisits: 1,
    decayMultiplier: 1,
    materials: [],
    unlocks: [],
    ...over,
  };
}

check('a first visit is called out', () => {
  const text = summariseVisit(result({ isFirstVisit: true }));
  assert.ok(text.includes('First visit'), `got: ${text}`);
  assert.ok(text.includes('+40 Foraging XP'), `got: ${text}`);
});

check('a decayed visit shows the yield percentage', () => {
  const text = summariseVisit(result({ decayMultiplier: 0.5 }));
  assert.ok(text.includes('50% yield'), `got: ${text}`);
});

check('a full-yield repeat visit does not claim decay', () => {
  const text = summariseVisit(result({ decayMultiplier: 1 }));
  assert.ok(!text.includes('yield'), `got: ${text}`);
  assert.ok(!text.includes('First visit'), `got: ${text}`);
});

check('a level-up is called out', () => {
  const text = summariseVisit(result({ levelledUp: true, skillLevel: 7 }));
  assert.ok(text.includes('Level 7'), `got: ${text}`);
});

console.log(
  failures === 0
    ? '\nAll POI visit helper tests passed.'
    : `\n${failures} test(s) failed.`,
);

process.exit(failures === 0 ? 0 : 1);
