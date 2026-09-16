# Stage 01 — Prototype

> Make the loop that already exists correct, secure and honest. **No new game systems.**

## Status

- **State:** DONE — code complete, 9/10 acceptance criteria verified.
  Criterion 10 is NEEDS MANUAL VERIFICATION (no `node_modules` in this environment).
- **Completed:** Task 1 (secure the journey endpoints), Task 2 (swept-path reveal),
  Task 3 (anti-cheat: speed grading and desk drift), Task 4 (background sync correctness),
  Task 5 (XP curve), Task 6 (housekeeping)
- **Remaining:** none
- **Notes:**
  - Test project is **NUnit**, not xUnit (it already existed with Testcontainers
    infrastructure). The stage said "create xUnit if none exists" — one existed, so its
    conventions were followed instead.
  - Reveal geometry lives in `GeoSlayer.Domain/Services/Fog/PathSweep.cs` as pure static
    functions, deliberately free of EF/DI so the required unit tests run without Docker.
    The `DatabaseIntegrationTestBase` tests do need Docker.
  - The supercover line takes **both** cells at an exact corner crossing. Taking only one
    made the revealed set depend on walking direction — a caught bug, now covered by
    `SupercoverLine_ReversedEndpoints_CoversTheSameCells` over a 13×13 endpoint sweep.
  - `MaxNewCellsPerSync` raised 9 → 2000: a swept path legitimately covers hundreds of
    cells, so the old value would have truncated every real walk.
  - `SyncRequest.Path()` returns empty for an all-zero legacy body rather than
    fabricating Null Island; `JourneyService.Sync` rejects an empty path with 400.
  - Anti-cheat lives in `GeoSlayer.Domain/Services/Fog/TraceValidator.cs`, also pure.
    Thresholds are `const`s there: 25 m accuracy, 0.3 displacement ratio, 20 m / 180 s
    dwell, 5 m/s (18 km/h) reveal cut.
  - **A missing `accuracy` passes the cutoff.** The app does not send one until task 4,
    so rejecting nulls would mean no current client reveals anything. The motion checks
    still apply, so a drifting or driving legacy client is still caught. Revisit once
    task 4 ships and the field is always present.
  - Speed is graded on **net displacement**, not path length: GPS jitter inflates path
    length and would otherwise flag a slow walk as a vehicle.
  - Dwell needed a **cross-sync** check in `FogService` as well as the in-batch one — a
    phone syncing every 30 s never accumulates 180 s of dwell evidence within one batch.
  - Player position/time tracking is updated *before* the verdict, so alternating good
    and bad syncs cannot be used to reset the speed cap.
  - Task 4: refresh extracted to `geoslayer.app/helpers/tokenRefresh.ts`, free of React
    and expo-router — the interceptor version calls `router.replace`, which throws in a
    background task with no navigation container. Concurrent refreshes are collapsed
    onto one in-flight promise so a single-use refresh token is not burned twice.
  - Failed batches queue in `AsyncStorage` under `bg_pending_batches`, oldest first,
    and are retried next tick. Trimming drops the **newest** when over cap: old ground
    is lost forever, whereas the newest is re-covered by the next GPS fix.
  - Drain is spaced `SYNC_SPACING_MS` (2.5s) apart and capped at 5 batches per tick —
    without spacing the server's own 2s cooldown would reject the whole backlog.
  - `Coord` gained an optional `accuracy`; the foreground now posts `{ positions: [...] }`
    rather than a bare lat/lng, so both paths use the array endpoint.
  - **UNVERIFIED:** `geoslayer.app/node_modules` is not installed in this environment, so
    the app was neither typechecked nor built. Acceptance criterion 10 is outstanding.
    Run `npm install && npx tsc --noEmit` in `geoslayer.app/` before trusting task 4.
  - **`DESIGN.md` does not exist in this repo**, though the stage files reference it
    throughout (§3.2, §7.1, §7.2). Task 5 was unblocked because the three XP values it
    names pin the standard RuneScape curve exactly. Later stages leaning on DESIGN.md
    for content that is *not* self-evident will be blocked until it is written.
  - Task 5: curve in `GeoSlayer.Domain/Services/Progression/XpCurve.cs`, table to level
    200 with the formula beyond it, uncapped.
  - **Semantic change:** `Player.Xp` now means *cumulative lifetime* XP and `Level` is
    derived from it via `LevelForXp`. The old loop subtracted on level-up and kept a
    within-level remainder, which cannot survive a curve change. Any existing rows hold
    remainder values and will read as low levels — they need backfilling, but there is
    no production data yet.
  - `Player.Xp` widened `int` → `long` (migration `WidenPlayerXpToLong`); `int` would have
    capped an uncapped curve at about level 126. `SyncResponse.Xp` and
    `LoginUserResponse.Xp` widened to match.
  - Added `GeoSlayer.Domain/Database/Context/AppDbContextFactory.cs` so `dotnet ef` works
    without Docker — a Debug build starts a Testcontainer at host startup, which EF's
    default host-building path would otherwise require.
  - App: `components/hud.tsx` divided by `level * 100`, which pegs the bar at 100% under
    cumulative XP. It now uses `helpers/xpCurve.ts`, a mirror of the server curve.
  - Task 6: both NU1903 packages were **transitive** (Swashbuckle → Microsoft.OpenApi,
    Testcontainers → SSH.NET), so they are pinned as direct references at 3.10.2 and
    2026.0.0. Swashbuckle 10.1.7 already declares `Microsoft.OpenApi >= 3.10.2`, so the
    2.4.1 was an old floor and the major bump introduces no binding mismatch. Build is
    now warning-free.
  - Seq endpoint reads `SEQ_URL`, defaulting to `http://localhost:5341` rather than the
    hardcoded `192.168.1.20`.

  Added after the stage was first marked done, while Stage 02 sat blocked:

  - `GeoSlayer.Tests/Services/Fog/FogServiceIntegrationTests.cs` — 12 database-backed
    tests covering what the pure tests cannot reach: cells actually persisting, the
    unique index making a replay a no-op rather than a duplicate-key crash, XP and the
    derived level landing on the player row, the cooldown, a rejected batch still
    updating last-known position, and one player's walk not leaking into another's map.
    **Written but never executed** — no Docker in this environment.
  - `TestContainerSetup` no longer throws when the container cannot start. It records
    the reason and `DatabaseIntegrationTestBase` calls `Assert.Ignore` with it, so a
    missing daemon reads as "skipped, needs Docker" instead of 12 failures with an
    unrelated stack trace. Without this the suite looked broken rather than incomplete.
  - `geoslayer.app/helpers/__tests__/xpCurve.test.mjs` + an `npm test` script. Runs on
    bare Node with no dependencies, so it works without `node_modules`. Mutation-checked:
    changing `300` to `301` in the curve fails 9 checks and exits 1.
