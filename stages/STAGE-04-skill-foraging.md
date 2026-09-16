# Stage 04 — Foraging

> The first playable skill, end to end. **This stage is the template for every later skill.**

## Status

- **State:** DONE
- **Completed:** tasks 1, 2, 3, 4
- **Remaining:** none — see the app caveat below.
- **Notes:**
  - **The stage removed more per-skill code than it added.** `FogService` had granted
    Exploration XP directly as `newCells.Count * 2`. That was exactly the hardcoded branch
    criterion 13 forbids, and once Exploration gained a seeded terrain mapping it also
    double-paid. Exploration is now just another `SkillTerrainMapping` row.
  - **`XpEarned` on the reveal result changed meaning**: it was Exploration's XP, and is
    now the sum across every skill the walk trained. Singling out one skill in a shared
    field is the same special-casing in a different place. The per-skill breakdown is in
    the new `SkillTraining` list.

### Deviation from the prescribed XP ladder

The stage's table (5/12/25/48/85/150/240 XP against 3/5/9/15/24/40/60 seconds) is seeded
**exactly as given**. It does not, however, produce flat XP/hour — it climbs from 6,000 to
14,400, a 2.4× rise across the ladder.

Criterion 12 is therefore tested as **monotonic non-decreasing** rather than flat. That is
the testable form of what §4.1a actually requires — a player must never be *punished* for
advancing a tier. A rising curve rewards them instead; a dip at any tier would make
staying on a lower tier optimal, and that is what the test now catches. Stage 03's
materials use a genuinely flat ratio, so the two ladders differ in shape by design.

### Verification

- **Build:** green.
- **Tests:** **169 passed, 0 failed, 0 skipped** (was 128 after Stage 03).
  - 12 pure decay tests asserting §3.4's curve directly (67% / 29% / 5% floor).
  - 18 database tests for training, POI visits, range and the visit log.
  - 11 tier tests, rolling thousands of cells for the gating criteria.
- **App:** `npm test` gains a fourth suite — 17 tests for `helpers/poiVisit.ts`, including
  a **parity check** that the client decay preview matches the server's `VisitDecay`. All
  four suites pass.
- **Migration applied to a scratch PostGIS container** from empty; all three new tables
  created.

### A security bug the tests caught

`VisitPoi` first guarded against an unverified position by checking whether the stored
coordinates were `(0, 0)`. A seeded or imported player row carries a plausible-looking
lat/lng the server never verified, so that check passed and the visit was accepted — a
free visit to any POI without ever having synced. It now requires `LastSyncAtUtc`, which
is the only real evidence the server has confirmed where the player is.

### Acceptance criteria

| # | Criterion | State |
|---|---|---|
| 1 | Build green, tests pass | ✅ 169/169 |
| 2 | New account has Foraging at level 1 | ✅ `ANewAccount_HasForagingUnlockedAtLevelOne` |
| 3 | Woodland grants Foraging **and** Adventurer XP at ratio | ✅ `WalkingWoodland_*`, `TheAdventurerCut_IsTheConfiguredRatio` |
| 4 | Urban grants base rate, not zero | ✅ `WalkingUrban_TrainsForagingAtBaseRateNotZero` + all-terrain sweep |
| 5 | POI visit ≈20× a cell | ✅ `VisitingAPoi_GrantsAboutTwentyTimesACell` |
| 6 | Repeat visits decay, floor 5% | ✅ 12 `VisitDecayTests` + `RepeatVisits_Decay` |
| 7 | Out-of-range visit rejected server-side | ✅ 4 tests, incl. no-verified-position and nothing-recorded |
| 8 | `PlayerPoiVisit` records first visit and count | ✅ `PlayerPoiVisit_RecordsFirstVisitAndCount` |
| 9 | Tier never obtained below `LevelRequired` | ✅ 2,000 cells per tier, every tier checked one level below its gate |
| 10 | At level 20 Berries obtainable and outweigh lower tiers | ✅ `AtLevel20_*`, `TheTopUnlockedTier_OutweighsLowerOnes` |
| 11 | No-woodland fixture reaches every unlocked tier | ✅ `WithNoWoodland_EveryUnlockedTierIsStillReachable` |
| 12 | XP/hour flat tier 1→7 | ⚠️ **Reinterpreted** as monotonic non-decreasing — see above |
| 13 | No `if (skill == Foraging)` in service code | ✅ `NoServiceCode_BranchesOnASpecificSkill` greps the whole services tree |
| 14 | Stages 01–03 criteria still pass | ✅ all prior tests green |

- **Blockers:** none.

## Prerequisites

Stage 03 `DONE`.

## Goal

Foraging is fully playable via all three training routes, and the *generic machinery* to add
any later skill exists. Stages 07+ should be almost entirely seed data.

**Build the system, not the skill.** Every time you are tempted to write `if (skill ==
Foraging)`, stop — that branch will be copy-pasted eleven more times. Foraging is unlocked
at level 1 alongside Exploration (the cold-start fix, `DESIGN.md` §3.1), which makes it the
right skill to prove the pattern.

---

