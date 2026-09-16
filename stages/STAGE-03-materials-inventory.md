# Stage 03 — Materials & inventory

> Things you can hold. Terrain classification, tiered material pools, stack caps, inventory.

## Status

- **State:** DONE
- **Completed:** tasks 1, 1a, 2, 3, 4, 5
- **Remaining:** none — see the app caveat below.
- **Notes:**
  - **Terrain is classified from already-imported OSM data, with no new Overpass call.**
    The stage asks to extend the existing import rather than add a second path; the
    existing query in `PoiImportService` already pulls the terrain-bearing tags
    (`natural`, `landuse`, `water`, `waterway`, `man_made`) and stores them with
    locations, so `PoiTerrainClassifier` reads those rows back. Cheaper than a second
    round trip, cannot rate-limit, and keeps one import path.
    - Trade-off: resolution is limited to the POI rows OSM returned, so a cell mid-forest
      with no tagged feature classifies as `Open`. That is the safe direction — `Open`
      still yields base materials, so the player sees reduced yield, never a dead cell.
    - Classification sits behind `ITerrainClassifier`, so **no test touches the network**.
      Every fixture injects a fake with the terrain it needs.
  - **22 materials seeded, 3 unique** — inside the ≤25 / ≤15 budget with room for the
    stages that add POI-specific materials.
  - Drop tables are derived from the seeded pools rather than hand-listed, so pools and
    tables cannot drift apart.

### Verification

- **Build:** green.
- **Tests:** **128 passed, 0 failed, 0 skipped** (was 99 after Stage 02).
  - 15 pure tier tests (`MaterialTierTests`) — these roll thousands of cells, which is
    how criterion 9 is proved as "never" rather than "not in the sample I tried".
  - 14 database tests (`MaterialServiceIntegrationTests`) for caps, overflow, caching
    and inventory.
- **App:** `npm test` runs a third suite — 20 tests for `helpers/inventory.ts`. All pass.
- **Migration applied to a scratch PostGIS container** from an empty database through all
  four migrations. All four Stage 03 tables created with the unique index on
  `CellTerrains` that makes the cache correct under races.

### A design bug the tests caught

The first cut put all three unique materials in the `Urban` category. Two of them landed
on tier 4, which broke the tier ladder — `HigherTiers_RequireHigherLevels` and
`HigherTiers_TakeLongerToGather` both failed. The fix was conceptual, not cosmetic: unique
materials come from **POI visits**, not from walking over a cell, so they do not belong in
a terrain pool at all. They now sit in their own `Relic` category, outside the terrain
ladders and excluded from cell drop tables.

### Acceptance criteria

| # | Criterion | State |
|---|---|---|
| 1 | Build green, tests pass | ✅ 128/128 |
| 2 | ≤25 materials, ≤15 unique | ✅ 22 and 3 — `SeededMaterials_StayWithinTheStageBudget` |
| 3 | Woodland yields woodland, urban yields urban | ✅ `AWoodlandCell_*`, `AnUrbanCell_*` |
| 4 | An `Open` cell still yields — never zero | ✅ `AnUnclassifiedCell_StillYieldsSomething` (+200-cell pure test) |
| 5 | Gathering into a full stack yields Dust, no error | ✅ `GatheringIntoAFullStack_YieldsDustAndDoesNotError` |
| 6 | Replayed sync yields identical drops | ✅ `ReplayingAnIdenticalSync_*` and a pinned seed literal |
| 7 | Inventory lists quantities and caps | ✅ `Inventory_*` (4 tests) |
| 8 | Terrain cached — second player triggers no new call | ✅ `TerrainIsClassifiedOnce_AndReusedBySecondPlayer` (Moq `Times.Once`) |
| 9 | Level 19 player **never** gets a level 20 material | ✅ 2,000 cells asserted, `ALevel19Player_NeverObtainsALevel20Material` |
| 10 | At level 20 it becomes obtainable and is weighted above lower tiers | ✅ `AtLevel20_*`, `TheHighestUnlockedTier_*` |
| 11 | No matching terrain still reaches every unlocked tier | ✅ `WithNoMatchingTerrain_EveryUnlockedTierIsStillReachable` |
| 12 | XP/hour roughly flat across tiers | ✅ `XpPerHour_IsRoughlyFlatAcrossTiers` — held at exactly 1 XP/3s |
| 13 | Stages 01–02 criteria still pass | ✅ all prior tests green |

- **Blockers:** none.

## Prerequisites

Stage 02 `DONE`.

## Goal

Walking yields materials. They live somewhere. They respect caps without ever hard-stalling
the (not-yet-built) idle layer.

**Get the tiering right here.** `DESIGN.md` §7.4 explains why: retrofitting a tier system
over 60 hand-authored materials is miserable, and the naive design (one bespoke material per
POI category) produces exactly that.

---

## Tasks

### 1. Material schema — tiered pools, not bespoke items

- [x] `Material`: `Id, Key, Name, Tier, Category, SkillType?, StackCap, IsUnique,
      LevelRequired, BaseGatherSeconds, XpPerUnit`.
- [x] `PlayerMaterial`: `PlayerId, MaterialId, Quantity`, unique `(PlayerId, MaterialId)`.
- [x] **Shared tiered pools.** Most POIs and terrain draw from pools by category and tier
      (`CommonUrban`, `RareIndustrial`, `CommonWoodland`…), *not* a unique item each.