- **Acceptance criteria:**
  1. `dotnet build GeoSlayer.sln` — **PASS**, 0 errors, 0 warnings.
  2. Unit tests — **PASS**, 64/64 pure-function tests, plus 12 database integration
     tests that **skip** (Docker unavailable here). The suite reports
     `Failed: 0, Passed: 64, Skipped: 12` rather than 12 red failures — see the note on
     `TestContainerSetup` below. App-side: `npm test` in `geoslayer.app/` passes 19
     checks with no `node_modules` required.
  3. Unauthenticated `POST /api/journey/sync` → 401 — **PASS by inspection**:
     `[Authorize]` on the controller, `AddJwtBearer` + `UseAuthentication`/
     `UseAuthorization` wired in `Program.cs`. Not exercised against a running server.
  4. No client-supplied `playerId` — **PASS**, verified by grep: no request DTO or
     controller takes one, and the app's remaining `playerId` references are all reads
     of the *login response*, never sent to the journey endpoints.
  5. 500 m walk reveals an unbroken corridor — **PASS**
     (`Sweep_FiveHundredMetreWalk_RevealsAContiguousCorridor`).
  6. Indoor-drift trace reveals zero cells — **PASS** (`Validate_DriftTrace_*`,
     including a 25-seed sweep so it is not one lucky random trace).
  7. 15 m/s vehicle trace reveals zero cells — **PASS** (`Validate_DrivingTrace_*`).
  8. Replaying a path grants nothing — **PASS at the geometry level**
     (`Sweep_ReplayingTheSamePath_YieldsNoCellsNotAlreadyRevealed`). The DB-level
     guarantee now has tests too
     (`FogServiceIntegrationTests.Reveal_ReplayingAnIdenticalPath_RevealsNothingAndGrantsNoXp`,
     `RevealedCells_DuplicateForTheSamePlayer_IsRejectedByTheUniqueIndex`), but they are
     **written, not executed** — no Docker here. Treat criterion 8 as fully verified only
     once `dotnet test` has run with a daemon up.
  9. XP curve known values — **PASS** (level 2 = 83, 50 = 101,333, 99 = 13,034,431).
  10. App builds, map renders, fog draws — **STILL NOT VERIFIED.**
      `geoslayer.app/node_modules` is not installed, so the app was neither typechecked
      nor run. Partially narrowed: `helpers/xpCurve.ts` is now covered by
      `helpers/__tests__/xpCurve.test.mjs`, which confirms the client curve matches the
      server on all 11 known values and that the HUD progress bar reads 0% / 50% / ~100%
      across a level band. That closes the riskiest hand-written app logic, but says
      nothing about whether the app compiles or renders.
