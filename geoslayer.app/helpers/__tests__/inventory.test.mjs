/**
 * Tests for helpers/inventory.ts — the inventory screen's presentation logic
 * (Stage 03 task 5).
 *
 * Same harness as progression.test.mjs: plain Node, no dependencies, because this
 * environment has no app toolchain and the .tsx screen itself cannot be rendered here.
 *
 *   node helpers/__tests__/inventory.test.mjs
 */

import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const source = readFileSync(join(here, '..', 'inventory.ts'), 'utf8');

// Same structural TypeScript stripper as progression.test.mjs.
const js = source
  .replace(/^import type .*?;$/gms, '')
  .replace(/^export /gm, '')
  .replace(/new (Map|Array|Set)<[^>]*>/g, 'new $1')
  .replace(/\)\s*:\s*[A-Za-z_$][\w$]*(\s*\|\s*[A-Za-z_$][\w$]*)*(\[\])?(\s*\|\s*null)?\s*\{/g, ') {')
  .replace(/\)\s*:\s*\{[^{}]*\}\[\]\s*\{/g, ') {')
  .replace(/:\s*Record<[^>]*>/g, '')
  .replace(/:\s*\{[^{}]*\}\[\]/g, '')
  .replace(/:\s*[A-Za-z_$][\w$]*(\s*\|\s*[A-Za-z_$][\w$]*)*(\[\])?(?=\s*[,)=\n])/g, '');

const exported = [
  'categoryIcon',
  'stackPercent',
  'stackLabel',
  'stackWarning',
  'tierLabel',
  'sortByUrgency',
  'formatPickups',
  'hasOverflow',
  'totalUnits',
];

const module = await import(
  `data:text/javascript,${encodeURIComponent(`${js}\nexport { ${exported.join(', ')} };`)}`
);

