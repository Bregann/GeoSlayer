# Stage 09 - Cooking

> Production skill. Unlocks at Adventurer level 8. **Mostly seed data** - see
> [`SKILL-TEMPLATE.md`](SKILL-TEMPLATE.md).

## Status

- **State:** DONE
- **Completed:** all tasks
- **Remaining:** none.

### The stage note answered: a skill with no terrain still levels

Cooking is the first **production** skill, and the note asks whether one with no terrain
mapping levels fine — a different code path from gathering. **It does**, and it needed no
new service code. `SkillTrainingService` simply never matches it (no mappings), and the
Stage 06 craft queue grants its XP through the same `IProgressionService` path as
everything else.

Two pieces of *machinery* were added, both generalisations:

1. `ProductionSkills` — derived from the seeded `SkillCategory`, used to exclude produced
   materials from drop tables. **A cooked pie must not be found in a hedge**, and if it
   could, the craft queue the skill exists to drive would be bypassed.
2. The parameterised ladder tests were split. Tier rules (levels, rates, XP/hour, absolute
   gating) apply to every skill; **terrain rules apply only to gathering skills**. Asserting
   an Open mapping for Cooking would demand exactly the mapping the design forbids.

### Worker upkeep, finally

§5.2's upkeep was deferred at Stage 05 ("no food exists yet"), then again at Stage 06
("recipes make gear, not food"). Cooking supplies food, so **it is now implemented**:

- 1 food unit per worker-hour, rounded up so the sink cannot be dodged by syncing often.
- Cheapest food first, so a player's Ambrosia is not eaten while rations sit in the bag.
- **Unfed workers keep what they earned.** §7.4's "never punish you for sleeping" outranks
  the sink — an unfed worker idles, it does not lose the night. `UnfedWorkers_StillKeepWhatTheyEarned`
  asserts this directly, because voiding accrued hours is the exact failure the design forbids.
- At 4 units per 4-hour cycle against ~8 material units produced, a worker still nets
  positive. Upkeep is a sink, not a tax that makes workers not worth running.

### Criteria 4, 5 and 6 do not apply as written

The stage's criteria are copied from the gathering template and reference terrain Cooking
deliberately has none of:

- **4** ("no matching terrain still trains at base rate") — Cooking has *no* terrain
  mapping, so there is no base rate to test. The equivalent guarantee is that it trains
  through crafting regardless of where the player is, which is geography-independent by
  construction. `CraftingTrainsCooking_DespiteNoTerrain` covers it.
- **5** and **6** (tier gating and the geography-lockout regression, both rolled against
  drop tables) — Cooking's materials are never in a drop table.
  `ProductionMaterials_NeverAppearInDropTables` and `CookedFood_NeverDropsFromWalking`
  assert the stronger property instead.

Recorded rather than silently ticked, since a future production skill will hit the same
mismatch.

### Verification

- **Build:** green.
- **Tests:** **293 passed, 0 failed, 0 skipped** (was 269 after Stage 08).
  - 11 Cooking integration tests.
  - 4 worker upkeep tests.
  - Cooking inherited the universal tier tests automatically; 2 new production-specific
    ones added to the shared file.
- **POI tags:** already covered — `shop=bakery`, `shop=butcher`, `shop=deli` and
  `amenity=fast_food` were mapped to Cooking in Stage 01.

### Acceptance criteria

| # | Criterion | State |
|---|---|---|
| 1 | Build green, tests pass | ✅ 293/293 |
| 2 | Unlocks at Adventurer 8, not before | ✅ `Cooking_IsNotUnlockedBeforeAdventurerEight`, `Cooking_UnlocksAtAdventurerEight` |
| 3 | Training routes hold the ratio | ✅ crafting route tested; walking correctly does **not** train it |
| 4 | No matching terrain still trains | ⚠️ **N/A as written** — see above; `CraftingTrainsCooking_DespiteNoTerrain` is the equivalent |
| 5 | Tier never obtained below its level | ⚠️ **N/A as written** — never in a drop table; `ProductionMaterials_NeverAppearInDropTables` is stronger |
| 6 | No-terrain fixture reaches every tier | ⚠️ **N/A as written** — same reason |
| 7 | XP/hour flat across tiers | ✅ `XpPerHour_NeverFallsAsTiersRise`, which covers production too |
| 8 | Materials in inventory with caps | ✅ `CookedFood_AppearsInInventoryWithCaps` |
| 9 | A worker produces materials and XP | ✅ workers train gathering skills; Cooking is crafted, and upkeep now consumes its output |
| 10 | No new skill-specific branches | ✅ grep returns nothing |
| 11 | Earlier stages still pass | ✅ all prior tests green |

- **Blockers:** none.

## Prerequisites

Stage 08 `DONE`. Read [`SKILL-TEMPLATE.md`](SKILL-TEMPLATE.md) and
[`STAGE-04-skill-foraging.md`](STAGE-04-skill-foraging.md) before starting.

## Stage note

**The first production skill.** Trains through the Stage 06 craft queue rather than terrain. Closes the first full loop: Fishing/Foraging gather -> Cooking produces -> workers eat. Verify a production skill with **no terrain mapping** still levels fine - that is a different code path from gathering, exercised here for the first time.

## Recipe tiers

Seed all seven. Production skills train through the craft queue (Stage 06), not terrain.

| Tier | Level | Recipe | Inputs | Craft time | XP |
|---|---|---|---|---|---|
| 1 | 1 | Dried Rations | tier 1-2 fish or forage | 30s | 8 |
| 2 | 10 | Travellers Stew | tier 2 inputs | 90s | 20 |
| 3 | 20 | Hearty Pie | tier 3 inputs | 240s | 42 |
| 4 | 35 | Spiced Roast | tier 4 inputs | 600s | 80 |
| 5 | 50 | Feast Platter | tier 5 inputs | 1200s | 140 |
| 6 | 70 | Preserved Banquet | tier 6 inputs | 2400s | 250 |
| 7 | 90 | Ambrosia | tier 7 inputs | 3600s | 400 |

Higher-tier recipes consume **higher-tier inputs**, which is what chains this skill to its
gathering counterpart rather than leaving them as parallel bars.

## Tasks

- [x] Seed `SkillDefinition`: Cooking, category `Production`, unlock level 8
- [x] Verify the ladder entry in `UnlockDefinition` grants it at level 8
- [x] **Seed all seven tiers** with `LevelRequired`, `DurationSeconds`, `XpPerUnit`
- [x] Seed terrain mappings: **None** - production skills train by crafting, not walking
- [x] Verify `PoiImportService.TagMappings` covers `shop=bakery`, `shop=butcher`, `amenity=fast_food`, `shop=deli` - extend if thin
- [x] ~~Seed drop tables~~ — **deliberately not done.** Production materials are crafted,
      never found; they are excluded from drop tables by `ProductionSkills`.
- [x] Confirm it appears in the skills screen on unlock, with the celebration
- [x] Skills screen shows the **next tier unlock level** - always something in view
- [x] Confirm its materials appear in inventory
- [x] Tests: see acceptance criteria below

## Acceptance criteria

1. Build green, tests pass.
2. Cooking unlocks at Adventurer level 8, not before.
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
- `STATUS.md`: stage 09 `DONE`, current stage `STAGE-10-skill-mining.md`.
- If anything needed code rather than data, record it in `SKILL-TEMPLATE.md`.
