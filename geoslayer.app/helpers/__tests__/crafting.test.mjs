/**
 * Tests for helpers/crafting.ts (Stage 06 task 4).
 *
 *   node helpers/__tests__/crafting.test.mjs
 */

import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const source = readFileSync(join(here, '..', 'crafting.ts'), 'utf8');

const js = source
  .replace(/^import type .*?;$/gms, '')
  .replace(/^export type [\s\S]*?;$/gm, '')
  .replace(/^export interface [\s\S]*?^\}$/gm, '')
  .replace(/^export /gm, '')
  .replace(/\)\s*:\s*\{[^{}]*\}\[\]\s*\{/g, ') {')
  .replace(/\)\s*:\s*[A-Za-z_$][\w$]*(\s*\|\s*[A-Za-z_$][\w$]*)*(\[\])?(\s*\|\s*null)?\s*\{/g, ') {')
  .replace(/:\s*Record<[^>]*>/g, '')
  .replace(/new Map<[^>]*>/g, 'new Map')
  .replace(/:\s*[A-Za-z_$][\w$]*(\s*\|\s*[A-Za-z_$][\w$]*)*(\[\])?(?=\s*[,)=\n])/g, '');

const exported = [
  'formatDuration', 'craftTimeRemaining', 'lockBadge', 'isTravelGated',
  'inputLabel', 'sortRecipes', 'groupItemsByKind', 'needsClaim',
];

const module = await import(
  `data:text/javascript,${encodeURIComponent(`${js}\nexport { ${exported.join(', ')} };`)}`
);

const {
  formatDuration, craftTimeRemaining, lockBadge, isTravelGated,
  inputLabel, sortRecipes, groupItemsByKind, needsClaim,
} = module;

let failures = 0;
function check(name, fn) {
  try { fn(); console.log(`  ok  ${name}`); }
  catch (err) { failures++; console.error(`FAIL  ${name}\n      ${err.message}`); }
}

function recipe(over = {}) {
  return {
    key: 'twine', name: 'Twine', description: '', skillName: 'Foraging',
    levelRequired: 1, durationSeconds: 300, xpReward: 15,
    outputName: 'Plant Fibre', outputQuantity: 2, outputKind: null,
    inputs: [], canCraft: true, lockReason: 'None', lockText: null,
    ...over,
  };
}

// ── Durations ────────────────────────────────────────────────────

check('minutes read as minutes', () => {
  assert.equal(formatDuration(300), '5m');
});

check('exact hours drop the minutes', () => {
  assert.equal(formatDuration(3600), '1h');
  assert.equal(formatDuration(7200), '2h');
});

check('mixed durations show both parts', () => {
  assert.equal(formatDuration(5400), '1h 30m');
});

check('zero reads as instant', () => {
  assert.equal(formatDuration(0), 'instant');
});

check('a finished craft says so rather than showing 0m', () => {
  assert.equal(
    craftTimeRemaining({ isComplete: true, secondsRemaining: 0 }),
    'Ready to collect',
  );
});

check('a running craft counts down', () => {
  assert.equal(
    craftTimeRemaining({ isComplete: false, secondsRemaining: 900 }),
    '15m',
  );
});

// ── Lock badges (criterion 5) ────────────────────────────────────

check('a craftable recipe has no badge', () => {
  assert.equal(lockBadge(recipe()), null);
});

check('travel-gated is its own badge, not "need materials"', () => {
  // The distinction is the point of criterion 5: "go somewhere new" is a different
  // answer from "keep playing".
  const travel = recipe({ canCraft: false, lockReason: 'TravelGated' });
  const missing = recipe({ canCraft: false, lockReason: 'MissingMaterials' });

  assert.equal(lockBadge(travel), 'TRAVEL GATED');
  assert.equal(lockBadge(missing), 'NEED MATERIALS');
  assert.notEqual(lockBadge(travel), lockBadge(missing));
});

check('level and skill locks are distinguished', () => {
  assert.equal(lockBadge(recipe({ canCraft: false, lockReason: 'LevelLocked' })), 'LEVEL LOCKED');
  assert.equal(lockBadge(recipe({ canCraft: false, lockReason: 'SkillLocked' })), 'SKILL LOCKED');
});

check('numeric lock reasons work too', () => {
  // The API may serialise the enum as an ordinal depending on configuration.
  assert.equal(lockBadge(recipe({ canCraft: false, lockReason: 3 })), 'TRAVEL GATED');
  assert.equal(isTravelGated(recipe({ canCraft: false, lockReason: 3 })), true);
});

// ── Inputs ───────────────────────────────────────────────────────

check('input label shows held against required', () => {
  assert.equal(
    inputLabel({ held: 3, quantity: 5, materialId: 1, key: 'x', name: 'X', hasEnough: false, isTravelGated: false }),
    '3 / 5',
  );
});

// ── Ordering ─────────────────────────────────────────────────────

check('craftable recipes lead, travel-gated trail', () => {
  const sorted = sortRecipes([
    recipe({ key: 'travel', canCraft: false, lockReason: 'TravelGated' }),
    recipe({ key: 'ready', canCraft: true }),
    recipe({ key: 'level', canCraft: false, lockReason: 'LevelLocked' }),
    recipe({ key: 'materials', canCraft: false, lockReason: 'MissingMaterials' }),
  ]);

  assert.deepEqual(sorted.map((r) => r.key), ['ready', 'materials', 'level', 'travel']);
});

check('sorting does not mutate the input', () => {
  const input = [recipe({ key: 'b', canCraft: false, lockReason: 'TravelGated' }), recipe({ key: 'a' })];
  sortRecipes(input);
  assert.equal(input[0].key, 'b');
});

// ── Items ────────────────────────────────────────────────────────

function item(over = {}) {
  return {
    id: 1, itemId: 1, key: 'satchel', name: 'Satchel', description: '',
    kind: 'Gear', slot: 'Body', modifierText: '+20% skill XP', tier: 1,
    quantity: 1, isEquipped: false, claimId: null, claimName: null,
    ...over,
  };
}

check('items group by kind', () => {
  const grouped = groupItemsByKind([
    item({ kind: 'Gear' }),
    item({ kind: 'Building' }),
    item({ kind: 'Gear' }),
  ]);

  assert.deepEqual(grouped.map((g) => g.kind), ['Gear', 'Building']);
  assert.equal(grouped[0].items.length, 2);
});

check('numeric kinds map to names', () => {
  const grouped = groupItemsByKind([item({ kind: 2 })]);
  assert.equal(grouped[0].kind, 'Building');
});

check('an unplaced building needs a Claim', () => {
  assert.equal(needsClaim(item({ kind: 'Building', claimId: null })), true);
  assert.equal(needsClaim(item({ kind: 'Building', claimId: 4 })), false);
});

check('gear never needs a Claim', () => {
  assert.equal(needsClaim(item({ kind: 'Gear', claimId: null })), false);
});

console.log(
  failures === 0 ? '\nAll crafting helper tests passed.' : `\n${failures} test(s) failed.`,
);

process.exit(failures === 0 ? 0 : 1);