const {
  categoryIcon,
  stackPercent,
  stackLabel,
  stackWarning,
  tierLabel,
  sortByUrgency,
  formatPickups,
  hasOverflow,
  totalUnits,
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

/** An item shaped like the server's InventoryItemDto. */
function item(over = {}) {
  return {
    materialId: 1,
    key: 'scrap',
    name: 'Scrap',
    tier: 1,
    category: 4,
    skillType: null,
    quantity: 100,
    stackCap: 1000,
    isUnique: false,
    isNearCap: false,
    isFull: false,
    ...over,
  };
}

// ── Stack display ────────────────────────────────────────────────

check('stack percent reflects quantity against cap', () => {
  assert.equal(stackPercent(item({ quantity: 250, stackCap: 1000 })), 25);
});

check('stack percent is clamped to 0..100', () => {
  assert.equal(stackPercent(item({ quantity: 5000, stackCap: 1000 })), 100);
  assert.equal(stackPercent(item({ quantity: -5, stackCap: 1000 })), 0);
});

check('stack percent does not divide by zero', () => {
  assert.equal(stackPercent(item({ stackCap: 0 })), 0);
});

check('stack label shows quantity against cap', () => {
  assert.equal(stackLabel(item({ quantity: 120, stackCap: 1000 })), '120 / 1,000');
});

// ── Overflow warnings (§7.4) ─────────────────────────────────────

check('a full stack warns that extra becomes Dust', () => {
  const warning = stackWarning(item({ isFull: true }));
  assert.ok(warning.includes('Dust'), `got: ${warning}`);
});

check('a near-cap stack warns before it overflows', () => {
  assert.equal(stackWarning(item({ isNearCap: true })), 'Nearly full');
});

check('a healthy stack has no warning', () => {
  assert.equal(stackWarning(item()), null);
});

check('full takes priority over near-cap', () => {
  // A full stack is also near cap; the more urgent message must win.
  const warning = stackWarning(item({ isFull: true, isNearCap: true }));
  assert.ok(warning.includes('Dust'), `got: ${warning}`);
});

// ── Tiers ────────────────────────────────────────────────────────

check('tier 1 gets no badge', () => {
  assert.equal(tierLabel(item({ tier: 1 })), null);
});

check('higher tiers get a badge', () => {
  assert.equal(tierLabel(item({ tier: 3 })), 'T3');
});

// ── Ordering ─────────────────────────────────────────────────────

check('fullest stacks sort first', () => {
  const sorted = sortByUrgency([
    item({ name: 'Low', quantity: 10, stackCap: 1000 }),
    item({ name: 'High', quantity: 900, stackCap: 1000 }),
    item({ name: 'Mid', quantity: 500, stackCap: 1000 }),
  ]);

  assert.deepEqual(sorted.map((i) => i.name), ['High', 'Mid', 'Low']);
});

check('sorting does not mutate the input', () => {
  const input = [
    item({ name: 'Low', quantity: 10 }),
    item({ name: 'High', quantity: 900 }),
  ];
  sortByUrgency(input);
  assert.equal(input[0].name, 'Low');
});

check('equal fullness falls back to name order', () => {
  const sorted = sortByUrgency([
    item({ name: 'Beta', quantity: 100 }),
    item({ name: 'Alpha', quantity: 100 }),
  ]);
  assert.deepEqual(sorted.map((i) => i.name), ['Alpha', 'Beta']);
});

// ── Pickups ──────────────────────────────────────────────────────

check('pickups read as a compact list', () => {
  const text = formatPickups([
    { materialId: 1, key: 'scrap', name: 'Scrap', tier: 1, category: 4, quantity: 3, overflowConvertedToDust: 0 },
    { materialId: 2, key: 'timber_oak', name: 'Oak Timber', tier: 2, category: 1, quantity: 1, overflowConvertedToDust: 0 },
  ]);

  assert.equal(text, '+3 Scrap, +1 Oak Timber');
});

check('a gain of zero is not shown', () => {
  // A fully overflowed material lands with quantity 0; announcing "+0" would be noise.
  const text = formatPickups([
    { materialId: 1, key: 'scrap', name: 'Scrap', tier: 1, category: 4, quantity: 0, overflowConvertedToDust: 5 },
  ]);

  assert.equal(text, null);
});

check('no gains yields null rather than an empty string', () => {
  assert.equal(formatPickups([]), null);
});

check('overflow is detected so the player can be told', () => {
  assert.equal(
    hasOverflow([
      { materialId: 1, key: 'scrap', name: 'Scrap', tier: 1, category: 4, quantity: 0, overflowConvertedToDust: 5 },
    ]),
    true,
  );

  assert.equal(
    hasOverflow([
      { materialId: 1, key: 'scrap', name: 'Scrap', tier: 1, category: 4, quantity: 5, overflowConvertedToDust: 0 },
    ]),
    false,
  );
});

// ── Totals ───────────────────────────────────────────────────────

check('total units sums across categories', () => {
  const total = totalUnits({
    distinctMaterials: 3,
    nearCapCount: 0,
    categories: [
      { category: 1, name: 'Woodland', items: [item({ quantity: 10 }), item({ quantity: 5 })] },
      { category: 4, name: 'Urban', items: [item({ quantity: 7 })] },
    ],
  });

  assert.equal(total, 22);
});

check('an empty inventory totals zero', () => {
  assert.equal(totalUnits({ distinctMaterials: 0, nearCapCount: 0, categories: [] }), 0);
});

check('every seeded category has an icon', () => {
  for (const name of [
    'Dust', 'Woodland', 'Water', 'Farmland',
    'Urban', 'Industrial', 'Rocky', 'Coastal', 'Relic',
  ]) {
    assert.notEqual(categoryIcon(name), '📦', `${name} has no icon`);
  }
});

console.log(
  failures === 0
    ? '\nAll inventory helper tests passed.'
    : `\n${failures} test(s) failed.`,
);

process.exit(failures === 0 ? 0 : 1);
