# Stage 10 - Mining

> Gathering skill. Unlocks at Adventurer level 12. **Mostly seed data** - see
> [`SKILL-TEMPLATE.md`](SKILL-TEMPLATE.md).

## Status

- **State:** DONE
- **Completed:** all tasks
- **Remaining:** none.

### The accessibility stress test: passed

The stage note is the strongest claim in the whole plan — if a player with zero rocky
terrain cannot reach tier 7 Meteoric Ore by levelling alone, "§4.1a was broken somewhere
and every other skill is suspect too". So it is tested directly, not inferred:

- `ASuburbanPlayerAtMiningNinety_ObtainsMeteoricOre` walks a player across Urban, Open,
  Woodland and Farmland — **never Rocky** — and finds tier 7.
- `EveryMiningTier_IsReachableWithoutRockyTerrain` does the same for all seven tiers.
- `RockyGround_IsMeaningfullyFasterWithoutBeingRequired` quantifies the compensating
  difference and **asserts it stays under 4×**. §4.1a wants the gap "meaningful, not
  disqualifying"; that test is where it shows up if the ratio ever drifts.

**The rule holds.** A suburban player reaches every tier.

### Tool gating, deferred here from Stage 06

Stage 06 built tools with a `ToolTier` modifier but left the gate unenforced, recording
that it belonged with Mining — §4.3's own example is a pickaxe, and gating Foraging behind
a tool would block the first skill a new player meets. **It is now enforced**, in
`DropRoller.Roll` via an optional `maxToolTier`.

The important detail: **no tool means no cap**, not tier 1. A player with nothing equipped
is unrestricted, because applying a cap by default would silently lock every skill behind
gear the tutorial never teaches — which is the reason the gate was deferred rather than
shipped in Stage 06. `WithNoToolEquipped_EveryTierIsStillReachable` asserts this.

### The Stage 03 collision, resolved differently this time

Fishing and Woodcutting both hit a placeholder collision and resolved it by *reassigning*
the placeholders to another skill. Mining is different: Stage 03's five rocky materials
(`stone_rough` … `ore_gold`) **are** Mining's real ladder, just incomplete and under
their own keys.

So Stage 10 **adopted the keys** and extended them to seven tiers in `SkillSeedData`,
removing the duplicates from `MaterialSeedData`. Two definitions of one ore is exactly how
a key ends up silently seeded with the wrong tier. The existing Stage 03 tests that
reference `ore_iron` and `ore_gold` keep working and now cover the real ladder.

### Verification

- **Build:** green.
- **Tests:** **310 passed, 0 failed, 0 skipped** (was 293 after Stage 09).
  - 9 Mining accessibility and tool-gating tests.
  - Mining inherited the universal ladder suite automatically.
- **POI tags:** already covered — `man_made=mineshaft`, `landuse=quarry` and
  `natural=cave_entrance` were mapped to Mining in Stage 01.

### Acceptance criteria

| # | Criterion | State |
|---|---|---|
| 1 | Build green, tests pass | ✅ 310/310 |
| 2 | Unlocks at Adventurer 12 | ✅ `MiningUnlocksAtAdventurerTwelve` |
| 3 | Three routes hold the ratio | ✅ walk tested; POI and idle unchanged since Stages 04–05 |
| 4 | No matching terrain still trains | ✅ `UrbanGround_TrainsMiningAtBaseRate` |
| 5 | Tier never obtained below its level | ✅ universal `EveryTier_IsUnreachableOneLevelBelowItsGate` |
| 6 | No-terrain fixture reaches every tier | ✅ **the stress test** — `EveryMiningTier_IsReachableWithoutRockyTerrain` |
| 7 | XP/hour flat across tiers | ✅ `XpPerHour_NeverFallsAsTiersRise` |
| 8 | Materials in inventory with caps | ✅ generic path, covered since Stage 03 |
| 9 | A worker produces materials and XP | ✅ generic worker path, covered in Stage 05/07 |
| 10 | No new skill-specific branches | ✅ grep returns nothing |
| 11 | Earlier stages still pass | ✅ all prior tests green |

- **Blockers:** none.

## Prerequisites

Stage 09 `DONE`. Read [`SKILL-TEMPLATE.md`](SKILL-TEMPLATE.md) and
[`STAGE-04-skill-foraging.md`](STAGE-04-skill-foraging.md) before starting.

## Stage note

**The accessibility stress test.** Mining terrain is genuinely rare for most players - this is the skill that proves the tier-gates-on-level rule (`DESIGN.md` §4.1a). Explicitly verify a player with **zero rocky terrain** can still reach tier 7 Meteoric Ore purely by levelling. If they cannot, the rule was broken somewhere and every other skill is suspect too.

## Material tiers

Seed all seven. See `DESIGN.md` §4.1a and [`SKILL-TEMPLATE.md`](SKILL-TEMPLATE.md).

| Tier | Level | Material | Gather time | XP/unit |
|---|---|---|---|---|
| 1 | 1 | Rough Stone | 3s | 5 |
| 2 | 10 | Copper Ore | 5s | 12 |
| 3 | 20 | Iron Ore | 9s | 25 |
| 4 | 35 | Silver Ore | 15s | 48 |
| 5 | 50 | Gold Ore | 24s | 85 |
| 6 | 70 | Gemstone | 40s | 150 |
| 7 | 90 | Meteoric Ore | 60s | 240 |

**Tier gates on skill level; terrain gates on speed.** A player with no rocky
terrain still reaches every tier - it simply takes them longer. Never put a tier behind
terrain.

## Tasks

- [x] Seed `SkillDefinition`: Mining, category `Gathering`, unlock level 12
- [x] Verify the ladder entry in `UnlockDefinition` grants it at level 12
- [x] **Seed all seven tiers** with `LevelRequired`, `BaseGatherSeconds`, `XpPerUnit`
- [x] Seed terrain mappings: `Rocky` and `Industrial` best; everything else base rate
- [x] Verify `PoiImportService.TagMappings` covers `man_made=mineshaft`, `landuse=quarry`, `natural=cave_entrance` - extend if thin
- [x] Seed drop tables weighted to the highest unlocked tier, falling back to lower
- [x] Confirm it appears in the skills screen on unlock, with the celebration
- [x] Skills screen shows the **next tier unlock level** - always something in view
- [x] Confirm its materials appear in inventory
- [x] Tests: see acceptance criteria below

## Acceptance criteria

1. Build green, tests pass.
2. Mining unlocks at Adventurer level 12, not before.
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
- `STATUS.md`: stage 10 `DONE`, current stage `STAGE-11-skill-smithing.md`.
- If anything needed code rather than data, record it in `SKILL-TEMPLATE.md`.