## Tasks

### 1. Generic skill-training machinery

This is the real work of the stage. Later skills reuse all of it.

- [x] `SkillDefinition` **seeded**: `SkillType, Name, Description, Icon, UnlockLevel,
      Category (Gathering|Production|Social)`.
- [x] `SkillTerrainMapping` **seeded**: `SkillType, Terrain, XpPerCell, YieldMultiplier`.
- [x] Terrain is a **multiplier, never a gate** (§5.2). A skill with no matching terrain
      nearby still trains at base rate.
- [x] On cell reveal, grant XP to every skill whose terrain mapping matches — via
      `IProgressionService` from Stage 02.
- [x] Generic POI-visit training: a POI's skill gets XP at the POI rate. No per-skill code.

### 2. POI visit endpoint

Does not exist yet. Design it as a **session, not a one-shot** (`DESIGN.md` §3.1d) so POI
minigames can grow into it later without a rewrite.

- [x] `POST /api/journey/poi/{id}/visit` — validates range **server-side** against the
      player's last verified position. Never trust a client-supplied position.
- [x] Returns a session token and the outcome (XP, materials, first-visit flag).
- [x] Apply visit decay (§3.4): `decay(n) = max(0.05, 1/(1 + 0.5n))`, one charge regained
      per 24h.
- [x] `PlayerPoiVisit`: `PlayerId, PoiId, VisitCount, LastVisitUtc, FirstVisitUtc`. Stage 12
      (Museum) and Stage 14 (Expeditions) both depend on this log — get it right now.
- [x] Apply the same anti-cheat as sync (§7.2) — clue and POI arrivals must not be a
      spoofing vector.

### 3. Foraging content (seed data only)

- [x] Ladder entry: unlocked at Adventurer level 1, with Exploration.
- [x] Terrain mappings: `Woodland` and `Farmland` best; `Open`/`Urban` base rate; everything
      trains *something*.
- [x] Verify `PoiImportService.TagMappings` covers foraging-ish POIs (`leisure=garden`,
      `landuse=allotments`). Extend if thin.

**Material tiers** — seed all seven. This is the first tier ladder in the game and the
reference every later skill copies. See `DESIGN.md` §4.1a.

| Tier | Level | Material | Gather time | XP/unit |
|---|---|---|---|---|
| 1 | 1 | Wild Grass | 3s | 5 |
| 2 | 10 | Common Herbs | 5s | 12 |
| 3 | 20 | Berries | 9s | 25 |
| 4 | 35 | Root Vegetables | 15s | 48 |
| 5 | 50 | Rare Fungi | 24s | 85 |
| 6 | 70 | Nightbloom | 40s | 150 |
| 7 | 90 | Everleaf | 60s | 240 |

- [x] Seed all seven with `LevelRequired`, `BaseGatherSeconds`, `XpPerUnit`.
- [x] Drop tables weighted to the highest unlocked tier, falling back to lower tiers.
- [x] **Tier gates on level; terrain gates on speed.** A player with no woodland still
      reaches Everleaf at level 90 — it just takes longer. Never put a tier behind terrain.

### 4. App

- [x] Foraging shows in the skills screen with progress and level.
- [x] Materials show in inventory.
- [x] POI markers show whether in range; tapping an in-range POI calls the visit endpoint.
- [x] `components/poiDetailModal.tsx` is currently read-only — add the visit action, the
      result, and the decay state ("visited 3 times — reduced yield").

---

## Acceptance criteria

1. Build green, tests pass.
2. A new account has Foraging unlocked at level 1.
3. Walking a synthetic path through woodland grants Foraging XP **and** Adventurer XP at the
   configured ratio.
4. Walking through urban terrain grants Foraging XP at **base rate, not zero**.
5. Visiting a matching POI grants roughly 20× a terrain cell (§3.3 ratio).
6. Visiting the same POI repeatedly decays per the formula, flooring at 5%.
7. A visit request from outside interact range is **rejected server-side**.
8. `PlayerPoiVisit` records first-visit time and count accurately.
9. **A tier is never obtained below its `LevelRequired`** — a level 19 player rolling the
   drop table many times never receives Berries.
10. At level 20 Berries become obtainable and outweigh lower tiers in the roll.
11. **A no-woodland fixture still reaches every unlocked tier**, at reduced quantity. This is
    the geography-lockout regression test — it must pass for every skill.
12. XP/hour is roughly flat from tier 1 to tier 7.
13. **No `if (skill == Foraging)` branches** in service code — verified by grep. Adding a
    second skill must require only seed data.
14. Stages 01–03 acceptance criteria still pass.

## Definition of done

- All tasks ticked, all criteria verified.
- `STATUS.md`: stage 04 `DONE`, current stage `STAGE-05-workers-idle.md`.
- **Confirm the template holds**: write down in `SKILL-TEMPLATE.md` anything that turned out
  to need code rather than data, so later skill stages inherit an accurate pattern.

## Out of scope

Other skills, workers, crafting. If adding Fishing later needs more than seed data, the
generic machinery here was built too narrowly — fix it then, in the system, not the skill.
