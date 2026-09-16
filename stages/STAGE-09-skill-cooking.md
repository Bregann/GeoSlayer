# Stage 09 - Cooking

> Production skill. Unlocks at Adventurer level 8. **Mostly seed data** - see
> [`SKILL-TEMPLATE.md`](SKILL-TEMPLATE.md).

## Status

- **State:** NOT STARTED
- **Completed:** _(none)_
- **Remaining:** all tasks
- **Notes:** _(none)_
- **Blockers:** _(none)_

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

- [ ] Seed `SkillDefinition`: Cooking, category `Production`, unlock level 8
- [ ] Verify the ladder entry in `UnlockDefinition` grants it at level 8
- [ ] **Seed all seven tiers** with `LevelRequired`, `DurationSeconds`, `XpPerUnit`
- [ ] Seed terrain mappings: **None** - production skills train by crafting, not walking
- [ ] Verify `PoiImportService.TagMappings` covers `shop=bakery`, `shop=butcher`, `amenity=fast_food`, `shop=deli` - extend if thin
- [ ] Seed drop tables weighted to the highest unlocked tier, falling back to lower
- [ ] Confirm it appears in the skills screen on unlock, with the celebration
- [ ] Skills screen shows the **next tier unlock level** - always something in view
- [ ] Confirm its materials appear in inventory
- [ ] Tests: see acceptance criteria below

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
