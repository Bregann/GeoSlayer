/**
 * Tests for helpers/progression.ts — the presentation logic behind the skills and
 * upgrade screens (Stage 02 task 5).
 *
 * Runs on plain Node with no dependencies, in the same style as xpCurve.test.mjs: the
 * app has no test runner configured and this environment has no node_modules, so the
 * screens themselves cannot be rendered or typechecked here. Keeping the logic pure and
 * testing it directly is what makes any of task 5 verifiable at all — the components are
 * left as thin rendering over these functions.
 *
 *   node helpers/__tests__/progression.test.mjs
 *
 * If a real test runner is added later, port these cases and delete this file.
 */

import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const source = readFileSync(join(here, '..', 'progression.ts'), 'utf8');

// Strip TypeScript. The previous approach listed every annotation by hand and broke the
// moment one was missed, so this removes them structurally instead:
//   - type-only imports
//   - return types on function declarations  ( "): Foo {"  ->  ") {" )
//   - generic arguments on `new Map<...>` / `new Array<...>`
//   - annotations on parameters and consts, up to a `,` `)` `=` or newline
const js = source
  .replace(/^import type .*?;$/gms, '')
  .replace(/^export /gm, '')
  .replace(/new (Map|Array|Set)<[^>]*>/g, 'new $1')
  // Return type: only after a parameter list's closing paren.
  .replace(/\)\s*:\s*[A-Za-z_$][\w$]*(\s*\|\s*[A-Za-z_$][\w$]*)*(\[\])?(\s*\|\s*null)?\s*\{/g, ') {')
  // Object-literal return types, e.g. `): { level: number; entries: X[] }[] {`
  .replace(/\)\s*:\s*\{[^{}]*\}\[\]\s*\{/g, ') {')
  // Parameter and variable annotations.
  .replace(/:\s*Record<[^>]*>/g, '')
  .replace(/:\s*\{[^{}]*\}\[\]/g, '')
  .replace(/:\s*[A-Za-z_$][\w$]*(\s*\|\s*[A-Za-z_$][\w$]*)*(\[\])?(?=\s*[,)=\n])/g, '');

const exported = [
  'skillIcon',
  'levelProgressPercent',
  'xpRemaining',
  'groupLockedByLevel',
  'sortSkillsByLevel',
  'groupUpgradesByCategory',
  'rankLabel',
  'purchaseBlockedReason',
  'formatEffect',
  'currentEffectLabel',
  'nextUnlockSummary',
  'nextTierLabel',
];

const module = await import(
  `data:text/javascript,${encodeURIComponent(`${js}\nexport { ${exported.join(', ')} };`)}`
);

