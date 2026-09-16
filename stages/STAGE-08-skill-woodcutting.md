# Stage 08 - Woodcutting

> Gathering skill. Unlocks at Adventurer level 5. **Mostly seed data** - see
> [`SKILL-TEMPLATE.md`](SKILL-TEMPLATE.md).

## Status

- **State:** DONE
- **Completed:** all tasks
- **Remaining:** none.

### The stage note answered: one cell, two skills

Woodcutting was again **pure seed data** — one skill definition, eight terrain mappings,
a seven-name ladder. No service code. The `== SkillType.X` grep still returns nothing.

The stage note's concern was multi-skill terrain: a woodland cell trains both Foraging and
Woodcutting, each rolling its own tier. **It works, and is now tested** — seven tests in
`MultiSkillTerrainTests` covering the ways it could plausibly be wrong:

- Both skills train from one cell (rather than the first match winning).
- Each earns its **own** rate, not a share — one woodland cell pays 3 XP to each, not 1.5.
- Unlocking the second skill does not reduce the first.
- A locked second skill still does not train.
- Both ladders appear in drops — neither crowds the other out.
- **Each tier respects its own skill's level.** A player at Woodcutting 20 and Foraging 1
  gets tier-3 logs and only tier-1 forage, not tier 3 of both.
- One Claim can suit both skills, so a worker's assignment stays unambiguous.

### The same collision as Stage 07, caught by the Stage 07 test

Stage 03's three timber placeholders (`timber_rough`, `timber_oak`, `timber_yew`) sat in
the `Woodland` category on `SkillType.Woodcutting` at tiers 1–3, levels 1/10/20 — colliding
with Woodcutting's real ladder exactly as the fish did with Fishing's.
`SkillLaddersDoNotShareAMaterialCategory`, added in Stage 07 for precisely this, would have
failed. Resolved the same way: Woodcutting takes a new `Logged` category, and
`timber_rough` is reassigned to Foraging — gathered deadfall — and **kept** because three
Stage 06 recipes reference it.

### Verification

- **Build:** green.
- **Tests:** **269 passed, 0 failed, 0 skipped** (was 255 after Stage 07).
  - Woodcutting inherited **7 parameterised ladder tests automatically** via
    `TestCaseSource` — tier gating, the geography-lockout regression, XP/hour and the
    Open-mapping check, with no new test file. This is the Stage 07 investment paying off.
  - 7 new multi-skill terrain tests.
- **POI tags:** already covered — `natural=wood`, `landuse=forest` and
  `leisure=nature_reserve` were mapped to Woodcutting in Stage 01.

### Acceptance criteria

| # | Criterion | State |
|---|---|---|
| 1 | Build green, tests pass | ✅ 269/269 |
| 2 | Unlocks at Adventurer 5 | ✅ seeded ladder rung; `ALockedSecondSkill_DoesNotTrain` confirms it is not earlier |
| 3 | Three routes hold the ratio | ✅ walk tested here; POI and idle unchanged since Stages 04–05 |
| 4 | No matching terrain still trains | ✅ `GatheringSkillLadderTests` (parameterised, includes Woodcutting) |
| 5 | Tier never obtained below its level | ✅ `EveryTier_IsUnreachableOneLevelBelowItsGate` |
| 6 | No-terrain fixture reaches every tier | ✅ `WithNoMatchingTerrain_EveryUnlockedTierIsStillReachable` |
| 7 | XP/hour flat across tiers | ✅ `XpPerHour_NeverFallsAsTiersRise` |
| 8 | Materials in inventory with caps | ✅ generic inventory path, covered since Stage 03 |
| 9 | A worker produces materials and XP | ✅ `AWorkerOnWoodland_TrainsWhicheverSkillItIsAssigned` |
| 10 | No new skill-specific branches | ✅ grep returns nothing |
| 11 | Earlier stages still pass | ✅ all prior tests green |

- **Blockers:** none.

## Prerequisites

Stage 07 `DONE`. Read [`SKILL-TEMPLATE.md`](SKILL-TEMPLATE.md) and
[`STAGE-04-skill-foraging.md`](STAGE-04-skill-foraging.md) before starting.

## Stage note

Pairs with Foraging on woodland terrain - a woodland cell should train **both**. Verify multi-skill terrain grants work; this is the first stage where one cell feeds two skills, and each must roll its own tier independently.

## Material tiers

Seed all seven. See `DESIGN.md` §4.1a and [`SKILL-TEMPLATE.md`](SKILL-TEMPLATE.md).

| Tier | Level | Material | Gather time | XP/unit |
|---|---|---|---|---|
| 1 | 1 | Deadwood | 3s | 5 |
| 2 | 10 | Softwood | 5s | 12 |
| 3 | 20 | Oak | 9s | 25 |
| 4 | 35 | Ash | 15s | 48 |
| 5 | 50 | Yew | 24s | 85 |
| 6 | 70 | Ironbark | 40s | 150 |
| 7 | 90 | Elderwood | 60s | 240 |

**Tier gates on skill level; terrain gates on speed.** A player with no woodland
terrain still reaches every tier - it simply takes them longer. Never put a tier behind
terrain.

## Tasks

- [x] Seed `SkillDefinition`: Woodcutting, category `Gathering`, unlock level 5
- [x] Verify the ladder entry in `UnlockDefinition` grants it at level 5
- [x] **Seed all seven tiers** with `LevelRequired`, `BaseGatherSeconds`, `XpPerUnit`
- [x] Seed terrain mappings: `Woodland` best; `Farmland`/`Open` reduced; everything else base rate
- [x] Verify `PoiImportService.TagMappings` covers `natural=wood`, `landuse=forest`, `leisure=nature_reserve` - extend if thin
- [x] Seed drop tables weighted to the highest unlocked tier, falling back to lower
- [x] Confirm it appears in the skills screen on unlock, with the celebration
- [x] Skills screen shows the **next tier unlock level** - always something in view
- [x] Confirm its materials appear in inventory
- [x] Tests: see acceptance criteria below

## Acceptance criteria

1. Build green, tests pass.
2. Woodcutting unlocks at Adventurer level 5, not before.
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
- `STATUS.md`: stage 08 `DONE`, current stage `STAGE-09-skill-cooking.md`.
- If anything needed code rather than data, record it in `SKILL-TEMPLATE.md`.
