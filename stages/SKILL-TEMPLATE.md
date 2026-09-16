# Skill stage template

The repeatable pattern for adding one skill. **Stage 04 (Foraging) is the worked example** —
read it before using this template.

By design, adding a skill after Stage 04 should be **mostly data, not code**. If a skill
stage requires significant new C#, that is a signal the systems in Stages 02–06 were built
too narrowly — fix the system rather than special-casing the skill.

## What Stage 04 proved (read this first)

Foraging was built as the worked example. The findings that change how you should use
this template:

**Adding a gathering skill is now genuinely seed data.** Three rows in
`SkillSeedData` — a `SkillDefinition`, a set of `SkillTerrainMapping` rows, and a tier
ladder built from `StandardLadder` — plus drop entries derived from those mappings. No
new C#. `ForagingTierTests.NoServiceCode_BranchesOnASpecificSkill` greps the whole
services tree and fails the build if a `== SkillType.X` comparison appears, so this stays
true rather than decaying.

**Every gathering skill MUST have a `TerrainType.Open` mapping row.** That row is the
base rate, and it is the single thing standing between the design and a geographic
lockout. `SkillTrainingService.XpForCell` falls back to it when no terrain flag matches;
a skill without one silently trains nothing on unclassified ground.

**Things that needed code, not data — already built, do not rebuild:**

- `SkillTrainingService.TrainFromCells` — matches every unlocked skill against the
  seeded mappings. Generic.
- `SkillTrainingService.VisitPoi` — range check, decay, visit log. Generic; the skill
  comes from the POI's own mapping.
- `VisitDecay` — the §3.4 curve. Shared by every skill.
- `DropRoller` — tier selection and the level gate. Shared.

**Two traps Stage 04 hit, both worth checking for in a new skill stage:**

1. **Double-granting.** `FogService` used to grant Exploration XP directly. Once
   Exploration had a terrain mapping it was paid twice. If a skill is granted anywhere
   other than through `SkillTerrainMapping`, delete that path rather than adding a
   guard.
