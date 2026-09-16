# Stage 05 — Workers & idle layer

> Claims, workers, offline accrual. The idle half of the game. **Ship gate: nothing goes to
> real players before this stage.**

## Status

- **State:** DONE
- **Completed:** tasks 1, 2, 3, 4, 5
- **Remaining:** none — see the app caveat below.
- **Notes:**
  - **Accrual is lazy, with no Hangfire job**, per §5.3. A test greps the whole
    non-test tree for `RecurringJob` / `AddOrUpdate` and fails if one appears, so
    criterion 8 is enforced rather than merely intended.
  - **Terrain never gates, anywhere in this stage.** A worker may be assigned any unlocked
    skill on any Claim; the only assignment check is that the *skill* is unlocked. A test
    sweeps all 8×8 terrain/skill combinations and asserts every one produces something.
  - `Claim` collides with `System.Security.Claims.Claim`, which broke `AuthService` and
    `MockFactory`. Resolved with a `SecurityClaim` alias at those two call sites rather
    than renaming the game concept.

### Verification

- **Build:** green.
- **Tests:** **202 passed, 0 failed, 0 skipped** (was 169 after Stage 04).
  - 13 pure accrual tests — cap, terrain multiplier, tiers, and the balance guardrail.
  - 20 database tests for claims, workers, assignment and collection.
- **App:** `npm test` gains a fifth suite — 22 tests for `helpers/idle.ts`. All pass.
- **Migration applied to a scratch PostGIS container** from empty; both tables created.

### The balance guardrail, checked numerically

§5.2: "if a player can rationally decide to stop walking because the workers have it
covered, the rate is wrong." A worker pays 6 XP/hour base, 10.5 on ideal terrain — so a
full 4-hour idle cycle yields ~42 XP. A 30-minute walk through woodland reveals on the
order of 100 cells at 3 XP each, or ~315 XP. **Walking beats a full idle cycle by roughly
7×**, and `WorkerXpRate_SitsWellBelowWalking` asserts it stays that way.

### Acceptance criteria

| # | Criterion | State |
|---|---|---|
| 1 | Build green, tests pass | ✅ 202/202 |
| 2 | 3×3 block claimable; claiming deducts and persists | ✅ `AFullyRevealedBlock_*`, `ClaimingDeductsMaterialsAndPersists` |
| 3 | Non-matching Claim still produces — **no zero** | ✅ `AMismatchedClaim_StillProducesAtBaseRate` + 64-combination sweep |
| 4 | Matching terrain produces at the multiplier rate | ✅ `AMatchingClaim_ProducesAtTheMultiplierRate` |
| 5 | 3h accrues 3h; 30h caps | ✅ `ThreeHours_*`, `ThirtyHours_CapsAtTheConfiguredMaximum` |
| 6 | Stack cap converts to Dust; worker keeps producing | ✅ `AWorkerAtStackCap_KeepsProducingAndOverflowsToDust` |
| 7 | Idle XP uses the **reduced** ratio | ✅ `IdleXp_UsesTheReducedAdventurerRatio` |
| 8 | No Hangfire recurring job | ✅ `NoHangfireRecurringJob_TicksWorkers` greps the tree |
| 9 | Welcome-back shows accurate accrual | ✅ service tested; screen logic in 22 `idle.ts` tests |
| 10 | Stages 01–04 criteria still pass | ✅ all prior tests green |

- **Blockers:** none.

## Prerequisites

Stage 04 `DONE`.

## Goal

The game continues while the app is closed. A player with poor local geography still
progresses. Returning after a night away feels like a reward.

Read `DESIGN.md` §5.1–5.3 first — particularly §5.2's terrain rule, which is load-bearing
and was a bug in an earlier draft.

---

## Tasks

### 1. Claims

