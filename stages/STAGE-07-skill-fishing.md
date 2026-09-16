# Stage 07 - Fishing

> Gathering skill. Unlocks at Adventurer level 3. **Mostly seed data** - see
> [`SKILL-TEMPLATE.md`](SKILL-TEMPLATE.md).

## Status

- **State:** NOT STARTED
- **Completed:** _(none)_
- **Remaining:** all tasks
- **Notes:** _(none)_
- **Blockers:** _(none)_

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

- [ ] Seed `SkillDefinition`: Fishing, category `Gathering`, unlock level 3
- [ ] Verify the ladder entry in `UnlockDefinition` grants it at level 3
- [ ] **Seed all seven tiers** with `LevelRequired`, `BaseGatherSeconds`, `XpPerUnit`
- [ ] Seed terrain mappings: `Water` and `Coastal` best; everything else base rate
- [ ] Verify `PoiImportService.TagMappings` covers `leisure=fishing`, `man_made=pier`, `natural=water`, `harbour=*` - extend if thin
- [ ] Seed drop tables weighted to the highest unlocked tier, falling back to lower
- [ ] Confirm it appears in the skills screen on unlock, with the celebration
- [ ] Skills screen shows the **next tier unlock level** - always something in view
- [ ] Confirm its materials appear in inventory
- [ ] Tests: see acceptance criteria below

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
