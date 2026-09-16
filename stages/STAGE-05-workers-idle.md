# Stage 05 — Workers & idle layer

> Claims, workers, offline accrual. The idle half of the game. **Ship gate: nothing goes to
> real players before this stage.**

## Status

- **State:** NOT STARTED
- **Completed:** _(none)_
- **Remaining:** all tasks
- **Notes:** _(none)_
- **Blockers:** _(none)_

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

- [ ] `Claim`: `Id, PlayerId, CentreGridLat, CentreGridLng, Size, TerrainProfile, ClaimedUtc`.
- [ ] Claimable when a contiguous 3×3 block of cells is fully revealed.
- [ ] Claiming costs materials; permanent.
- [ ] `TerrainProfile` derived from the constituent cells' terrain flags.
- [ ] Claim capacity limited by bonus-point upgrades (Stage 02) — seed a `ClaimSlot` upgrade.
- [ ] Density cap: one Claim per N revealed cells, so a player cannot claim their whole
      neighbourhood on day one.

### 2. Workers

- [ ] `Worker`: `Id, PlayerId, ClaimId?, Name, Tier, AssignedSkill?, StartedAtUtc,
      LastCollectedAtUtc`.
- [ ] Start with one worker; further slots via the Worker Slot upgrade (Stage 02).
- [ ] A worker produces **both** materials and skill XP in `AssignedSkill`.
- [ ] **Terrain is a multiplier, never a gate.** A worker can train *any* unlocked skill on
      *any* Claim. Matching terrain grants +50–100%. A landlocked player's worker still
      trains Fishing at base rate.

      > This is the most important rule in the stage. An earlier design draft made terrain a
      > hard requirement and silently reintroduced the geographic lockout the whole unlock
      > ladder exists to prevent. Do not reintroduce it.

- [ ] Upkeep: workers consume food/coin per hour. Unfed workers idle (they do not die, and
      nothing is lost).
- [ ] Worker XP rate must sit **well below** walking rate for the same skill (§3.3). The
      worker is a floor, not a substitute.

### 3. Offline accrual — lazy, not ticked

Per §5.3. **Do not add a Hangfire recurring job for this.**

- [ ] Compute on next sync:
      `elapsed = min(now - lastCollectedAtUtc, offlineCapHours)`.
- [ ] Base cap 4h, extended by the Offline Cap upgrade, buildings and gear to ~24h.
- [ ] Idle XP uses the **reduced** Adventurer ratio (Stage 02) so idle never drives the
      unlock ladder at walking pace.
- [ ] Materials respect stack caps with Dust overflow (Stage 03) — **workers never
      hard-stall**.

      > Why lazy beats ticking: a tick costs O(players × workers) forever, including for
      > everyone asleep. Lazy costs O(workers) once, only for players who return. The only
      > thing ticking buys is "workers idle!" notifications, better done as one-shot jobs
      > scheduled at the known cap-reached timestamp.

### 4. Welcome-back screen

The highest-value UX in the idle half. It is what makes closing the app feel good rather
than like quitting.

- [ ] On return, show what accrued: materials, XP, levels, unlocks.
- [ ] Animated and specific — "340 Timber, 12 Ore, Foraging reached level 14", not a
      generic "you have rewards".
- [ ] Show it only when something meaningful accrued; never nag.

### 5. App

- [ ] Claims view on the map — outline claimed territory.
- [ ] Worker management: assign to Claim + skill, view rates, feed.
- [ ] Offline cap and time-to-cap visible, so players can plan.

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
