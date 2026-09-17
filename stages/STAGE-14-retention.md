# Stage 14 — Retention systems

> Expeditions, Patrol Routes, Uncharted Transit, Districts, Surges. What makes the game
> survive contact with month two.

## Status

- **State:** DONE (backend); app screens outstanding
- **Completed:** tasks 1, 2, 3, 4, 5 — all five systems
- **Remaining:** app surfaces, recorded in STATUS.md.

### The Stage 01 speed cap is gone

The definition of done asks for it, and §7.1 is emphatic about why. The hard cap
"punishes cyclists, who are a legitimate audience moving under their own power, and gives
a bus commuter *nothing* for genuinely passing through new territory".

`TraceValidator` no longer rejects a fast batch. It now rejects only **implausible** speed
(>111 m/s, 400 km/h) — that is a forged path, not a commute. Everything below banks as
Uncharted Transit, graded by speed:

| Pace | Reveals | Banks |
|---|---|---|
| ≤2 m/s (walking) | all | nothing |
| 2–7.5 m/s (cycling) | tapers linearly | the remainder |
| >7.5 m/s | nothing | all |

The taper is linear rather than stepped, so a cyclist slowing for a hill is not punished
by a cliff edge — which is how §7.1 handles cyclists "without a special case".

**Three Stage 01 tests asserted the old rule and were rewritten**, not deleted: they now
assert that a driving trace is accepted-but-banked, that a cyclist reveals partially, and
that implausible speed is still rejected.

### Expeditions turn old trips into assets

Dispatch requires a **real prior visit** in `PlayerPoiVisit` — the Stage 04 log, which is
what made this "nearly free to implement". Duration scales with distance and is capped at
48h, so a distant POI is a long trip rather than an abandoned worker.

This is also a second angle on the rural/urban imbalance: a rural player who visits a city
**once** gains lasting access to urban materials.

### Patrols keep novelty and routine separate

§5.7's split is the whole design: **novelty stays the only route to progress; routine
becomes the route to maintenance.** A completed circuit pays worker upkeep — food, which
Stage 09 made a real constraint — and grants **no XP**, asserted directly. One completion
per day, so the reward is maintenance rather than a grind target.

Waypoints must be hit **in order**. Accepting them in any order would let a player who
lives inside the loop claim it without walking it.

### Districts make the map matter

A uniform holding grants nothing; a varied one grants a bonus. Without that, §5.5 notes,
"the optimal play is nine identical cells of your best terrain". Contiguity is required
too — a scattered holding is not a District.

### Museum set bonuses, delivered here

Deferred from Stage 12 because Relics and Expeditions were empty, so any bonus would have
been balanced against an unfinishable Museum. Both wings now have entries, so the bonuses
ship: small, permanent, and **read by real systems** rather than merely displayed.

Cartography deliberately has **no** bonus — regions are created on discovery, so the wing
has no fixed size and can never be "complete". Promising a bonus for finishing it would
be a lie.

### Verification

- **Build:** green.
- **Tests:** **418 passed, 0 failed, 0 skipped** (was 379 after Stage 13).
  - 33 retention integration tests across all five systems.
  - 6 Museum set-bonus tests.
  - 3 rewritten Stage 01 speed tests.
- **Migration** applied to a scratch PostGIS container; 74 tables.

### Acceptance criteria

| # | Criterion | State |
|---|---|---|
| 1 | Build green, tests pass | ✅ 418/418 |
| 2 | Dispatch only to POIs with a real prior visit | ✅ `AWorkerCannotBeSent_ToAPoiNeverVisited` |
| 3 | Duration scales with distance; returns materials | ✅ `ExpeditionDuration_ScalesWithDistance`, `AReturnedExpedition_YieldsThePoisMaterials` |
| 4 | Circuit completes only in order | ✅ `ACircuitDoesNotComplete_WhenWaypointsAreOutOfOrder` |
| 5 | Patrol awards upkeep and **no** cell XP | ✅ `PatrolCompletion_AwardsUpkeepAndNoXp` |
| 6 | 15 m/s banks and reveals nothing | ✅ `MovementAtFifteenMetresPerSecond_BanksAndRevealsNothing` |
| 7 | Walking redeems at the ratio | ✅ `WalkingNearBankedTransit_RedeemsIt`, `RedemptionHonoursTheConfiguredRatio` |
| 8 | Transit decays after the window | ✅ `BankedTransit_ExpiresAfterTheWindow` |
| 9 | Varied District grants; uniform does not | ✅ `AVariedDistrict_GrantsItsBonus`, `AUniformDistrict_GrantsNothing` |
| 10 | Surge boosts and expires on schedule | ✅ 5 surge tests |
| 11 | Earlier stages still pass | ✅ all prior tests green |

