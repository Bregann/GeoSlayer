# Stage 11 - Smithing

> Production skill. Unlocks at Adventurer level 16. **Mostly seed data** - see
> [`SKILL-TEMPLATE.md`](SKILL-TEMPLATE.md).

## Status

- **State:** NOT STARTED
- **Completed:** _(none)_
- **Remaining:** all tasks
- **Notes:** _(none)_
- **Blockers:** _(none)_

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

- [ ] Seed `SkillDefinition`: Smithing, category `Production`, unlock level 16
- [ ] Verify the ladder entry in `UnlockDefinition` grants it at level 16
- [ ] **Seed all seven tiers** with `LevelRequired`, `DurationSeconds`, `XpPerUnit`
- [ ] Seed terrain mappings: **None** - production skill
- [ ] Verify `PoiImportService.TagMappings` covers `craft=blacksmith`, `man_made=works`, `landuse=industrial` - extend if thin
- [ ] Seed drop tables weighted to the highest unlocked tier, falling back to lower
- [ ] Confirm it appears in the skills screen on unlock, with the celebration
- [ ] Skills screen shows the **next tier unlock level** - always something in view
- [ ] Confirm its materials appear in inventory
- [ ] Tests: see acceptance criteria below

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
