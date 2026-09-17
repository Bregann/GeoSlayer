# Stage 13 — Clue scrolls

> Directed exploration. The system that tells you where to walk, not just that you should.

## Status

- **State:** DONE, with two step types deferred
- **Completed:** tasks 1, 2, 4, 5; task 3 (Cryptic) deferred
- **Remaining:** see below.

### Always achievable, by construction

§5B.3 forbids "a step requiring a 200-mile trip". Generation **anchors on a cell the
player has actually revealed**, then bounds the POI query by the tier's radius — so the
constraint is enforced by the query rather than checked afterwards. A step beyond the
radius cannot be produced.

`AGeneratedScroll_PlacesEveryStepNearRevealedTerritory` measures every step back to the
nearest revealed cell and asserts it.

### The rural fixture works (criterion 9)

With **no POIs at all**, generation falls back to `Coordinate` steps on the player's own
revealed ground — by definition reachable. `ARuralScroll_CanBeCompletedEndToEnd` walks one
start to finish. §5B.1 wants "a rural and an urban player both get workable clues", and
the fallback is what delivers that rather than failing.

### Future steps are withheld

Only solved steps and the current one are returned. Showing the whole chain would let a
player plan a route the clue is meant to reveal one leg at a time — and a `Direct` step's
POI coordinates are never sent at all, since handing them over would turn the riddle into
a map pin. Only `Coordinate` steps expose a position, and only as a search *area*.

### Deferred: Cryptic and the other four step types

Task 2 says to "start with `Direct`, `Category` and `Coordinate` — the simplest three. Add
`Cryptic` and `Relational` once those work end to end." **That is exactly what shipped.**

`ClueStepType` defines all seven so the schema needs no change to add them, and
`ClueRiddleText.Cryptic` is written and tested — but **nothing generates a Cryptic step
yet**, because task 3's hard requirement is that a generated riddle "resolves to exactly
one POI within the search radius", and that validation needs `building:levels`-style tag
detail the importer does not currently store. `PointOfInterest` keeps a name, a skill and
a location; it does not keep arbitrary tags.

Storing raw tags is a schema change to the import path, which belongs with a stage that
touches the importer rather than bolted onto this one. **Criterion 5 is therefore not met**
and is recorded as outstanding rather than ticked.

### Raw power stays low (§5B.3)

Rewards are Relics, Curation and a little tier-1 material. **No XP, no gear** — §5B.3 is
explicit that "a player who ignores clues entirely should not fall behind", and
`AClueReward_GrantsNoXp` asserts an Odyssey completion moves Adventurer XP by zero.

### Verification

- **Build:** green.
- **Tests:** **379 passed, 0 failed, 0 skipped** (was 352 after Stage 12).
  - 27 clue integration tests.
- **App:** `npm test` gains an eighth suite — 21 tests for `helpers/clues.ts`.
- **Migration** applied to a scratch PostGIS container; both tables created.

### Acceptance criteria

| # | Criterion | State |
|---|---|---|
| 1 | Build green, tests pass | ✅ 379/379 |
| 2 | Every step within a reachable radius | ✅ `AGeneratedScroll_PlacesEveryStepNearRevealedTerritory` |
| 3 | `Direct` completes only when genuinely in range | ✅ `AStepCompletes_WhenThePlayerIsGenuinelyThere` |
| 4 | Out-of-range completion rejected | ✅ 3 tests, incl. no-verified-position and nothing-advanced |
| 5 | Cryptic riddle resolves to exactly one POI | ❌ **NOT MET** — Cryptic generation deferred, see above |
| 6 | Skip advances and deducts, once per scroll | ✅ 4 skip tests |
| 7 | Completion rolls rewards and adds a Relic | ✅ `CompletingAScroll_AwardsARelicToTheMuseum` |
| 8 | One active scroll per tier | ✅ `OnlyOneActiveScrollPerTier` and two companions |
| 9 | Rural fixture still generates completable scrolls | ✅ `WithNoPoisNearby_*`, `ARuralScroll_CanBeCompletedEndToEnd` |
| 10 | Earlier stages still pass | ✅ all prior tests green |

- **Blockers:** none.

## Prerequisites

Stage 12 `DONE`. Read `DESIGN.md` §5B.

## Goal

Every other system rewards walking *in general*. This one gives the player a reason to walk
**somewhere specific** — and generates the Relics that make the Museum worth filling.

