# Stage 16 — Combat encounters

> Roaming encounters and fixed training grounds. See `DESIGN.md` §5C.

## Status

- **State:** NOT STARTED
- **Completed:** _(none)_
- **Remaining:** all tasks
- **Notes:** Added after Stage 15, once the combat scope question was decided (§5C).
- **Blockers:** _(none)_

## Prerequisites

Stage 15 `DONE`. Read `DESIGN.md` §5C.

## Goal

Combat becomes an encounter system rather than a gathering ladder. Two sources with
different character: **roaming** encounters that spawn at any nearby POI and expire, and
**training grounds** fixed to historic POIs that never do.

**The rule that must not break:** historic POIs are a *boost*, never the only venue. A
player with no castle trains Combat entirely through roaming encounters — slower, never
blocked. This is the same geography rule every other skill follows, and Stage 10's Mining
tests are the template for proving it.

---

## Tasks

### 1. Schema

- [ ] `EncounterDefinition` **seeded**: `Key, Name, Description, MinCombatLevel, Tier,
      IsTrainingGround`.
- [ ] `PlayerEncounter`: `PlayerId, DefinitionKey, PoiId, SpawnedUtc, ExpiresUtc?,
      ResolvedUtc?`.
- [ ] Training grounds have no `ExpiresUtc` — permanence is what makes them the reliable
      route.

### 2. Spawning

- [ ] Roaming encounters spawn server-side against POIs the player could plausibly reach,
      reusing the **Resource Surge pattern** (§5.6): deterministic per cell per window,
      shared between players in the same place, cheap.
- [ ] **Any POI category** can host a roaming encounter — this is what guarantees a
      castle-less player still meets them.
- [ ] Training grounds derive from `historic=castle|fort|ruins|battlefield` and
      `military=*`, which `PoiImportService.TagMappings` already maps to Combat.
- [ ] Roaming encounters expire. Missing one costs nothing.

### 3. Resolution

- [ ] **Auto-resolving** — the walk was the input. No real-time interaction.
- [ ] Resolves against Combat level and equipped gear; yields XP and materials from the
      existing Combat ladder, which needs no change.
- [ ] Difficulty scales with Combat level so an encounter stays worth attempting.
- [ ] **Losing costs time, never materials.** §7.4's rule holds: nothing punishes a player
      for having been away.
- [ ] Arrival validated by the same server-side position check as POI visits and clue
      steps (§7.2) — an encounter must not become a spoofing vector.

### 4. App

- [ ] Encounters on the map, distinct from POI markers.
- [ ] Encounter detail: what it is, what it yields, whether it expires.
- [ ] Resolution result screen.

---

## Acceptance criteria

1. Build green, tests pass.
2. A roaming encounter spawns at a non-historic POI — proving they are not castle-gated.
3. **A fixture with zero historic POIs still trains Combat to any level.** This is the
   geography-lockout regression test, and it is the whole point of the stage.
4. A training ground does not expire; a roaming encounter does.
5. Resolution grants XP and materials at the correct tier for the player's Combat level.
6. A resolution attempt from outside range is rejected server-side.
7. Losing an encounter costs no materials.
8. Training grounds are measurably better than roaming — reliable and repeatable — so
   having one is an advantage without being a requirement.
9. All earlier stages' acceptance criteria still pass.

## Definition of done

- All tasks ticked, all criteria verified.
- `STATUS.md`: stage 16 `DONE`.
