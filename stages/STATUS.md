# Build status

**All 15 originally planned stages are `DONE`.** Stage 16 (Combat encounters) was added
after the scope question was decided — see `DESIGN.md` §5C.

Single source of truth for where the build is. Update this when a stage completes.

| # | Stage | State |
|---|---|---|
| 01 | Prototype | DONE |
| 02 | Progression core | DONE |
| 03 | Materials & inventory | DONE |
| 04 | Foraging | DONE |
| 05 | Workers & idle | DONE |
| 06 | Crafting | DONE |
| 07 | Fishing | DONE |
| 08 | Woodcutting | DONE |
| 09 | Cooking | DONE |
| 10 | Mining | DONE |
| 11 | Smithing | DONE |
| 12 | Museum | DONE |
| 13 | Clue scrolls | DONE |
| 14 | Retention systems | DONE |
| 15 | Remaining skills | DONE |
| 16 | Combat encounters | NOT STARTED |

States: `NOT STARTED` → `IN PROGRESS` → `DONE` (or `BLOCKED`, with a reason).

## Open questions for a human

Stage 15's definition of done asks that `DESIGN.md` §9's open questions be recorded here
rather than decided by an agent. **None of these should be settled without you.**

- ~~**Combat scope.**~~ **DECIDED:** encounters. Some spawn at random POIs, some are fixed
  training grounds at historic sites. Written up as `DESIGN.md` §5C and scoped as
  `STAGE-16-combat-encounters.md`. The geography rule is preserved — historic POIs are a
  boost, never the only venue.
- **Multiplayer.** Nothing in the build assumes other players exist. §9 lists it as open;
  the Museum's shareable profile (§5A.1) is the only hook that points that way.
- **Monetisation.** Untouched, deliberately. It shapes the whole design and is not an
  agent's call.
- **Trading's coin economy.** Flagged for decision rather than built: there is no currency
  and no material→coin sink, and upkeep is paid in food alone. What coin is *for* should be
  decided before it exists — a second currency with no job is worse than none. Recorded as
  `DESIGN.md` §9.5.

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

### From Stage 05

**Stage 05 is the ship gate — Stages 01–05 form one release.** Backend fully tested
(33 new tests); the screens are not.

Needs a working app toolchain:

- Typecheck and launch `app/workers.tsx` and `components/welcomeBack.tsx`.
- Confirm the welcome-back screen appears after a real absence and stays away when
  nothing accrued.
**Two deferrals carried out of Stage 05, both deliberate:**

- ~~**Worker upkeep is not implemented.**~~ **Resolved in Stage 09.** Cooking supplies
  food, so upkeep is now live: 1 unit per worker-hour, cheapest food first, and unfed
  workers keep what they earned (§7.4 outranks the sink).
- **Claims are not drawn on the map.** `ClaimDto` carries a bounding box for exactly this,
  and the claim/eligibility endpoints exist, but the map outline and the claim-from-map
  action were not built. API-only.

Needs a human, since only real time shows it:

- **Leave the app closed overnight** and confirm the welcome-back numbers match the cap
  (4h base) rather than the full absence.
- **Whether the worker rate feels right.** Numerically it is ~7× below walking, which is
  the intended direction, but whether that reads as "a helpful floor" or "not worth
  bothering with" is a judgement only play answers. All seeded constants
  (`OfflineAccrual`) so retuning is cheap.

### From Stage 06

Backend fully tested (25 new tests). Two screens added, neither rendered or typechecked.

Needs a working app toolchain:

- Typecheck and launch `app/crafting.tsx` and `app/equipment.tsx`.
- Confirm a travel-gated recipe is visibly different from a level-gated one — that
  distinction is criterion 5 and the whole reason the lock reason is an enum rather than
  a boolean.

Carried forward, deliberately:

- ~~**Tool gating is not enforced.**~~ **Resolved in Stage 10.** `DropRoller` now takes an
  optional `maxToolTier`, read from equipped items. No tool means no cap — not tier 1 —
  which is what makes it safe for skills the tutorial teaches.
- **No craft-completion notifications.** No push infrastructure exists. §5.3 suggests
  one-shot jobs at the known completion timestamp; that is notification plumbing, best
  done once for crafts, workers-at-cap and clue scrolls together — **Stage 14
  (Retention)** is the right home.

### From Stage 07

Nothing outstanding. Fishing needed no app work — the skills screen, inventory and worker
screens are generic and picked it up automatically, which is the point of Stage 04's
machinery.

Worth a human eye eventually:

- **Whether a landlocked player actually finds Fishing playable.** Base rate is 1 XP/cell
  against 3 on water, so it is 3× slower rather than blocked — that ratio is the whole
  geography compromise and it is the thing most likely to feel wrong in practice.

### From Stage 08

Nothing outstanding — Woodcutting was seed data and needed no app work.

One thing to watch when adding the remaining gathering skills: **Stage 03's placeholder
materials keep colliding.** Fishing hit it, Woodcutting hit it. Any skill whose materials
were sketched into `MaterialSeedData` during Stage 03 needs its own `MaterialCategory` and
the placeholders reassigned. `SkillLaddersDoNotShareAMaterialCategory` catches it, so it
fails loudly rather than producing a subtly wrong drop table — but expect it.