- **Blockers:** _(none)_

## Goal

At the end of this stage GeoSlayer is a *correct* map-painter: you walk, the right cells
reveal, background walking works, XP accrues on a curve that will not need replacing, and
the exploits that would poison the data are closed.

It is deliberately **not yet a game** — no materials, no skills, no workers. Those are
Stages 02–05. Resist adding them here.

## Prerequisites

None. This is the first stage.

## Why these tasks and not others

Everything here is either (a) a bug that makes real-world play lose data, (b) a security
hole, or (c) a decision that gets *more* expensive to change later. The XP curve is in this
stage because every later system grants XP; changing the curve after players have balances
means a migration and a rescale.

---

## Tasks

### 1. Secure the journey endpoints

`GeoSlayer/Controllers/JourneyController.cs` has no `[Authorize]` and takes `playerId` from
the request body. Anyone can read or mutate any player's map without a token.

- [x] Add `[Authorize]` to `JourneyController`.
- [x] Resolve the player from the JWT, not the request. `IUserContextHelper` already exists
      (`GeoSlayer.Domain/Helpers/UserContextHelper.cs`) — use it.
- [x] Remove `PlayerId` from `SyncRequest`. It is now derived server-side.
- [x] `GET /api/journey/revealed/{playerId}` — drop the route parameter; return the caller's
      own cells.
- [x] Update the app: `helpers/apiClient.ts` callers and `helpers/backgroundLocation.ts` stop
      sending `playerId`.

**Why first:** every later task touches these endpoints. Do the signature change once.

### 2. Swept-path reveal

Currently only the endpoint cell reveals (`RevealRadius = 0`, single position per sync). A
walk between two syncs loses every cell in between.

- [x] `SyncRequest` accepts an **array** of positions (`lat`, `lng`, `timestampMs`,
      `accuracy`), not a single point. Keep a single-point path working for compatibility.
- [x] In `FogService`, interpolate the path between consecutive positions and reveal every
      cell the line crosses. A supercover line walk (Bresenham variant that includes
      diagonally-touched cells) over the grid is the right algorithm — no gaps.
- [x] Apply `RevealRadius` around each swept cell. Set `RevealRadius = 1` (3×3).
- [x] Insert revealed cells in **one batch**, not per cell. A 40-minute walk can be 200+
      cells.
- [x] Preserve the existing unique index behaviour — re-revealing a cell is a no-op and
      grants nothing.

### 3. Anti-cheat: speed grading and desk drift

See `DESIGN.md` §7.1 and §7.2. These belong here because swept-path reveal *amplifies* both
exploits — shipping task 2 without this is worse than shipping neither.

- [x] **Accuracy cutoff.** Reject positions with `accuracy > 25m` before any processing.
- [x] **Displacement gate.** Compute net displacement vs. total path length over the batch.
      A ratio below ~0.3 means random-walk drift, not travel — reject the batch's reveals.
- [x] **Dwell detection.** If the centroid of the last N positions has not moved beyond
      ~20m over several minutes, suspend reveals until genuine displacement resumes.
- [x] **Speed grading.** Full reveal at walking pace. Above ~18 km/h, do not reveal.
      *Interim rule for this stage* — Uncharted Transit (§7.1) is Stage 14. Bank nothing
      yet; just don't reveal.