- **Blockers:** none.

## Prerequisites

Stage 13 `DONE`. Read `DESIGN.md` §5.4–5.7 and §7.1.

## Goal

Close the long-run failure modes: travel becoming worthless, routine walks becoming
unrewarding, commutes being wasted, and the world going stale.

Each task below is independent — they can be done in any order, or split across loops.
**Expeditions first** if you want the highest value per unit of work.

---

## Tasks

### 1. Worker Expeditions (§5.4) — highest value

Solves: POI value currently expires. A cathedral you visited on holiday is dead to you
forever, which is a shame given it is the most memorable thing in your game.

- [x] Dispatch a worker to **any POI the player has personally visited at least once**. The
      `PlayerPoiVisit` log from Stage 04 already has this — nearly free to implement.
- [x] Duration scales with real-world distance. Distant POIs are high-value, low-frequency.
- [x] Returns that POI's exclusive materials and skill XP.
- [x] Occupies the worker, competing with Claim work — a real decision.
- [x] Populate the Museum `Expeditions` wing with trophies.

This turns every trip the player has ever taken into a permanent asset, and softens the
rural/urban imbalance (§7.3) from a new angle: a rural player who visits a city once gains
lasting access to urban materials.

### 2. Patrol Routes (§5.7)

Solves: diminishing returns make the daily walk progressively less rewarding — and the daily
walk is the habit the whole game depends on.

- [x] Player defines a saved loop from their own walked path.
- [x] Re-walking grants **no cell-reveal XP** (correct — it is not new ground).
- [x] **Completing the circuit** awards a reliable batch of worker upkeep (food, coin).
- [x] Detect completion by proximity to the saved route's waypoints in order.

Novelty stays the only route to *progress*; routine becomes the route to *maintenance*. The
upkeep sink gets a dependable supply that does not demand constant novelty.

### 3. Uncharted Transit (§7.1)

Replaces the interim hard cap from Stage 01.

- [x] Ground covered above walking pace banks as **Uncharted Transit** — cells traversed but
      not revealed.
- [x] Daily cap on banked transit, so a long flight does not bank a continent.
- [x] Walking **redeems** banked transit nearby, at ~1 walked cell per 2–3 banked.
- [x] Unredeemed transit decays over ~a week — an opportunity, not an obligation.
- [x] Speed-graded conversion: full reveal at walking pace, partial at cycling pace, pure
      transit above. This handles cyclists without a special case.
- [x] **Banked transit on the map.** Drawn as a violet dashed layer above the fog, so it
      reads as ground glimpsed rather than cleared. Opacity carries the speed grading, and
      an overlay states what the cells are and what to do about them — without that the
      mechanic is invisible.

Converts the biggest exploit into the best retention mechanic: the commute becomes a reason
to walk, seeded along routes the player already travels.

### 4. District synergies (§5.5)

- [x] Contiguous Claims of **varied** terrain form a District with a combined bonus
      (Forest + Water + Farmland = "Homestead", +15% output).
- [x] Seeded District definitions — new ones cost nothing but data.
- [x] Without this the optimal play is nine identical cells of your best terrain; with it,
      you read the actual map.

### 5. Resource Surges (§5.6)

- [x] Temporary localised buffs on a POI category or terrain type within a region.
- [x] Generated server-side against existing regions — cheap to run.
- [x] Frequent, small, clearly signposted on the map.
- [x] **A player who ignores every surge must still progress fine.** A nudge, not an
      obligation.

---

## Acceptance criteria

1. Build green, tests pass.
2. A worker can be dispatched only to POIs with a real prior visit in `PlayerPoiVisit`.
3. Expedition duration scales with distance; returns the POI's exclusive materials.
4. A saved patrol route completes only when its waypoints are hit in order.
5. Patrol completion awards upkeep and **no** cell-reveal XP.
6. Movement at 15 m/s banks transit and reveals nothing.
7. Walking near banked transit redeems it at the configured ratio.
8. Banked transit decays after the configured window.
9. A varied-terrain District grants its bonus; a uniform one does not.
10. A surge measurably boosts its target and expires on schedule.
11. All earlier stages' acceptance criteria still pass.

## Definition of done

- All tasks ticked, all criteria verified.
- `STATUS.md`: stage 14 `DONE`, current stage `STAGE-15-remaining-skills.md`.
- Remove the Stage 01 interim speed cap, now superseded by Uncharted Transit.
