# Build status

**Current stage: `STAGE-02-progression-core.md`**

Single source of truth for where the build is. Update this when a stage completes.

| # | Stage | State |
|---|---|---|
| 01 | Prototype | DONE |
| 02 | Progression core | BLOCKED |
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

**Stage 02 is BLOCKED: `DESIGN.md` does not exist.**

It has never existed in this repo — no file on disk, and no commit in history deleted
one — yet the stage files reference it throughout. Stage 01 got through because the only
thing it needed from it (§3.2, the XP curve) was pinned exactly by the three known values
the task listed. Stage 02 needs actual game content that nothing else specifies: the
milestone XP grants (§3.0b), the unlock ladder above level 1 (§3.1), the idle XP rules
(§3.3), and the upgrade cost curves and effects (§3.0a).

Inventing those numbers would bake made-up game design into seed data and migrations, and
four of Stage 02's acceptance criteria assert behaviour against values only `DESIGN.md`
defines. See the blocker note in `STAGE-02-progression-core.md` for the specific list.

**To unblock:** add `DESIGN.md` at the repo root covering §3.0, §3.0a, §3.0b, §3.1, §3.1c
and §3.3 — or write the missing values into the stage file and drop the references.

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

Needs Docker (unavailable here, so the Testcontainers integration tests never ran):

- `dotnet test` with Docker running, to exercise the DB-backed paths — in particular
  that re-revealing a cell is a no-op at the unique-index level (criterion 8).