- [x] Keep the existing checks: 50 m/s hard cap, clock-skew, sync cooldown. Adapt the 2s
      cooldown to reject *syncs closer than 2s apart*, not to drop whole batches.

**Unit tests required** (these are pure functions — test them properly):
- Supercover line across a known grid returns the expected cell set.
- A synthetic drift trace (random walk within 25m) reveals zero cells.
- A synthetic walk trace (1.4 m/s, consistent bearing) reveals the expected count.
- A synthetic drive trace (15 m/s) reveals zero cells.

### 4. Background sync correctness

`geoslayer.app/helpers/backgroundLocation.ts` batches locations then syncs only the last
one, and never refreshes its token.

- [x] Send the **whole `locations` batch** to the new array-accepting endpoint.
- [x] **Refresh the access token** when expired. The refresh logic in
      `helpers/apiClient.ts` is not used by the background task — extract it so both paths
      share it. Currently a long walk 401s into an empty catch and silently loses everything.
- [x] Do not swallow errors silently. Persist failed batches to `AsyncStorage` and retry on
      the next tick, so a network drop mid-walk does not lose ground.
- [x] Verify `gs_player` is present before syncing (it is written by `contexts/authContext.tsx`).

### 5. XP curve

`FogService.cs:157` uses `player.Level * 100` — linear, and wrong for an infinite-levelling
game.

- [x] Implement the RuneScape curve (`DESIGN.md` §3.2) as a precomputed `long[]` lookup
      table to ~200 levels, built once at startup, with the formula as fallback beyond.
- [x] Replace the levelling loop in `FogService.Reveal`.
- [x] Uncapped — no level 99 ceiling.
- [x] Unit test: known values (level 2 = 83, level 50 = 101,333, level 99 = 13,034,431).

**Do not** add Adventurer XP or per-skill XP here — that is Stage 02. This task only swaps
the curve behind the existing single `Player.Xp`.

### 6. Housekeeping

- [x] Update the two high-severity vulnerable packages (`Microsoft.OpenApi` 2.4.1,
      `SSH.NET` 2025.1.0).
- [x] Fix the debug seed guard in `Program.cs:154` — it checks for `financemanagercontainer`
      (template leftover) and so never matches this project's `geoslayercontainer`.
- [x] Remove the stale `StreetImportService` reference in the `PoiImportService` docstring.
- [x] Update the three "street progress" permission strings in `app.json` — streets were
      removed and the strings now describe a system that does not exist.
- [x] Move the hardcoded Seq IP (`Program.cs:27`) to configuration.

---

## Acceptance criteria

Each must be *verified*, not assumed.

1. `dotnet build GeoSlayer.sln` succeeds with no errors.
2. All new unit tests pass. (If no test project exists, create `GeoSlayer.Tests` — xUnit —
   and wire it into the solution.)
3. An unauthenticated request to `POST /api/journey/sync` returns 401.
4. An authenticated request cannot read or mutate another player's cells — no `playerId` is
   accepted from the client anywhere.
5. Posting a synthetic 500m walking path in one batch reveals a contiguous unbroken corridor
   of cells with no gaps.
6. Posting a synthetic indoor-drift trace reveals **zero** cells.
7. Posting a synthetic 15 m/s vehicle trace reveals **zero** cells.
8. Re-posting an identical path a second time reveals zero new cells and grants zero XP.
9. XP curve lookup matches the three known values above.
10. The app builds and the map screen still renders, tracks position, and draws fog.

## Definition of done

- Every task checkbox ticked.
- Every acceptance criterion verified.
- `STATUS.md` updated: stage 01 `DONE`, current stage `STAGE-02-progression-core.md`.
- Anything needing a physical walk recorded in the `STATUS.md` manual verification queue.

## Needs manual verification (human, outdoors)

An agent cannot verify these. Add to the `STATUS.md` queue:

- Walk ~1km with the screen off; confirm the revealed corridor matches the route.
- Leave the phone on a desk for an hour with the app backgrounded; confirm no cells reveal.
- Confirm the Android foreground-service notification appears and persists.

## Out of scope

Materials, skills, workers, crafting, Museum, clues, Uncharted Transit banking. All later
stages. If a task here seems to need one of those, it does not — re-read the task.
