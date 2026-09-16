# Stage 03 — Materials & inventory

> Things you can hold. Terrain classification, tiered material pools, stack caps, inventory.

## Status

- **State:** NOT STARTED
- **Completed:** _(none)_
- **Remaining:** all tasks
- **Notes:** _(none)_
- **Blockers:** _(none)_

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

- [ ] `Material`: `Id, Key, Name, Tier, Category, SkillType?, StackCap, IsUnique,
      LevelRequired, BaseGatherSeconds, XpPerUnit`.
- [ ] `PlayerMaterial`: `PlayerId, MaterialId, Quantity`, unique `(PlayerId, MaterialId)`.
- [ ] **Shared tiered pools.** Most POIs and terrain draw from pools by category and tier
      (`CommonUrban`, `RareIndustrial`, `CommonWoodland`…), *not* a unique item each.
- [ ] Reserve `IsUnique` for **10–15 named materials** total, attached to the rarest and most
      memorable POI types. Exclusivity means nothing if everything is exclusive.
- [ ] Seed the initial pools — keep total distinct materials under ~25 at this stage.

### 1a. Material tiers (RuneScape pattern)

Read `DESIGN.md` §4.1a before implementing. This is the core of how gathering skills feel
like progression, and getting it wrong is expensive to unpick later.

- [ ] **`LevelRequired` gates access absolutely.** Below the level, the material cannot be
      obtained at all — not rarely, not slowly. Not obtainable.
- [ ] **`BaseGatherSeconds` rises with tier.** Higher tiers take longer per unit.
- [ ] **`XpPerUnit` rises proportionally**, so XP/hour stays roughly flat across tiers. A
      player must never be punished for advancing to a higher tier.
- [ ] Roughly one new tier per 10–15 skill levels, so a next unlock is always in view.
- [ ] Drop rolls select the **highest tier the player has unlocked**, with weighted fallback
      to lower tiers.

- [ ] **THE CRITICAL RULE — tier gates on skill level, terrain gates on speed.**

      > *What* you can obtain depends **only** on skill level. *How fast* depends on terrain,
      > POIs, gear and tools. Do **not** put high tiers behind rare terrain — that silently
      > rebuilds the geographic lockout the unlock ladder exists to prevent, and a player in
      > a flat suburb would cap at tier 2 forever. A landlocked player at Mining 50 gathers
      > Gold Ore; it just takes them longer than someone living by a quarry.

- [ ] Gather time scales the **quantity yielded per cell** on reveal, rather than blocking a
      walk on a timer. Workers (Stage 05) apply `BaseGatherSeconds` directly, since they work
      in real time.

### 2. Stack caps and overflow

Per §7.4. The rule that matters: **the idle layer must never punish you for sleeping.**

- [ ] Per-material stack cap (from `Material.StackCap`), not a global inventory limit.
- [ ] Overflow **auto-converts** to a universal currency (Dust) at a poor rate. Never
      discard, never block.
- [ ] Gathering at cap still succeeds — it just yields Dust instead. Nothing hard-stalls.

### 3. Cell terrain classification

- [ ] `RevealedCell.Terrain` (flags/bitmask — a cell can be both wooded and watered).
- [ ] Classify at reveal time from OSM data. The Overpass import in `PoiImportService`
      already fetches the area; extend the query to pull landuse/natural/water polygons per
      import cell rather than adding a second import path.
- [ ] Terrain types: `Woodland, Water, Farmland, Urban, Industrial, Rocky, Coastal, Open`.
- [ ] Cache per grid cell — classify once, reuse for every player.
- [ ] Unclassified cells default to `Open` and still yield base materials. **A cell must
      never yield nothing** — that would reintroduce a geographic dead zone.

### 4. Drops

- [ ] `DropTable` / `DropTableEntry`, **seeded**: material, weight, min/max quantity,
      required level.
- [ ] Revealing a cell rolls its terrain drop table.
- [ ] Seeded RNG per (player, cell) so a replayed sync cannot re-roll for a better result.
- [ ] Drop rates scale with the relevant skill level once skills gather (Stage 04).

### 5. Inventory API and UI

- [ ] `GET /api/player/inventory`.
- [ ] Inventory screen; wire up the 🎒 stub in `components/hud.tsx`.
- [ ] Group by category, show stack/cap, flag near-cap materials.
- [ ] Sync response includes materials gained, so the app can show pickups.

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