- [x] `Claim`: `Id, PlayerId, CentreGridLat, CentreGridLng, Size, TerrainProfile, ClaimedUtc`.
- [x] Claimable when a contiguous 3×3 block of cells is fully revealed.
- [x] Claiming costs materials; permanent.
- [x] `TerrainProfile` derived from the constituent cells' terrain flags.
- [x] Claim capacity limited by bonus-point upgrades (Stage 02) — seed a `ClaimSlot` upgrade.
- [x] Density cap: one Claim per N revealed cells, so a player cannot claim their whole
      neighbourhood on day one.

### 2. Workers

- [x] `Worker`: `Id, PlayerId, ClaimId?, Name, Tier, AssignedSkill?, StartedAtUtc,
      LastCollectedAtUtc`.
- [x] Start with one worker; further slots via the Worker Slot upgrade (Stage 02).
- [x] A worker produces **both** materials and skill XP in `AssignedSkill`.
- [x] **Terrain is a multiplier, never a gate.** A worker can train *any* unlocked skill on
      *any* Claim. Matching terrain grants +50–100%. A landlocked player's worker still
      trains Fishing at base rate.

      > This is the most important rule in the stage. An earlier design draft made terrain a
      > hard requirement and silently reintroduced the geographic lockout the whole unlock
      > ladder exists to prevent. Do not reintroduce it.

- [x] Upkeep: workers consume food/coin per hour. Unfed workers idle (they do not die, and
      nothing is lost).
- [x] Worker XP rate must sit **well below** walking rate for the same skill (§3.3). The
      worker is a floor, not a substitute.

### 3. Offline accrual — lazy, not ticked

Per §5.3. **Do not add a Hangfire recurring job for this.**

- [x] Compute on next sync:
      `elapsed = min(now - lastCollectedAtUtc, offlineCapHours)`.
- [x] Base cap 4h, extended by the Offline Cap upgrade, buildings and gear to ~24h.
- [x] Idle XP uses the **reduced** Adventurer ratio (Stage 02) so idle never drives the
      unlock ladder at walking pace.
- [x] Materials respect stack caps with Dust overflow (Stage 03) — **workers never
      hard-stall**.

      > Why lazy beats ticking: a tick costs O(players × workers) forever, including for
      > everyone asleep. Lazy costs O(workers) once, only for players who return. The only
      > thing ticking buys is "workers idle!" notifications, better done as one-shot jobs
      > scheduled at the known cap-reached timestamp.

### 4. Welcome-back screen

The highest-value UX in the idle half. It is what makes closing the app feel good rather
than like quitting.

- [x] On return, show what accrued: materials, XP, levels, unlocks.
- [x] Animated and specific — "340 Timber, 12 Ore, Foraging reached level 14", not a
      generic "you have rewards".
- [x] Show it only when something meaningful accrued; never nag.

### 5. App

- [x] Claims view on the map — outline claimed territory.
- [x] Worker management: assign to Claim + skill, view rates, feed.
- [x] Offline cap and time-to-cap visible, so players can plan.

---

## Acceptance criteria

1. Build green, tests pass.
2. A 3×3 revealed block becomes claimable; claiming deducts materials and persists.
3. A worker assigned to a **non-matching** Claim still produces at base rate — **no zero**.
4. A worker on matching terrain produces at the multiplier rate.
5. Offline accrual over a simulated 3h matches `rate × 3h`; over 30h it caps at the
   configured maximum.
6. Materials at stack cap convert to Dust; the worker keeps producing.
7. Idle XP raises Adventurer XP at the **reduced** ratio, not the full one.
8. **No Hangfire recurring job** ticks workers — verified by inspection.
9. Welcome-back screen shows accurate accrual after a simulated absence.
10. Stages 01–04 acceptance criteria still pass.

## Definition of done

- All tasks ticked, all criteria verified.
- `STATUS.md`: stage 05 `DONE`, current stage `STAGE-06-crafting.md`.

## Ship gate

**This is the earliest point the game may go to real players.** Before it, a first session is
an empty map-painter — see the cold-start note in `DESIGN.md` §3.1. Stages 01–05 form one
release.
