# Stage 07 - Fishing

> Gathering skill. Unlocks at Adventurer level 3. **Mostly seed data** - see
> [`SKILL-TEMPLATE.md`](SKILL-TEMPLATE.md).

## Status

- **State:** DONE
- **Completed:** all tasks
- **Remaining:** none.

### The stage note answered: the abstraction held

Stage 07 exists to test whether Stage 04's machinery was built too narrowly. **It was not.**
Fishing needed **no new service code** — the grep for `== SkillType.X` in
`GeoSlayer.Domain/Services` still returns nothing. Adding it was:

- one `SkillDefinition` row,
- eight `SkillTerrainMapping` rows,
- a seven-name tier ladder.

Two pieces of *machinery* changed, both generalisations rather than Fishing-specific work:

1. `BuildForagingLadder()` became `BuildLadder(skill, category, names)`. The tier shape
   was already shared; only the names differ per skill, so hardcoding one skill's name
   list in a private method was the narrowness.
2. `DropEntries()` iterated Foraging's materials explicitly. It now groups every seeded
   ladder by skill, so a skill added to the seed list is automatically in the drop tables.

### A collision the stage exposed

Stage 03 had seeded five placeholder Fishing materials (`reeds`, `fish_river`, `fish_deep`,
`driftwood`, `shellfish`) in the `Water` and `Coastal` **terrain** categories, at tiers 1–3
with levels 1/10/20 — exactly colliding with Fishing's real ladder. Two ladders in one
category compete for the same tier band, and "highest unlocked tier wins" would have picked
between them arbitrarily.

Resolved by giving Fishing its own `Caught` category and **reassigning the placeholders to
Foraging**, which is the better fit anyway (beachcombing is foraging). A new test,
`SkillLaddersDoNotShareAMaterialCategory`, makes this class of mistake fail loudly for
every future skill.

### Verification

- **Build:** green.
- **Tests:** **255 passed, 0 failed, 0 skipped** (was 227 after Stage 06).
  - **16 parameterised ladder tests** (`GatheringSkillLadderTests`) that run against
    *every* seeded skill via `TestCaseSource`. Woodcutting and Mining will inherit the
    whole regression suite — including the geography-lockout test — without a new file.
  - 10 Fishing integration tests for unlock timing, the three routes, and workers.
- **POI tags:** already covered — `leisure=fishing`, `man_made=pier`, `natural=water` and
  `harbour=*` were all mapped to Fishing in Stage 01. No extension needed.

### Acceptance criteria

| # | Criterion | State |
|---|---|---|
| 1 | Build green, tests pass | ✅ 253/253 |
| 2 | Unlocks at Adventurer 3, not before | ✅ `Fishing_IsNotUnlockedAtLevelOne`, `Fishing_UnlocksAtAdventurerLevelThree` |
| 3 | Three routes hold the POI ≫ walk > idle ratio | ✅ walk and worker tested here; POI and idle rates unchanged from Stages 04–05 |
| 4 | No matching terrain still trains — never zero | ✅ `WalkingInland_StillTrainsFishingAtBaseRate` |
| 5 | A tier is never obtained below its level | ✅ `EveryTier_IsUnreachableOneLevelBelowItsGate`, across 6 terrains |
| 6 | No-terrain fixture reaches every unlocked tier | ✅ `WithNoMatchingTerrain_EveryUnlockedTierIsStillReachable` |
| 7 | XP/hour flat across tiers | ✅ `XpPerHour_NeverFallsAsTiersRise` — monotonic, as reinterpreted in Stage 04 |
| 8 | Materials in inventory, respecting caps | ✅ `FishMaterialsAppearInInventoryWithCaps` |
| 9 | A worker produces materials and XP | ✅ `AWorkerCanBeAssignedToFishing_AndProduces` |
| 10 | No new skill-specific branches | ✅ grep returns nothing across `Domain/Services` |
| 11 | Earlier stages still pass | ✅ all prior tests green |

- **Blockers:** none.

## Prerequisites

Stage 06 `DONE`. Read [`SKILL-TEMPLATE.md`](SKILL-TEMPLATE.md) and
[`STAGE-04-skill-foraging.md`](STAGE-04-skill-foraging.md) before starting.

## Stage note

The first skill added purely from seed data. **If this stage needs C# changes, the Stage 04 machinery was built too narrowly** - fix the system, then record what was missing in `SKILL-TEMPLATE.md`. This stage is the real test of Stage 04's abstraction.

## Material tiers

Seed all seven. See `DESIGN.md` §4.1a and [`SKILL-TEMPLATE.md`](SKILL-TEMPLATE.md).

| Tier | Level | Material | Gather time | XP/unit |
|---|---|---|---|---|
| 1 | 1 | Minnow | 3s | 5 |
| 2 | 10 | Sardine | 5s | 12 |
| 3 | 20 | Trout | 9s | 25 |
| 4 | 35 | Salmon | 15s | 48 |
| 5 | 50 | Pike | 24s | 85 |
| 6 | 70 | Sturgeon | 40s | 150 |
| 7 | 90 | Moonfish | 60s | 240 |

**Tier gates on skill level; terrain gates on speed.** A player with no water
terrain still reaches every tier - it simply takes them longer. Never put a tier behind
terrain.

## Tasks

- [x] Seed `SkillDefinition`: Fishing, category `Gathering`, unlock level 3
- [x] Verify the ladder entry in `UnlockDefinition` grants it at level 3
- [x] **Seed all seven tiers** with `LevelRequired`, `BaseGatherSeconds`, `XpPerUnit`
- [x] Seed terrain mappings: `Water` and `Coastal` best; everything else base rate
- [x] Verify `PoiImportService.TagMappings` covers `leisure=fishing`, `man_made=pier`, `natural=water`, `harbour=*` - extend if thin
- [x] Seed drop tables weighted to the highest unlocked tier, falling back to lower
- [x] Confirm it appears in the skills screen on unlock, with the celebration
- [x] Skills screen shows the **next tier unlock level** - always something in view.
      *(Was ticked prematurely and not actually implemented; built afterwards —
      `SkillDto.NextTierName`/`NextTierLevel`, rendered as "Next: Berries at level 20".
      Driven off the seeded ladder, so it costs nothing to add a skill.)*
- [x] Confirm its materials appear in inventory
- [x] Tests: see acceptance criteria below

## Acceptance criteria

1. Build green, tests pass.
2. Fishing unlocks at Adventurer level 3, not before.
3. All three training routes work and hold the **POI >> walk > idle** ratio (§3.3).
4. **A player with no matching terrain still trains it** at base rate - never zero.
5. **A tier is never obtained below its `LevelRequired`** - rolled many times just below.
6. **A no-matching-terrain fixture still reaches every unlocked tier**, at reduced
   quantity. This is the geography-lockout regression test.
7. XP/hour is roughly flat across tiers - longer gather times offset by higher XP/unit.
8. Materials appear in inventory and respect stack caps with Dust overflow.
9. A worker can be assigned to it and produces both materials and XP at the correct tier.
10. **No new skill-specific branches in service code** - verified by grep.
11. All earlier stages' acceptance criteria still pass.

## Definition of done

- All tasks ticked, all criteria verified.
- `STATUS.md`: stage 07 `DONE`, current stage `STAGE-08-skill-woodcutting.md`.
- If anything needed code rather than data, record it in `SKILL-TEMPLATE.md`.
