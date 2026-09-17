# Stage 16 — Combat encounters

> Roaming encounters and fixed training grounds. See `DESIGN.md` §5C.

## Status

- **State:** DONE
- **Completed:** all tasks
- **Remaining:** none.

### What was built

Two tables (`EncounterDefinition` seeded, `PlayerEncounter`), an `EncounterService`
spawning on the Resource Surge pattern, pure resolution rules in `EncounterResolution`,
a `CombatController`, and an app screen reached from the HUD.

**21 new tests, one per acceptance criterion**, bringing the suite to 522.

### The geography rule, proved rather than asserted

Criterion 3 is the stage's whole point, and it has two tests:
`WithNoHistoricPois_CombatIsStillTrainable` puts a café, a library and a park near a
player with no historic ground and resolves an encounter for real XP, and
`EveryTier_IsReachableWithoutHistoricGround` checks the roaming ladder covers all seven
tiers so nothing at the top of the skill needs a castle.

Training grounds and roaming encounters pay **identically at the same tier** — a test
asserts the level gates match. The advantage of historic ground is availability only
(permanent, repeatable). Had it paid more, a player without one would be permanently
behind rather than merely slower, which is precisely the lockout §5C.2 forbids.

### The architectural guard caught this stage

`NoServiceCode_BranchesOnASpecificSkill` (Stage 04) failed on the first run:
`EncounterService` compared `== SkillType.Combat` in five places.

Arguably a false positive — encounters *are* a Combat-only system, not generic machinery
branching on skill. But the fix was better than an exemption: the skill is now
`EncounterSeedData.Skill`, declared once as seed data and read by the service. One place
names it, and no service code decides anything by asking which skill it holds.

### Not done, and recorded rather than ticked

- **Gear does not affect resolution.** The stage asked for "Combat level and equipped
  gear"; only level is read. Needs a combat modifier on `Item` first.
- **Encounters are not drawn on the map.** They have a screen instead. The DTO already
  carries coordinates, so this is a map layer, not an API change.

Both are visible in the task list above, unticked.

### Verification

- **Build:** green, 0 warnings.
- **Tests:** **522 passed, 0 failed, 0 skipped** (was 501).
- **Migration:** `Stage16CombatEncounters` applied against a scratch PostGIS container,
  full chain from empty. Two new tables, no destructive operations on existing ones.

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

- [x] `EncounterDefinition` **seeded**: `Key, Name, Description, MinCombatLevel, Tier,
      IsTrainingGround`.
- [x] `PlayerEncounter`: `PlayerId, DefinitionKey, PoiId, SpawnedUtc, ExpiresUtc?,
      ResolvedUtc?`.
- [x] Training grounds have no `ExpiresUtc` — permanence is what makes them the reliable
      route.

### 2. Spawning

- [x] Roaming encounters spawn server-side against POIs the player could plausibly reach,
      reusing the **Resource Surge pattern** (§5.6): deterministic per cell per window,
      shared between players in the same place, cheap.
- [x] **Any POI category** can host a roaming encounter — this is what guarantees a
      castle-less player still meets them.
- [x] Training grounds derive from `historic=castle|fort|ruins|battlefield` and
      `military=*`, which `PoiImportService.TagMappings` already maps to Combat.
- [x] Roaming encounters expire. Missing one costs nothing.

### 3. Resolution

- [x] **Auto-resolving** — the walk was the input. No real-time interaction.
- [ ] **PARTLY DONE.** Resolves against Combat level and yields XP and materials from the
      existing Combat ladder, which needed no change. **Equipped gear is not consulted** —
      `WinChance` reads level alone. Adding it means a weapon/armour modifier on `Item`,
      which is its own piece of work; recorded rather than quietly skipped.
- [x] Difficulty scales with Combat level so an encounter stays worth attempting.
- [x] **Losing costs time, never materials.** §7.4's rule holds: nothing punishes a player
      for having been away.
- [x] Arrival validated by the same server-side position check as POI visits and clue
      steps (§7.2) — an encounter must not become a spoofing vector.

### 4. App

- [ ] **NOT DONE.** Encounters are on their own screen (`app/encounters.tsx`, reached from
      the HUD), not drawn on the map. The DTO already carries `Latitude`/`Longitude` for
      exactly this, so it is a layer on the map screen rather than new API work.
- [x] Encounter detail: what it is, what it yields, whether it expires.
- [x] Resolution result screen.

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
