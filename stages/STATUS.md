# Build status

**Current stage: `STAGE-05-workers-idle.md`**

Single source of truth for where the build is. Update this when a stage completes.

| # | Stage | State |
|---|---|---|
| 01 | Prototype | DONE |
| 02 | Progression core | DONE |
| 03 | Materials & inventory | DONE |
| 04 | Foraging | DONE |
| 05 | Workers & idle | NOT STARTED |
| 06 | Crafting | NOT STARTED |
| 07 | Fishing | NOT STARTED |
| 08 | Woodcutting | NOT STARTED |
| 09 | Cooking | NOT STARTED |
| 10 | Mining | NOT STARTED |
| 11 | Smithing | NOT STARTED |
| 12 | Museum | NOT STARTED |
| 13 | Clue scrolls | NOT STARTED |
| 14 | Retention systems | NOT STARTED |
| 15 | Remaining skills | NOT STARTED |

States: `NOT STARTED` → `IN PROGRESS` → `DONE` (or `BLOCKED`, with a reason).

## Blockers

**None.**

Stage 02's `DESIGN.md` blocker is **resolved and was stale**: the file now exists at the
repo root (1182 lines, added in `277e40e`) and covers §3.0, §3.0a, §3.0b, §3.1, §3.1c and
§3.3. The previous note here claimed it "has never existed in this repo" — that was true
when written, and is no longer.

§3.1 gives the unlock ladder concretely, so nothing was invented for it. The milestone XP
values and upgrade cost curves that §3.0a/§3.0b give only qualitatively were chosen as
tuning values and documented with their reasoning in `ProgressionDefaults.cs` — which is
what DESIGN.md asks for, since it specifies both as seeded data explicitly so they can be
retuned without a deploy.

## Manual verification queue

Things marked `NEEDS MANUAL VERIFICATION` by a stage, awaiting a human with a phone and a
pair of shoes. Anything location-dependent lands here — an agent cannot verify a walk.

### From Stage 01

Needs a human with a phone and a pair of shoes:

- Walk ~1 km with the screen off; confirm the revealed corridor matches the route.
- Leave the phone on a desk for an hour with the app backgrounded; confirm no cells reveal.
- Confirm the Android foreground-service notification appears and persists.

Needs a working app toolchain (this environment has no `geoslayer.app/node_modules`,
so the app was never typechecked or built — **acceptance criterion 10 is unverified**):

- `cd geoslayer.app && npm install && npx tsc --noEmit` — typecheck Stage 01's app
  changes (`helpers/tokenRefresh.ts`, `helpers/xpCurve.ts`, `helpers/backgroundLocation.ts`,
  `components/hud.tsx`, `app/(tabs)/index.tsx`, `types/map.ts`).
- Launch the app and confirm the map screen still renders, tracks position and draws fog.

  Partially narrowed since: `npm test` (no `node_modules` needed) checks `helpers/xpCurve.ts`
  against the server curve and the HUD progress bar across a level band. `backgroundLocation.ts`
  and the screen changes remain entirely unverified — they need the real toolchain.

~~Needs Docker~~ — **done.** The Stage 01 database tests now execute and pass.

The reason they never ran was not a missing daemon. `TestContainerSetup` is a
`[SetUpFixture]`, and NUnit scopes one to its own namespace and descendants; it sat in
`GeoSlayer.Tests.Infrastructure` while the tests live in the sibling namespace
`GeoSlayer.Tests.Services.*`. So `OneTimeSetUp` never ran, the container never started,
and every database test skipped with `Container unavailable: not started` — the `??`
fallback string, meaning no Docker error had ever occurred. Fixed by moving the fixture to
the root `GeoSlayer.Tests` namespace.

Worth noting as a process risk: the suite reported `Passed!` with a zero exit code the
whole time, so CI would have stayed green with the entire database layer untested.

### From Stage 02

All five tasks are built. `npm test` covers the app's progression logic (32 tests, no
`node_modules` required), but the screens themselves have **never been rendered or
typechecked** — this environment has no app toolchain.

Needs a working app toolchain:

- `cd geoslayer.app && npm install && npx tsc --noEmit` — typecheck the Stage 02 screens
  (`app/skills.tsx`, `app/upgrades.tsx`, `components/unlockCelebration.tsx`,
  `components/hud.tsx`, `types/progression.ts`, `helpers/progression.ts`,
  `styles/progression.ts`).
- Launch and walk the three new surfaces: the Skills screen (✨), the Upgrade screen (⚡),
  and the unlock celebration. The celebration only fires on a sync that crosses a ladder
  rung, so reaching Adventurer level 3 is the cheapest way to see it.
- Confirm the HUD still reads correctly now that `hp` and `gold` are gone — those were
  hardcoded `85/100` and `0g`, and were removed rather than left showing fake state.

Worth a human eye when the app lands, since only a real session shows it:

- **The tuning values are first-draft.** Milestone XP (1/20/50/100) and the upgrade cost
  curves are reasoned from DESIGN.md's ratios, not playtested. §3.1 says levels 1–3 should
  be reachable in the first session or two — that is the thing to check against a real walk,
  and it is a seeded-config change if it is wrong, not a code change.

### From Stage 03

Backend fully tested (29 new tests). The inventory screen's logic is unit-tested via
`npm test`, but the `.tsx` screen has **never been rendered or typechecked** here.

Needs a working app toolchain:

- Typecheck and launch `app/inventory.tsx`, and confirm the 🎒 button opens it.
- Confirm the material pickup toast appears on a sync that yields materials, and that it
  auto-dismisses (3s normally, 6s when a stack overflowed into Dust).

Needs a human with a phone, since only real geography shows it:

- **Terrain classification accuracy.** `PoiTerrainClassifier` infers terrain from the POIs
  OSM already gave us rather than from landuse polygons, so a cell in the middle of an
  untagged forest will classify as `Open` and yield base materials instead of timber.
  Walk a known wood, a known riverside and a plain residential street, and check the
  materials match. If the resolution proves too coarse, the fix is to extend the Overpass
  query with landuse/natural polygons — `ITerrainClassifier` exists so that swap is local.
- **Whether the drop rates feel right.** The weights and quantities are reasoned from
  §4.1a's ratios, not playtested. All seeded data, so retuning needs no deploy.

### From Stage 04

Backend fully tested (41 new tests). The POI visit UI's logic is unit-tested via
`npm test`, but the `.tsx` changes have **never been rendered or typechecked** here.

Needs a working app toolchain:

- Typecheck and launch the updated `components/poiDetailModal.tsx` — the visit button,
  the decay preview, and the result box.
- Confirm an out-of-range POI shows "Move closer — Nm away" and the button is disabled.

Needs a human with a phone, since only a real POI shows it:

- **Visit the same POI several times** and confirm the yield visibly drops (100% → 67% →
  50%) and the modal explains why. This is §3.4's anti-degeneracy rule, and it is the
  thing most likely to feel wrong in practice rather than on paper.
- **Confirm the 30 m range grace is right.** `SkillTrainingService.RangeGraceMetres`
  widens the 50 m interact radius because the stored position is from the last sync. Too
  tight and a real visit fails while standing at the door; too loose and it is a
  spoofing vector.
- **Whether a garden or allotment actually appears as a Foraging POI** — the tag mapping
  changed, so previously-imported POIs still carry their old skill until re-imported.