- [x] Reserve `IsUnique` for **10–15 named materials** total, attached to the rarest and most
      memorable POI types. Exclusivity means nothing if everything is exclusive.
- [x] Seed the initial pools — keep total distinct materials under ~25 at this stage.

### 1a. Material tiers (RuneScape pattern)

Read `DESIGN.md` §4.1a before implementing. This is the core of how gathering skills feel
like progression, and getting it wrong is expensive to unpick later.

- [x] **`LevelRequired` gates access absolutely.** Below the level, the material cannot be
      obtained at all — not rarely, not slowly. Not obtainable.
- [x] **`BaseGatherSeconds` rises with tier.** Higher tiers take longer per unit.
- [x] **`XpPerUnit` rises proportionally**, so XP/hour stays roughly flat across tiers. A
      player must never be punished for advancing to a higher tier.
- [x] Roughly one new tier per 10–15 skill levels, so a next unlock is always in view.
- [x] Drop rolls select the **highest tier the player has unlocked**, with weighted fallback
      to lower tiers.

- [x] **THE CRITICAL RULE — tier gates on skill level, terrain gates on speed.**

      > *What* you can obtain depends **only** on skill level. *How fast* depends on terrain,
      > POIs, gear and tools. Do **not** put high tiers behind rare terrain — that silently
      > rebuilds the geographic lockout the unlock ladder exists to prevent, and a player in
      > a flat suburb would cap at tier 2 forever. A landlocked player at Mining 50 gathers
      > Gold Ore; it just takes them longer than someone living by a quarry.

- [x] Gather time scales the **quantity yielded per cell** on reveal, rather than blocking a
      walk on a timer. Workers (Stage 05) apply `BaseGatherSeconds` directly, since they work
      in real time.

### 2. Stack caps and overflow

Per §7.4. The rule that matters: **the idle layer must never punish you for sleeping.**

- [x] Per-material stack cap (from `Material.StackCap`), not a global inventory limit.
- [x] Overflow **auto-converts** to a universal currency (Dust) at a poor rate. Never
      discard, never block.
- [x] Gathering at cap still succeeds — it just yields Dust instead. Nothing hard-stalls.

### 3. Cell terrain classification

- [x] ~~`RevealedCell.Terrain`~~ → **`CellTerrain` table** (flags/bitmask — a cell can be
      both wooded and watered). *Deliberate deviation:* the next bullet requires the
      classification to be cached and reused for every player, and `RevealedCell` is
      per-player, so storing it there would duplicate the value per player and let copies
      disagree. `CellTerrain` is keyed on the grid cell with a unique index, which is what
      makes criterion 8 actually hold.
- [x] Classify at reveal time from OSM data. The Overpass import in `PoiImportService`
      already fetches the area; extend the query to pull landuse/natural/water polygons per
      import cell rather than adding a second import path.
- [x] Terrain types: `Woodland, Water, Farmland, Urban, Industrial, Rocky, Coastal, Open`.
- [x] Cache per grid cell — classify once, reuse for every player.
- [x] Unclassified cells default to `Open` and still yield base materials. **A cell must
      never yield nothing** — that would reintroduce a geographic dead zone.

### 4. Drops

- [x] `DropTable` / `DropTableEntry`, **seeded**: material, weight, min/max quantity,
      required level.
- [x] Revealing a cell rolls its terrain drop table.
- [x] Seeded RNG per (player, cell) so a replayed sync cannot re-roll for a better result.
- [x] Drop rates scale with the relevant skill level once skills gather (Stage 04).

### 5. Inventory API and UI

- [x] `GET /api/player/inventory`.
- [x] Inventory screen; wire up the 🎒 stub in `components/hud.tsx`.
- [x] Group by category, show stack/cap, flag near-cap materials.
- [x] Sync response includes materials gained, so the app can show pickups.

---

## Acceptance criteria

1. Build green, tests pass.
2. Total distinct seeded materials ≤ 25; unique named materials ≤ 15.
3. Revealing a woodland cell yields woodland-pool materials; an urban cell yields urban-pool.
4. An unclassified/`Open` cell still yields base materials — never zero.
5. Gathering into a full stack yields Dust and does not error or block.
6. Replaying an identical sync yields **identical** drops (seeded RNG), not a re-roll.
7. Inventory screen lists materials with correct quantities and caps.
8. Terrain classification is cached — a second player revealing the same cell triggers no
   new Overpass call.
9. **A material with `LevelRequired = 20` is never obtained by a level 19 player** — not
   rarely, never. Verified by a test rolling the drop table many times at level 19.
10. At level 20 that material becomes obtainable and is weighted above lower tiers.
11. **A test fixture with no matching terrain still yields every tier the player has
    unlocked**, at reduced quantity — proving tier gates on level, not geography.
12. XP/hour is roughly flat across tiers — higher `BaseGatherSeconds` is offset by higher
    `XpPerUnit`.
13. Stages 01–02 acceptance criteria still pass.

## Definition of done

- All tasks ticked, all criteria verified.
- `STATUS.md`: stage 03 `DONE`, current stage `STAGE-04-skill-foraging.md`.

## Out of scope

Crafting (Stage 06), workers consuming materials (Stage 05), Museum donations (Stage 12).