2. **POI tag mappings are first-match-wins.** `PoiImportService.TagMappings` returns the
   first matching row, so a tag already claimed by an earlier skill cannot be remapped by
   appending. Check for an existing claim before adding — an appended duplicate is dead
   code that reads as though it works. (`natural=wood` is Woodcutting's; `leisure=park` is
   Exploration's.)

**The XP ladder is not flat.** The table below yields 6,000 → 14,400 XP/hour from tier 1
to 7. That is intended — §4.1a requires only that advancing is never a *punishment*. Test
it as monotonic non-decreasing, not as flat.

## What Stage 07 (Fishing) proved

Fishing was the first skill added purely from seed data, and **it worked** — no new service
code, and the `== SkillType.X` grep still returns nothing. Adding a gathering skill is now:

```csharp
// 1. SkillSeedData.Definitions — one row
new() { SkillType = SkillType.Woodcutting, Name = "Woodcutting", …, UnlockLevel = 5, … }

// 2. SkillSeedData.TerrainMappings — one Open row (MANDATORY) plus the good terrains
new() { SkillType = SkillType.Woodcutting, Terrain = TerrainType.Open,     XpPerCell = 1.0, … }
new() { SkillType = SkillType.Woodcutting, Terrain = TerrainType.Woodland, XpPerCell = 3.0, … }

// 3. A ladder, and add it to AllSkillMaterials
public static IReadOnlyList<Material> WoodcuttingMaterials { get; } = BuildLadder(
    SkillType.Woodcutting, MaterialCategory.Logged, [ ("key", "Name"), … seven … ]);
```

**Then write no tests.** `GatheringSkillLadderTests` is parameterised over every seeded
ladder, so a new skill automatically inherits tier gating, the geography-lockout
regression test, XP/hour, and the Open-mapping check. Add an integration file only for
something genuinely skill-specific.

**Two rules that will bite:**

- **Every skill needs its own `MaterialCategory`.** Two ladders in one category compete for
  the same tier band and the roll picks between them arbitrarily. Stage 03's placeholder
  fish collided with Fishing's real ladder exactly this way.
  `SkillLaddersDoNotShareAMaterialCategory` now catches it.
- **Check `PoiImportService.TagMappings` for tags already claimed.** First match wins, so
  appending a duplicate is dead code that reads as though it works.

## The pattern

### 1. Seed data — the bulk of the work

- **Skill definition** — `SkillType` enum value (most already exist), unlock level in the
  ladder table, display name, icon, description.
- **Material tiers** — see below. This is the bulk of a skill's content.
- **Terrain mappings** — which cell terrain types train this skill while walking, and at
  what rate.
- **POI mappings** — already exist in `PoiImportService.TagMappings`. Verify the skill's
  tags are covered and the `XpReward` weights are sensible. Extend if thin.
- **Drop tables** — what gathering yields, at what rate, by tier and player level.
- **Museum entries** (once Stage 12 is done) — one per material per tier.

### 1a. Material tiers — required for every skill

Read `DESIGN.md` §4.1a. **Every gathering skill needs a full tier ladder**, roughly one new
material every 10–15 levels. Seven tiers to level 90 is the standard shape:

| Tier | Level | Gather time | XP/unit |
|---|---|---|---|
| 1 | 1 | 3s | 5 |
| 2 | 10 | 5s | 12 |
| 3 | 20 | 9s | 25 |
| 4 | 35 | 15s | 48 |
| 5 | 50 | 24s | 85 |
| 6 | 70 | 40s | 150 |
| 7 | 90 | 60s | 240 |

Rules that apply to **every** skill:

- **`LevelRequired` gates access absolutely** — below the level, not obtainable at all.
- **Higher tiers take longer** (`BaseGatherSeconds`) but grant proportionally more XP, so
  XP/hour stays roughly flat. Never punish a player for advancing.
- **Tier gates on skill level; terrain gates on speed.** Do *not* hide high tiers behind rare
  terrain — that rebuilds the geographic lockout. A landlocked player at level 50 gets the
  tier-5 material, just more slowly than someone with ideal terrain.
- Lower tiers becoming irrelevant is **intentional**, not a flaw.

**Production skills** invert this: recipes carry `LevelRequired` and `DurationSeconds`, and
higher-tier recipes consume higher-tier inputs — which is what chains gathering to production
rather than leaving them as parallel bars.

### 2. Rates and balance

Fill in the standard ratio from `DESIGN.md` §3.3 — **POI ≫ walking > idle**:

| Route | Rate |
|---|---|
| Walk through matching terrain | ~1 XP/cell |
| Visit a matching POI | ~20× a terrain cell |
| Worker assigned | slow trickle, offline, capped |

Terrain is a **multiplier, never a gate** (§5.2). A player with no matching terrain must
still be able to train the skill, just slower.

### 3. App

- Skill appears in the skills screen when unlocked.
- Its materials appear in inventory.
- Unlock celebration fires at the ladder level.
- Any skill-specific UI (usually none — the generic screens should cover it).

### 4. Tests

- Walking a synthetic path through matching terrain grants the expected XP.
- Walking through non-matching terrain grants base rate, not zero.
- Visiting a matching POI grants the POI rate.
- Drop tables produce expected distributions over N rolls (seeded RNG).
- **A material is never obtained below its `LevelRequired`** — roll many times just below.
- **A no-matching-terrain fixture still yields every unlocked tier**, at reduced quantity.
- XP/hour is roughly flat across tiers.

### 5. Acceptance criteria

- Skill unlocks at the correct Adventurer level.
- All three training routes work and hold the correct ratio.
- Materials appear in inventory and respect stack caps.
- Every tier gates on level and is reachable regardless of local terrain.
- Nothing in an earlier stage regressed.
- Build green, tests pass.

## Checklist to copy into a new skill stage

```markdown
## Status
- **State:** NOT STARTED
- **Completed:** _(none)_
- **Remaining:** all tasks
- **Notes:** _(none)_
- **Blockers:** _(none)_

## Tasks
- [ ] Seed skill definition + ladder entry
- [ ] Seed the full material tier ladder (7 tiers: levels 1/10/20/35/50/70/90)
- [ ] Set LevelRequired, BaseGatherSeconds, XpPerUnit per tier
- [ ] Seed terrain mappings + rates (speed only - never gate tiers on terrain)
- [ ] Verify/extend POI tag mappings
- [ ] Seed drop tables with highest-unlocked-tier weighting
- [ ] Wire into skills screen; show the next tier unlock level
- [ ] Tests: terrain XP, POI XP, base rate, drop distribution, tier gating,
      no-terrain fixture reaches all tiers, flat XP/hour
- [ ] Verify no regression in earlier stages
- [ ] Confirm the skill has a `TerrainType.Open` mapping row (the base rate)
- [ ] Check `PoiImportService.TagMappings` for tags already claimed by another skill
- [ ] Confirm no service grants this skill XP outside `SkillTerrainMapping`
```