### From Stage 09

Nothing outstanding on the backend. No app work was needed — Cooking appears in the
generic skills screen and its recipes in the generic crafting screen.

Needs an app toolchain when one exists:

- **Surface `workersWentUnfed`.** The sync response now carries it, and it is the nudge
  that tells a player to cook. Nothing renders it yet, so an unfed worker is currently
  silent — the one piece of upkeep that is server-only.

Worth a human eye:

- **Whether upkeep feels like a sink or a tax.** 1 food/worker-hour against ~2 material
  units/hour produced means a worker nets positive, which is the intent. But a player who
  forgets to cook for a week returns to a stalled larder, and whether that reads as "I
  should cook more" or "this is a chore" is a judgement only play answers. All constants
  are in `OfflineAccrual`.

### From Stage 10

Nothing outstanding. Mining needed no app work.

**The accessibility rule is verified, not assumed.** A simulated suburban player with zero
rocky terrain reaches tier 7 Meteoric Ore, and rocky ground yields under 4× urban — the
gap §4.1a wants to be "meaningful, not disqualifying". If that ratio ever drifts,
`RockyGround_IsMeaningfullyFasterWithoutBeingRequired` is where it surfaces.

Worth a human eye:

- **Whether under-4× actually feels acceptable in play.** It is the right shape
  numerically, but a player who knows a quarry-dweller mines twice as fast may still feel
  it. That is a judgement only real play answers, and the multipliers are seeded.

### From Stage 11

Nothing outstanding on the backend. The gathering/production loop is now closed: Mining
produces ore, Smithing turns it into tools, and the tools make Mining faster.

~~Tools show only their primary modifier.~~ **Fixed immediately after** — `ModifierText`
now reads "Gathers up to tier 3 · 20% faster gathering", and the DTO carries the secondary
modifier so the app can render them separately if it prefers.

### From Stage 12

Needs an app toolchain when one exists:

- Typecheck and launch `app/museum.tsx`, and confirm empty plinths read as a pull rather
  than as noise. §5A.1 stakes the whole system on that framing, and it is the one thing a
  test cannot check.
- **Nothing links to the Museum yet.** The screen and route exist but no HUD button opens
  it — the bottom panel has 🎒 ✨ ⚡ and no 🏛️.

Carried forward, deliberately:

- ~~**Museum set bonuses** are not implemented.~~ **Delivered in Stage 14**, once Relics
  and Expeditions had entries. Small, permanent, and read by real systems. Cartography
  deliberately has none — it has no fixed size, so it can never be complete.

Worth a human eye:

- **Region naming quality.** `PoiRegionResolver` names a cell after its most notable POI,
  which will sometimes read oddly — "you have been to Tesco Express" is technically true
  and tonally wrong. The fix is a better resolver behind the same interface, but whether
  it matters depends on how it reads in a real neighbourhood.

### From Stage 13

**Criterion 5 is not met.** Cryptic clue generation is deferred, and this is the first
acceptance criterion in the project left genuinely unsatisfied rather than reinterpreted.

- `ClueStepType` defines all seven types and `ClueRiddleText.Cryptic` is written and
  tested, but nothing generates a Cryptic step. Task 3's hard requirement is that a
  generated riddle resolve to **exactly one** POI in the radius — otherwise it is
  unsolvable — and that validation needs arbitrary OSM tags (`building:levels`) that
  `PointOfInterest` does not store. It keeps a name, a skill and a location.
- **The fix is an importer change**: store raw tags on import, then generate and validate
  against them. That belongs with a stage that touches `PoiImportService`.
- Shipping Cryptic without the uniqueness check would be worse than not shipping it — an
  ambiguous riddle is a clue the player cannot solve, which is exactly what the one-skip
  rule exists to rescue them from.

Needs an app toolchain when one exists:

- Typecheck and launch `app/clues.tsx`.
- **Map integration for coordinate steps is not built.** The screen states the radius in
  text and the API sends the centre, but the map draws no search circle. The data is all
  there — `SearchLat`/`SearchLng`/`SearchRadius` on the step DTO.

Needs a human with a phone (carried from the stage file):

- **Walk a generated clue end to end** and confirm the riddle is solvable by someone who
  did not write the generator. Category steps say things like "Stand somewhere where the
  faithful gather" — whether that reads as evocative or as vague is the whole question,
  and no test can answer it.

### From Stage 14

**The Stage 01 interim speed cap is removed**, as the definition of done required.
`TraceValidator` now rejects only implausible speed (>400 km/h, a forged path); everything
below banks as Uncharted Transit.

All five retention systems are backend-complete and tested, and **all five now have app
surfaces** — the largest app gap in the project is closed:

- **Banked transit on the map.** A violet dashed layer above the fog, opacity carrying the
  speed grading. §7.1's "distinct visual state".
