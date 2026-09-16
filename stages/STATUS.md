# Build status

**Current stage: `STAGE-02-progression-core.md`**

Single source of truth for where the build is. Update this when a stage completes.

| # | Stage | State |
|---|---|---|
| 01 | Prototype | DONE |
| 02 | Progression core | IN PROGRESS |
| 03 | Materials & inventory | NOT STARTED |
| 04 | Foraging | NOT STARTED |
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

Backend only; task 5 (app screens) is not yet built. Needs a working app toolchain:

- Skills screen wired to `GET /api/player/skills` — unlocked skills with progress, locked
  ones greyed with their unlock level.
- Upgrade screen wired to `GET /api/player/upgrades`,
  `POST /api/player/upgrades/{key}/purchase` and `POST /api/player/upgrades/respec`.
- Unlock celebration (full screen, not a toast — §3.1c). The sync response already carries
  `Unlocks` and `BonusPointsGranted` for this.
- HUD: show Adventurer level/XP and drop the hardcoded `hp={85}` / `gold={0}`.

Worth a human eye when the app lands, since only a real session shows it:

- **The tuning values are first-draft.** Milestone XP (1/20/50/100) and the upgrade cost
  curves are reasoned from DESIGN.md's ratios, not playtested. §3.1 says levels 1–3 should
  be reachable in the first session or two — that is the thing to check against a real walk,
  and it is a seeded-config change if it is wrong, not a code change.
