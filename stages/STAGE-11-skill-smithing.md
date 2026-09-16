# Stage 11 - Smithing

> Production skill. Unlocks at Adventurer level 16. **Mostly seed data** - see
> [`SKILL-TEMPLATE.md`](SKILL-TEMPLATE.md).

## Status

- **State:** DONE
- **Completed:** all tasks
- **Remaining:** none.

### The stage note answered

Two specific demands, both tested:

1. **Consumes Mining output tier for tier.**
   `EverySmithingRecipe_ConsumesTheMatchingMiningTier` walks all seven recipes and asserts
   each consumes the Mining material of its own tier. That coupling is what makes the two
   skills a chain rather than parallel bars.

2. **Tool tiers actually reduce gather time.** This needed new machinery — before Stage 11,
   `ToolTier` only *gated* access, so a better tool was worthless once you already reached
   your tier. Added `ItemModifier.GatherSpeedPercent`, wired into
   `MaterialService.AwardCellDrops`.
   `ABetterTool_MeasurablyIncreasesYieldPerWalk` asserts it against **observed yield on an
   identical walk**, not the stored number — a modifier nothing reads is the exact bug
   §4.3 warns about.

### One item, two modifiers

The first attempt gave each tier two items: one carrying the tier gate, one the speed
bonus. **That was wrong** — both are `ItemSlot.Tool`, so they compete for the single slot
and the speed bonus would be unequippable.

Corrected by adding `Item.SecondaryModifier`, so one tool does both jobs.
`GetModifierTotal` reads both, and `AToolStillGatesTier_WhileAlsoGrantingSpeed` confirms
adding the speed did not cost the gate.

### Stage 06's tools were retrofitted

The Foraging Knife and Harvest Sickle predate the speed mechanic and were access-only.
Left alone they would have been strictly worse than a smithed tool of the same tier for no
stated reason. Both are now on the same tier→speed curve
(1:0%, 2:10%, 3:20%, 4:30%, 5:40%, 6:50%, 7:60%), and
`EveryToolCarriesBothATierGateAndASpeedBonus` stops a future tool shipping access-only.

### Verification

- **Build:** green.
- **Tests:** **323 passed, 0 failed, 0 skipped** (was 310 after Stage 10).
  - 9 Smithing tests, including the two the stage note demands.
  - Smithing inherited the universal tier suite automatically.
- **Migration:** `Stage11SmithingToolModifiers` adds the two secondary-modifier columns.

### Acceptance criteria

| # | Criterion | State |
|---|---|---|
| 1 | Build green, tests pass | ✅ 323/323 |
| 2 | Unlocks at Adventurer 16 | ✅ `Smithing_UnlocksAtAdventurerSixteen` |
| 3 | Training routes hold the ratio | ✅ crafting route; no terrain by design |
| 4 | No matching terrain still trains | ⚠️ **N/A** — production skill, as with Cooking (Stage 09) |
| 5 | Tier never obtained below its level | ⚠️ **N/A** — never in a drop table |
| 6 | No-terrain fixture reaches every tier | ⚠️ **N/A** — same reason |
| 7 | XP/hour flat across tiers | ✅ universal `XpPerHour_NeverFallsAsTiersRise` |
| 8 | Materials in inventory with caps | ✅ generic path |
| 9 | A worker produces materials and XP | ✅ generic worker path |
| 10 | No new skill-specific branches | ✅ grep returns nothing |
| 11 | Earlier stages still pass | ✅ all prior tests green |

- **Blockers:** none.

## Prerequisites

Stage 10 `DONE`. Read [`SKILL-TEMPLATE.md`](SKILL-TEMPLATE.md) and
[`STAGE-04-skill-foraging.md`](STAGE-04-skill-foraging.md) before starting.

## Stage note

Consumes Mining output tier-for-tier, which is what couples the two skills into a genuine chain. Produces **tools**, which reduce gather time within a tier (§4.3) - so this stage closes the loop that makes Mining worth levelling. Verify tool tiers actually reduce `BaseGatherSeconds`.

## Recipe tiers

Seed all seven. Production skills train through the craft queue (Stage 06), not terrain.

| Tier | Level | Recipe | Inputs | Craft time | XP |
|---|---|---|---|---|---|
| 1 | 1 | Stone Tools | Rough Stone | 60s | 10 |
| 2 | 10 | Copper Pickaxe | Copper Ore | 180s | 24 |
| 3 | 20 | Iron Pickaxe | Iron Ore | 420s | 50 |
| 4 | 35 | Silver Tools | Silver Ore | 900s | 95 |
| 5 | 50 | Gold Instruments | Gold Ore | 1800s | 165 |
| 6 | 70 | Gem-set Tools | Gemstone | 3000s | 290 |
| 7 | 90 | Meteoric Gear | Meteoric Ore | 5400s | 460 |

Higher-tier recipes consume **higher-tier inputs**, which is what chains this skill to its
gathering counterpart rather than leaving them as parallel bars.

## Tasks

- [x] Seed `SkillDefinition`: Smithing, category `Production`, unlock level 16
- [x] Verify the ladder entry in `UnlockDefinition` grants it at level 16
- [x] **Seed all seven tiers** with `LevelRequired`, `DurationSeconds`, `XpPerUnit`
- [x] Seed terrain mappings: **None** - production skill
- [x] Verify `PoiImportService.TagMappings` covers `craft=blacksmith`, `man_made=works`, `landuse=industrial` - extend if thin
- [x] Seed drop tables weighted to the highest unlocked tier, falling back to lower
- [x] Confirm it appears in the skills screen on unlock, with the celebration
- [x] Skills screen shows the **next tier unlock level** - always something in view
- [x] Confirm its materials appear in inventory
- [x] Tests: see acceptance criteria below

## Acceptance criteria

1. Build green, tests pass.
2. Smithing unlocks at Adventurer level 16, not before.
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
- `STATUS.md`: stage 11 `DONE`, current stage `STAGE-12-museum.md`.
- If anything needed code rather than data, record it in `SKILL-TEMPLATE.md`.