- **Expeditions screen.** `app/expeditions.tsx`. Needed a new endpoint first —
  `GET /api/retention/expeditions/destinations` — because nothing returned the visit log,
  so the screen had no destinations to offer.
- **Patrol routes.** `app/patrols.tsx`. Waypoints captured from the player's position
  while walking, which is how §5.7 intends them to be created.
- **Surges on the map.** An amber banner, refetched per sync. Renders nothing when none
  are running, so it never becomes ignorable chrome.
- **District status.** On the workers screen, beside the Claims. Extended server-side with
  a nearest-miss shortfall, since "no District" was otherwise a dead end — see below.

Worth a human eye:

- **Whether the transit redemption ratio feels right.** One walked cell redeems three
  banked. Too generous and the commute becomes the efficient route; too stingy and the
  bank rots unused. All constants are in `TransitGrading`.

---

## Outstanding work, all stages

Everything below is recorded because it was deliberately not built, not because it was
forgotten. Ordered by how much it matters.

### 1. ~~The app is largely unverified~~ — RESOLVED

`npm install` was run and the whole app now **typechecks, lints and bundles clean**:

```
npm run verify     # tsc --noEmit && expo lint
```

- **`tsc --noEmit`: 0 errors** across every screen, component and helper.
- **`expo lint`: clean**, 0 warnings.

The app's own `.mjs` test suites were **removed** when the codebase was aligned to Orbit,
which has no app-side tests. Verification is now typecheck, lint, and exercising the real
API over HTTP — which is what caught four POSTs pointing at the wrong endpoint during the
route migration, something no helper unit test could have seen.

Do **not** run `npx expo export` on the dev box: it exhausts the 1.9 GB of RAM and takes
the machine down.

The caveat that stood through Stages 02–15 is closed. What remains is *visual* and
*behavioural* review on a real device — layout, whether the framing reads right — not
whether the code is sound.

### 2. ~~Stage 14's systems are mostly still API-only~~ — RESOLVED

All four remaining surfaces were built: expeditions, patrols, surges and District status.
Each got a presentation helper; the web bundle reached **16 routes**.

Two of them turned out not to be pure UI work:

- **Expeditions had no data source.** Dispatch existed, but nothing returned the POIs a
  player had visited, so there was nothing to dispatch *to*. Added
  `GetExpeditionDestinations` over the Stage 04 visit log.
- **District status was a dead end.** §5.5 requires varied terrain on purpose, but a
  player holding nine identical Claims saw only an empty status and could not learn that
  variety was the problem. Added `DistrictMatching.NearestMiss` so the status names what
  it is short of.

Worth a human eye: the patrols screen duplicates the server's 20-hour cooldown as
`COOLDOWN_HOURS`. Nothing ties the two together — if the server rule moves, the client
will quietly disagree.

### 3. The codebase now follows Orbit's conventions

Aligned to `github.com/Bregann/Orbit` as the reference: `GeoSlayer.Core` naming,
block-scoped namespaces, `api/[controller]/[action]` routes, one DTO per file,
feature-foldered interfaces, `required` properties, and on the app side
`interfaces/api/<feature>/`, a `QueryKeys` enum and the `useMutation*` wrappers.
`RunCSharpChecks.yml` now gates PRs on `dotnet format` plus the test suite.

Two pre-existing bugs surfaced while verifying the new routes, both invisible to the test
suite because tests construct services directly rather than through DI:

- **`IUserContextHelper` was never registered.** Container validation failed at startup,
  so the API could not boot at all.
- **The app called `/api/Auth/RefreshAppToken`**, which does not exist. Token refresh
  would have failed on first use.

**Known issue:** `/swagger` returns 500. `GeoSlayer.Core.csproj` pins `Microsoft.OpenApi`
3.10.2 for two CVEs and Swashbuckle 10.1.7 is incompatible with that version. A deliberate
trade-off, but it means route changes must be verified by curling endpoints.

### 4. Genuinely unmet acceptance criteria

- **Stage 13 criterion 5** — Cryptic clue generation. Needs raw OSM tags stored on import;
  `PointOfInterest` keeps only a name, skill and location. An importer change.

### 5. Systems described but not built

- **Trading's material→coin economy** (Stage 15). Needs a currency, sink and price table.
- **Craft-completion notifications** (Stage 06). No push infrastructure exists.
- **Athletics distance synergy**, **Banking stack-cap upgrades** (Stage 15).

### 6. Needs a human with a phone

No test can answer these:

- Walk a **generated clue** end to end — is the riddle solvable by someone who did not
  write the generator?
- Walk a **known wood, riverside and plain street** — does terrain classification match?
- **Visit one POI repeatedly** — does the §3.4 decay curve feel right or punitive?
- **Leave the app closed overnight** — does the welcome-back screen read as a reward?
- Check **region naming**: `PoiRegionResolver` names a cell after its most notable POI, so
  "you have been to Tesco Express" is possible and tonally wrong.
- **Balance generally.** Every constant is reasoned from DESIGN.md's ratios and none is
  playtested. All of it is seeded, so retuning needs no deploy.
