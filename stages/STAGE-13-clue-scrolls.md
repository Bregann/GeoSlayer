# Stage 13 — Clue scrolls

> Directed exploration. The system that tells you where to walk, not just that you should.

## Status

- **State:** NOT STARTED
- **Completed:** _(none)_
- **Remaining:** all tasks
- **Notes:** _(none)_
- **Blockers:** _(none)_

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

- [ ] `ClueScrollDefinition` **seeded**: `Key, Tier, StepCount, RewardTableKey`.
- [ ] `PlayerClueScroll`: `Id, PlayerId, Tier, CurrentStep, StartedUtc, CompletedUtc?`.
- [ ] `ClueStep`: `ScrollId, StepIndex, StepType, TargetPoiId?, TargetLat?, TargetLng?,
      TargetRadius?, RiddleText, SolvedUtc?`.
- [ ] Tiers: `Wandering, Roaming, Pilgrim, Odyssey` — scaling step count, distance, rewards.
- [ ] **One active scroll per tier.** Prevents hoarding; keeps each meaningful.

### 2. Step generation

- [ ] Generate steps **relative to the player's own revealed territory and POIs**, so rural
      and urban players both get workable clues.
- [ ] **Always achievable** — never generate a step requiring a 200-mile trip. The §5.2
      geography rule applies here too. Bound generation by a reachable radius.
- [ ] Step types, all answerable from existing OSM data:
      - `Direct` — a named POI, validated by id
      - `Category` — "any lighthouse", works anywhere
      - `Coordinate` — a pin with a search radius
      - `Terrain` — from Stage 03 classification
      - `Cryptic` — template + POI tags
      - `Relational` — "where two rivers meet", spatial query
      - `Sequence` — position + bearing from a prior point
- [ ] Start with `Direct`, `Category` and `Coordinate` — the simplest three. Add `Cryptic`
      and `Relational` once those work end to end.

### 3. Cryptic generation

- [ ] Template table, seeded: `"Where the {faithful gather} beneath {three spires}"` built
      from `amenity=place_of_worship` + `building:levels`.
- [ ] Templates reference tag patterns, not specific POIs, so they apply anywhere.
- [ ] Validate that a generated riddle resolves to **exactly one** POI within the search
      radius. Ambiguous riddles are unsolvable — regenerate rather than ship them.

### 4. Completion and rewards

- [ ] Arrival validated **server-side** by the same anti-cheat as POI visits (§7.2). Clue
      completion must not become a spoofing vector.
- [ ] **One skip per scroll**, at a cost. A clue the player genuinely cannot solve must never
      permanently block their only scroll of that tier.
- [ ] **No time limits.** Clues are for savouring; they pair with weekend walks.
- [ ] Reward tables **seeded**, weighted toward Museum Relics, display items, Museum currency
      and materials.
- [ ] **Keep raw power low.** A player who ignores clues entirely must not fall behind.
      Clues are optional content, and their pull should be collection, not stats.
- [ ] Populate the Museum `Relics` wing from clue rewards.

### 5. App

- [ ] Clue screen: active scrolls, current step, riddle text, skip.
- [ ] Map integration — show the search area for coordinate steps, not the exact answer.
- [ ] Completion celebration and reward reveal.

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