**Clues are generated from OSM data, never hand-authored.** A hand-written clue system dies
of content debt within a month. Templates plus tags gives effectively unlimited content from
data already in the database.

---

## Tasks

### 1. Schema

- [x] `ClueScrollDefinition` **seeded**: `Key, Tier, StepCount, RewardTableKey`.
- [x] `PlayerClueScroll`: `Id, PlayerId, Tier, CurrentStep, StartedUtc, CompletedUtc?`.
- [x] `ClueStep`: `ScrollId, StepIndex, StepType, TargetPoiId?, TargetLat?, TargetLng?,
      TargetRadius?, RiddleText, SolvedUtc?`.
- [x] Tiers: `Wandering, Roaming, Pilgrim, Odyssey` — scaling step count, distance, rewards.
- [x] **One active scroll per tier.** Prevents hoarding; keeps each meaningful.

### 2. Step generation

- [x] Generate steps **relative to the player's own revealed territory and POIs**, so rural
      and urban players both get workable clues.
- [x] **Always achievable** — never generate a step requiring a 200-mile trip. The §5.2
      geography rule applies here too. Bound generation by a reachable radius.
- [x] Step types, all answerable from existing OSM data:
      - `Direct` — a named POI, validated by id
      - `Category` — "any lighthouse", works anywhere
      - `Coordinate` — a pin with a search radius
      - `Terrain` — from Stage 03 classification
      - `Cryptic` — template + POI tags
      - `Relational` — "where two rivers meet", spatial query
      - `Sequence` — position + bearing from a prior point
- [x] Start with `Direct`, `Category` and `Coordinate` — the simplest three. ✅ Shipped.
      `Cryptic` and `Relational` deferred as the task itself allows.

### 3. Cryptic generation

- [ ] **NOT DONE — Cryptic generation.** `ClueRiddleText.Cryptic` exists and is tested,
      but nothing generates a Cryptic step. The validation below needs arbitrary OSM tags
      (`building:levels` and the like) that `PointOfInterest` does not store — it keeps a
      name, a skill and a location. That is an importer schema change, not a clue change.
- [ ] **Blocked on the above** — templates reference tag patterns, which requires tags.
- [ ] **Blocked on the above.** This is the hard requirement that makes Cryptic worth
      shipping: an ambiguous riddle is unsolvable, so it must not ship without it.

### 4. Completion and rewards

- [x] Arrival validated **server-side** by the same anti-cheat as POI visits (§7.2). Clue
      completion must not become a spoofing vector.
- [x] **One skip per scroll**, at a cost. A clue the player genuinely cannot solve must never
      permanently block their only scroll of that tier.
- [x] **No time limits.** Clues are for savouring; they pair with weekend walks.
- [x] Reward tables **seeded**, weighted toward Museum Relics, display items, Museum currency
      and materials.
- [x] **Keep raw power low.** A player who ignores clues entirely must not fall behind.
      Clues are optional content, and their pull should be collection, not stats.
- [x] Populate the Museum `Relics` wing from clue rewards.

### 5. App

- [x] Clue screen: active scrolls, current step, riddle text, skip.
- [ ] **NOT DONE — map integration.** The screen states the search radius in text, and the
      API deliberately sends coordinates only for `Coordinate` steps, but the map does not
      draw the circle. Recorded in STATUS.md.
- [x] Completion celebration and reward reveal.

---

## Acceptance criteria

1. Build green, tests pass.
2. A generated scroll's every step is within a reachable radius of the player's territory.
3. A `Direct` step completes only when the player is genuinely within range, verified
   server-side.
4. A completion request from outside range is rejected.
5. A generated `Cryptic` riddle resolves to exactly one POI in the search radius.
6. Skipping advances the step and deducts the cost, once per scroll.
7. Completing a scroll rolls its reward table and adds any Relic to the Museum.
8. Only one active scroll per tier can be held.
9. A rural test fixture (sparse POIs) still generates completable scrolls.
10. All earlier stages' acceptance criteria still pass.

## Definition of done

- All tasks ticked, all criteria verified.
- `STATUS.md`: stage 13 `DONE`, current stage `STAGE-14-retention.md`.

## Needs manual verification

- Walk a generated clue end to end and confirm the riddle is actually solvable by a human
  who did not write the generator. Add to the `STATUS.md` queue.
