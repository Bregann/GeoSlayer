# Stage 14 — Retention systems

> Expeditions, Patrol Routes, Uncharted Transit, Districts, Surges. What makes the game
> survive contact with month two.

## Status

- **State:** NOT STARTED
- **Completed:** _(none)_
- **Remaining:** all tasks
- **Notes:** _(none)_
- **Blockers:** _(none)_

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

- [ ] Dispatch a worker to **any POI the player has personally visited at least once**. The
      `PlayerPoiVisit` log from Stage 04 already has this — nearly free to implement.
- [ ] Duration scales with real-world distance. Distant POIs are high-value, low-frequency.
- [ ] Returns that POI's exclusive materials and skill XP.
- [ ] Occupies the worker, competing with Claim work — a real decision.
- [ ] Populate the Museum `Expeditions` wing with trophies.

This turns every trip the player has ever taken into a permanent asset, and softens the
rural/urban imbalance (§7.3) from a new angle: a rural player who visits a city once gains
lasting access to urban materials.

### 2. Patrol Routes (§5.7)

Solves: diminishing returns make the daily walk progressively less rewarding — and the daily
walk is the habit the whole game depends on.

- [ ] Player defines a saved loop from their own walked path.
- [ ] Re-walking grants **no cell-reveal XP** (correct — it is not new ground).
- [ ] **Completing the circuit** awards a reliable batch of worker upkeep (food, coin).
- [ ] Detect completion by proximity to the saved route's waypoints in order.

Novelty stays the only route to *progress*; routine becomes the route to *maintenance*. The
upkeep sink gets a dependable supply that does not demand constant novelty.

### 3. Uncharted Transit (§7.1)

Replaces the interim hard cap from Stage 01.

- [ ] Ground covered above walking pace banks as **Uncharted Transit** — cells traversed but
      not revealed.
- [ ] Daily cap on banked transit, so a long flight does not bank a continent.
- [ ] Walking **redeems** banked transit nearby, at ~1 walked cell per 2–3 banked.
- [ ] Unredeemed transit decays over ~a week — an opportunity, not an obligation.
- [ ] Speed-graded conversion: full reveal at walking pace, partial at cycling pace, pure
      transit above. This handles cyclists without a special case.
- [ ] Show banked transit on the map as a distinct visual state.

Converts the biggest exploit into the best retention mechanic: the commute becomes a reason
to walk, seeded along routes the player already travels.

### 4. District synergies (§5.5)

- [ ] Contiguous Claims of **varied** terrain form a District with a combined bonus
      (Forest + Water + Farmland = "Homestead", +15% output).
- [ ] Seeded District definitions — new ones cost nothing but data.
- [ ] Without this the optimal play is nine identical cells of your best terrain; with it,
      you read the actual map.

### 5. Resource Surges (§5.6)

- [ ] Temporary localised buffs on a POI category or terrain type within a region.
- [ ] Generated server-side against existing regions — cheap to run.
- [ ] Frequent, small, clearly signposted on the map.
- [ ] **A player who ignores every surge must still progress fine.** A nudge, not an
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
