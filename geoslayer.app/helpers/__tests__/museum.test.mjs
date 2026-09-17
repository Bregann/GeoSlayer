/**
 * Tests for helpers/museum.ts (Stage 12 task 4).
 *
 *   node helpers/__tests__/museum.test.mjs
 */

import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const source = readFileSync(join(here, '..', 'museum.ts'), 'utf8');

const js = source
  .replace(/^import type .*?;$/gms, '')
  .replace(/^export type [\s\S]*?;$/gm, '')
  .replace(/^export interface [\s\S]*?^\}$/gm, '')
  .replace(/^const RARITY_NAMES[\s\S]*?;$/gm, "const RARITY_NAMES = ['Common','Uncommon','Rare','Legendary'];")
  .replace(/^export /gm, '')
  .replace(/\)\s*:\s*[A-Za-z_$][\w$]*(\s*\|\s*[A-Za-z_$][\w$]*)*(\[\])?(\s*\|\s*null)?\s*\{/g, ') {')
  .replace(/:\s*Record<[^>]*>/g, '')
  .replace(/:\s*[A-Za-z_$][\w$]*(\s*\|\s*[A-Za-z_$][\w$]*)*(\[\])?(?=\s*[,)=\n])/g, '');

const exported = [
  'rarityName', 'rarityColour', 'wingIcon', 'wingPercent', 'wingProgress',
  'foundLabel', 'sortWings', 'sortEntries', 'overallPercent', 'donatableEntries',
];

const module = await import(
  `data:text/javascript,${encodeURIComponent(`${js}\nexport { ${exported.join(', ')} };`)}`
);

const {
  rarityName, rarityColour, wingIcon, wingPercent, wingProgress,
  foundLabel, sortWings, sortEntries, overallPercent, donatableEntries,
} = module;

let failures = 0;
function check(name, fn) {
  try { fn(); console.log(`  ok  ${name}`); }
  catch (err) { failures++; console.error(`FAIL  ${name}\n      ${err.message}`); }
}

function entry(over = {}) {
  return {
    key: 'terrain:Woodland', wing: 'Naturalist', name: 'Woodland',
    description: '', rarity: 'Common', unlockCondition: 'Reveal a woodland cell',
    isFound: false, firstAcquiredUtc: null, acquiredAtName: null,
    quantity: 0, donatableQuantity: 0,
    ...over,
  };
}

function wing(over = {}) {
  return { wing: 'Naturalist', name: 'Naturalist', found: 0, total: 7, isComplete: false, entries: [], ...over };
}

// ── Rarity ───────────────────────────────────────────────────────

check('rarity names survive an ordinal', () => {
  assert.equal(rarityName(3), 'Legendary');
  assert.equal(rarityName('Rare'), 'Rare');
});

check('an unknown ordinal falls back rather than throwing', () => {
  assert.equal(rarityName(99), 'Common');
});

check('rarer entries get a distinct colour', () => {
  assert.notEqual(rarityColour('Legendary'), rarityColour('Common'));
  assert.notEqual(rarityColour('Rare'), rarityColour('Uncommon'));
});

// ── Progress ─────────────────────────────────────────────────────

check('progress reads as "found of total"', () => {
  // 5A.2 calls out "7 of 48 counties" specifically, so this is the primary label.
  assert.equal(wingProgress(wing({ found: 7, total: 48 })), '7 of 48');
});

check('percent is rounded', () => {
  assert.equal(wingPercent(wing({ found: 7, total: 48 })), 15);
});

check('an empty wing does not divide by zero', () => {
  assert.equal(wingPercent(wing({ found: 0, total: 0 })), 0);
});

check('overall percent spans the whole museum', () => {
  assert.equal(overallPercent({ wings: [], totalFound: 25, totalEntries: 100, curation: 0 }), 25);
});

check('an empty museum is 0%, not NaN', () => {
  assert.equal(overallPercent({ wings: [], totalFound: 0, totalEntries: 0, curation: 0 }), 0);
});

// ── The diary line ───────────────────────────────────────────────

check('a found entry says where and when', () => {
  const label = foundLabel(entry({
    isFound: true,
    acquiredAtName: 'Durham Cathedral',
    firstAcquiredUtc: '2026-05-03T10:00:00Z',
  }));

  assert.ok(label.includes('Durham Cathedral'), `got: ${label}`);
  assert.ok(label.includes('2026'), `got: ${label}`);
});

check('a place with no date still reads', () => {
  const label = foundLabel(entry({ isFound: true, acquiredAtName: 'Somewhere' }));
  assert.equal(label, 'Found at Somewhere');
});

check('a date with no place still reads', () => {
  const label = foundLabel(entry({ isFound: true, firstAcquiredUtc: '2026-05-03T10:00:00Z' }));
  assert.ok(label.startsWith('Found '), `got: ${label}`);
  assert.ok(!label.includes('null'), `got: ${label}`);
});

check('an unfound entry has no diary line', () => {
  assert.equal(foundLabel(entry()), null);
});

check('a found entry with neither place nor date returns null, not "null"', () => {
  assert.equal(foundLabel(entry({ isFound: true })), null);
});

// ── Ordering: gaps are shown, not hidden ─────────────────────────

check('found entries lead, gaps follow', () => {
  const sorted = sortEntries([
    entry({ name: 'Unfound', isFound: false }),
    entry({ name: 'Found', isFound: true }),
  ]);

  assert.deepEqual(sorted.map((e) => e.name), ['Found', 'Unfound']);
});

check('gaps are kept, never filtered out', () => {
  // 5A.1: empty plinths are the pull. A UI that hid them would be a receipt.
  const sorted = sortEntries([entry({ isFound: false }), entry({ isFound: true })]);
  assert.equal(sorted.length, 2);
});

check('sorting does not mutate the input', () => {
  const input = [entry({ name: 'B', isFound: false }), entry({ name: 'A', isFound: true })];
  sortEntries(input);
  assert.equal(input[0].name, 'B');
});

check('empty wings sort last but are kept', () => {
  const sorted = sortWings([
    wing({ name: 'Relics', total: 0, found: 0 }),
    wing({ name: 'Naturalist', total: 7, found: 3 }),
  ]);

  assert.deepEqual(sorted.map((w) => w.name), ['Naturalist', 'Relics']);
  assert.equal(sorted.length, 2, 'an empty wing reads as "not yet", so it stays');
});

// ── Donations ────────────────────────────────────────────────────

check('donatable entries are those with spares', () => {
  const museum = {
    wings: [wing({ entries: [
      entry({ key: 'a', donatableQuantity: 0 }),
      entry({ key: 'b', donatableQuantity: 3 }),
    ] })],
    totalFound: 2, totalEntries: 2, curation: 0,
  };

  assert.deepEqual(donatableEntries(museum).map((e) => e.key), ['b']);
});

check('every wing has an icon', () => {
  for (const name of ['Cartography','Landmarks','Naturalist','Skills','Rarities','Feats','Relics','Expeditions']) {
    assert.notEqual(wingIcon(name), '📦', `${name} has no icon`);
  }
});

console.log(
  failures === 0 ? '\nAll museum helper tests passed.' : `\n${failures} test(s) failed.`,
);

process.exit(failures === 0 ? 0 : 1);