const {
  skillIcon,
  levelProgressPercent,
  xpRemaining,
  groupLockedByLevel,
  sortSkillsByLevel,
  groupUpgradesByCategory,
  rankLabel,
  purchaseBlockedReason,
  formatEffect,
  currentEffectLabel,
  nextUnlockSummary,
  nextTierLabel,
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

/** An upgrade shaped like the server's UpgradeDto. */
function upgrade(over = {}) {
  return {
    key: 'worker_slot',
    name: 'Extra Worker Slot',
    category: 'Workers',
    description: '',
    rank: 0,
    maxRank: 5,
    effectPerRank: 1,
    currentEffect: 0,
    nextRankCost: 1,
    minAdventurerLevel: 1,
    isAvailable: true,
    canAfford: true,
    ...over,
  };
}

// ── Progress bars ────────────────────────────────────────────────

check('progress is 0% at the level floor', () => {
  assert.equal(levelProgressPercent(83, 83, 174), 0);
});

check('progress is 50% mid-band', () => {
  assert.ok(Math.abs(levelProgressPercent(128.5, 83, 174) - 50) < 0.01);
});

check('progress is clamped to 0..100', () => {
  assert.equal(levelProgressPercent(0, 83, 174), 0);
  assert.equal(levelProgressPercent(9999, 83, 174), 100);
});

check('progress does not divide by zero on a degenerate band', () => {
  assert.equal(levelProgressPercent(50, 100, 100), 0);
  assert.equal(levelProgressPercent(50, 200, 100), 0);
});

check('xpRemaining never goes negative', () => {
  assert.equal(xpRemaining(200, 174), 0);
  assert.equal(xpRemaining(100, 174), 74);
});

// ── The roadmap (§3.1c) ──────────────────────────────────────────

// Field names mirror the server's LockedSkillDto exactly. A mismatch here is not
// cosmetic: reading the wrong one renders a blank label, which is how the original
// `name` / `displayName` split was caught.
const LOCKED = [
  { displayName: 'Claims', payload: 'Claims', unlockType: 'System', unlocksAtAdventurerLevel: 5 },
  { displayName: 'Fishing', payload: 'Fishing', unlockType: 'Skill', unlocksAtAdventurerLevel: 3 },
  { displayName: 'Woodcutting', payload: 'Woodcutting', unlockType: 'Skill', unlocksAtAdventurerLevel: 5 },
];

check('locked ladder is grouped by level, ascending', () => {
  const grouped = groupLockedByLevel(LOCKED);
  assert.deepEqual(grouped.map((g) => g.level), [3, 5]);
});

check('rungs arriving at the same level are grouped together', () => {
  const grouped = groupLockedByLevel(LOCKED);
  const atFive = grouped.find((g) => g.level === 5);
  assert.equal(atFive.entries.length, 2, 'Woodcutting and Claims both arrive at 5');
});

check('grouping an empty ladder yields nothing', () => {
  assert.deepEqual(groupLockedByLevel([]), []);
});

check('next unlock summary names the nearest rung and the distance', () => {
  const summary = nextUnlockSummary({
    adventurerLevel: 1,
    adventurerXp: 0,
    adventurerXpForCurrentLevel: 0,
    adventurerXpForNextLevel: 83,
    unlocked: [],
    locked: LOCKED,
  });

  assert.ok(summary.includes('Fishing'), `got: ${summary}`);
  assert.ok(summary.includes('level 3'), `got: ${summary}`);
  assert.ok(summary.includes('2 levels away'), `got: ${summary}`);
});

check('next unlock summary says "1 level away" in the singular', () => {
  const summary = nextUnlockSummary({
    adventurerLevel: 2,
    adventurerXp: 0,
    adventurerXpForCurrentLevel: 0,
    adventurerXpForNextLevel: 0,
    unlocked: [],
    locked: LOCKED,
  });

  assert.ok(summary.includes('1 level away'), `got: ${summary}`);
});

check('next unlock summary handles a rung already reached', () => {
  const summary = nextUnlockSummary({
    adventurerLevel: 3,
    adventurerXp: 0,
    adventurerXpForCurrentLevel: 0,
    adventurerXpForNextLevel: 0,
    unlocked: [],
    locked: LOCKED,
  });

  assert.ok(summary.includes('ready to unlock'), `got: ${summary}`);
});

check('next unlock summary is null once the ladder is exhausted', () => {
  assert.equal(
    nextUnlockSummary({
      adventurerLevel: 30,
      adventurerXp: 0,
      adventurerXpForCurrentLevel: 0,
      adventurerXpForNextLevel: 0,
      unlocked: [],
      locked: [],
    }),
    null,
  );
});

// ── Skills ───────────────────────────────────────────────────────

check('skills sort by level, highest first', () => {
  const sorted = sortSkillsByLevel([
    { skillType: 1, name: 'Foraging', xp: 100, level: 2, xpForCurrentLevel: 83, xpForNextLevel: 174 },
    { skillType: 0, name: 'Exploration', xp: 5000, level: 10, xpForCurrentLevel: 0, xpForNextLevel: 0 },
  ]);

  assert.deepEqual(sorted.map((s) => s.name), ['Exploration', 'Foraging']);
});

check('sorting does not mutate the input', () => {
  const input = [
    { skillType: 1, name: 'Foraging', xp: 100, level: 2, xpForCurrentLevel: 0, xpForNextLevel: 0 },
    { skillType: 0, name: 'Exploration', xp: 5000, level: 10, xpForCurrentLevel: 0, xpForNextLevel: 0 },
  ];
  sortSkillsByLevel(input);
  assert.equal(input[0].name, 'Foraging', 'the original array was reordered');
});

check('every ladder skill has an icon', () => {
  for (const name of [
    'Exploration', 'Foraging', 'Fishing', 'Woodcutting',
    'Cooking', 'Mining', 'Smithing', 'Farming', 'Trading',
  ]) {
    assert.notEqual(skillIcon(name), '❓', `${name} has no icon`);
  }
});

check('an unknown skill falls back rather than throwing', () => {
  assert.equal(skillIcon('Nonsense'), '❓');
});

// ── Upgrades (§3.0a) ─────────────────────────────────────────────

check('upgrades group by category', () => {
  const grouped = groupUpgradesByCategory([
    upgrade({ key: 'worker_slot', category: 'Workers' }),
    upgrade({ key: 'reveal_radius', category: 'Exploration' }),
    upgrade({ key: 'offline_cap', category: 'Idle' }),
  ]);

  assert.deepEqual(grouped.map((g) => g.category), ['Workers', 'Exploration', 'Idle']);
});

check('rank label reads "n / max"', () => {
  assert.equal(rankLabel(upgrade({ rank: 3, maxRank: 5 })), '3 / 5');
});

check('rank label reads MAX at the cap', () => {
  assert.equal(rankLabel(upgrade({ rank: 5, maxRank: 5 })), 'MAX');
});

check('a purchasable upgrade is not blocked', () => {
  assert.equal(purchaseBlockedReason(upgrade()), null);
});

check('a maxed upgrade is blocked as fully upgraded', () => {
  assert.equal(purchaseBlockedReason(upgrade({ rank: 5, maxRank: 5 })), 'Fully upgraded');
});

check('a level-gated upgrade says which level it needs', () => {
  const reason = purchaseBlockedReason(
    upgrade({ isAvailable: false, minAdventurerLevel: 3 }),
  );
  assert.ok(reason.includes('level 3'), `got: ${reason}`);
});

check('an unaffordable upgrade says the cost', () => {
  const reason = purchaseBlockedReason(
    upgrade({ canAfford: false, nextRankCost: 4 }),
  );
  assert.equal(reason, 'Costs 4 points');
});

check('a one-point cost is singular', () => {
  const reason = purchaseBlockedReason(
    upgrade({ canAfford: false, nextRankCost: 1 }),
  );
  assert.equal(reason, 'Costs 1 point');
});

check('max rank takes priority over affordability', () => {
  // At max rank nextRankCost is null; reporting a cost here would be nonsense.
  const reason = purchaseBlockedReason(
    upgrade({ rank: 5, maxRank: 5, canAfford: false, nextRankCost: null }),
  );
  assert.equal(reason, 'Fully upgraded');
});

// ── Effect formatting ────────────────────────────────────────────

check('reveal radius formats as cells, pluralised', () => {
  const u = upgrade({ key: 'reveal_radius' });
  assert.equal(formatEffect(u, 1), '+1 cell reveal radius');
  assert.equal(formatEffect(u, 2), '+2 cells reveal radius');
});

check('scholar formats as a percentage', () => {
  // The server sends 0.05 per rank, not 5 — a raw render would read "+0.05%".
  assert.equal(formatEffect(upgrade({ key: 'scholar' }), 0.1), '+10% skill XP');
});

check('offline cap formats as hours', () => {
  assert.equal(formatEffect(upgrade({ key: 'offline_cap' }), 4), '+4h offline accrual');
});

check('worker slot pluralises', () => {
  const u = upgrade({ key: 'worker_slot' });
  assert.equal(formatEffect(u, 1), '+1 worker slot');
  assert.equal(formatEffect(u, 3), '+3 worker slots');
});

check('an unknown upgrade key still renders something', () => {
  assert.equal(formatEffect(upgrade({ key: 'future_upgrade' }), 7), '+7');
});

check('rank 0 has no current effect to show', () => {
  assert.equal(currentEffectLabel(upgrade({ rank: 0 })), null);
});

check('a purchased rank shows its total effect', () => {
  assert.equal(
    currentEffectLabel(upgrade({ key: 'scholar', rank: 2, currentEffect: 0.1 })),
    '+10% skill XP',
  );
});

// ── Next tier in view (SKILL-TEMPLATE.md) ────────────────────────

function skill(over = {}) {
  return {
    skillType: 15, name: 'Foraging', xp: 100, level: 5,
    xpForCurrentLevel: 83, xpForNextLevel: 174,
    nextTierName: 'Berries', nextTierLevel: 20,
    ...over,
  };
}

check('the next tier names itself and its level', () => {
  assert.equal(nextTierLabel(skill()), 'Berries at level 20');
});

check('a reached tier says it is available', () => {
  assert.equal(nextTierLabel(skill({ level: 20 })), 'Berries available now');
});

check('a maxed skill has no next tier', () => {
  assert.equal(nextTierLabel(skill({ nextTierName: null, nextTierLevel: null })), null);
});

console.log(
  failures === 0
    ? '\nAll progression helper tests passed.'
    : `\n${failures} test(s) failed.`,
);

process.exit(failures === 0 ? 0 : 1);
