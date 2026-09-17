/**
 * Tests for helpers/districts.ts (DESIGN.md §5.5).
 *
 *   node helpers/__tests__/districts.test.mjs
 */

import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const source = readFileSync(join(here, '..', 'districts.ts'), 'utf8');

const js = source
  .replace(/^import type .*?;$/gms, '')
  .replace(/^export interface [\s\S]*?^\}$/gm, '')
  .replace(/^export /gm, '')
  .replace(/const needs: string\[\] = \[\]/g, 'const needs = []')
  .replace(/\)\s*:\s*[A-Za-z_$][\w$]*(\s*\|\s*[A-Za-z_$][\w$]*)*(\[\])?(\s*\|\s*null)?\s*\{/g, ') {')
  .replace(/:\s*[A-Za-z_$][\w$]*(\s*\|\s*[A-Za-z_$][\w$]*)*(\[\])?\s*\|\s*undefined(?=\s*[,)])/g, '')
  .replace(/:\s*[A-Za-z_$][\w$]*(\s*\|\s*[A-Za-z_$][\w$]*)*(\[\])?(?=\s*[,)=\n])/g, '');

const exported = [
  'hasDistrict', 'bonusLabel', 'joinTerrains', 'districtTitle', 'districtHint',
];

const module = await import(
  `data:text/javascript,${encodeURIComponent(`${js}\nexport { ${exported.join(', ')} };`)}`
);

const { hasDistrict, bonusLabel, joinTerrains, districtTitle, districtHint } = module;

let failures = 0;
function check(name, fn) {
  try { fn(); console.log(`  ok  ${name}`); }
  catch (err) { failures++; console.error(`FAIL  ${name}\n      ${err.message}`); }
}

function formed(over = {}) {
  return {
    districtKey: 'riverside', name: 'Riverside',
    description: 'Where the woods meet the water.',
    outputBonus: 0.08, claimCount: 2,
    nearestName: null, missingTerrains: [], missingClaims: 0, ...over,
  };
}

function unformed(over = {}) {
  return {
    districtKey: null, name: null, description: null,
    outputBonus: 0, claimCount: 1,
    nearestName: 'Riverside', missingTerrains: ['Water'], missingClaims: 1, ...over,
  };
}

// --- formed -----------------------------------------------------------------

check('a formed District is recognised', () => {
  assert.equal(hasDistrict(formed()), true);
  assert.equal(hasDistrict(unformed()), false);
  assert.equal(hasDistrict(undefined), false);
});

check('the bonus reads as a percentage of output', () => {
  assert.equal(bonusLabel(formed({ outputBonus: 0.15 })), '+15% output');
  assert.equal(bonusLabel(formed({ outputBonus: 0.08 })), '+8% output');
});

check('a formed District is titled by name', () => {
  assert.equal(districtTitle(formed()), 'Riverside');
});

check('a formed District states the bonus it pays', () => {
  const hint = districtHint(formed({ outputBonus: 0.15 }));
  assert.match(hint, /\+15% output/);
  assert.match(hint, /woods meet the water/);
});

// --- unformed: the case that has to teach -----------------------------------

check('no District says so rather than showing an empty name', () => {
  assert.equal(districtTitle(unformed()), 'No District yet');
});

check('the shortfall names the terrain to go and claim', () => {
  const hint = districtHint(unformed({ missingTerrains: ['Water'], missingClaims: 0 }));
  assert.match(hint, /Riverside/);
  assert.match(hint, /Water/);
});

check('missing Claims are counted when terrain is already held', () => {
  const hint = districtHint(unformed({ missingTerrains: [], missingClaims: 2 }));
  assert.match(hint, /2 more Claims/);
});

check('a single missing Claim reads singular', () => {
  const hint = districtHint(unformed({ missingTerrains: [], missingClaims: 1 }));
  assert.match(hint, /1 more Claim\b/);
  assert.doesNotMatch(hint, /Claims/);
});

check('terrain and Claims are both reported when both are short', () => {
  const hint = districtHint(unformed({ missingTerrains: ['Water'], missingClaims: 1 }));
  assert.match(hint, /Water/);
  assert.match(hint, /1 more Claim/);
});

check('holding everything but contiguity says so', () => {
  const hint = districtHint(unformed({ missingTerrains: [], missingClaims: 0 }));
  assert.match(hint, /neighbours/);
});

check('with no nearest District the advice is still actionable', () => {
  const hint = districtHint(unformed({ nearestName: null }));
  assert.match(hint, /[Cc]laim/);
  assert.ok(hint.length > 20);
});

check('a brand new player is told what to do', () => {
  const hint = districtHint(undefined);
  assert.match(hint, /[Cc]laim/);
});

// --- terrain joining --------------------------------------------------------

check('terrains join as a sentence would read', () => {
  assert.equal(joinTerrains(['Water']), 'Water');
  assert.equal(joinTerrains(['Water', 'Urban']), 'Water and Urban');
  assert.equal(joinTerrains(['Water', 'Urban', 'Rocky']), 'Water, Urban and Rocky');
  assert.equal(joinTerrains([]), '');
});

console.log(
  failures === 0 ? '\nAll district helper tests passed.' : `\n${failures} test(s) failed.`,
);

process.exit(failures === 0 ? 0 : 1);
