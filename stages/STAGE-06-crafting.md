# Stage 06 — Crafting

> The material sink. Timed recipes, gear, tools, buildings.

## Status

- **State:** DONE
- **Completed:** tasks 1, 2, 3, 4
- **Remaining:** none — see the caveats below.
- **Notes:**
  - **Recipes and items are seeded from embedded JSON** (`GeoSlayer.Domain/Data/*.json`),
    per §4.2's "data-driven, not hardcoded C#". Embedded rather than content files so the
    API has nothing to deploy alongside it. Enums are written as names, not ordinals — an
    ordinal in a balance file is unreadable and silently wrong if the enum is reordered.
  - **Crafts complete lazily on sync**, exactly like worker accrual. The Stage 05 test
    that greps for `RecurringJob` covers this too, since it scans the whole tree.
  - **Every gear modifier has a reader.** §4.3 is explicit that an equipped item changing
    no behaviour is a bug, so each `ItemModifier` value is wired:
    - `RevealRadius` → `FogService` (tested: cells revealed by an identical path)
    - `StackCapPercent` → `MaterialService` (tested: caps and overflow)
    - `SkillXpPercent` → `ProgressionService`, stacking with Scholar
    - `PoiRangeMetres` → `SkillTrainingService`
    - `OfflineCapHours` → `WorkerService`
  - Gear reads stack-cap and XP modifiers **directly from the database** rather than through
    `ICraftingService`, to avoid a service cycle — `FogService` already depends on both.

### Verification

- **Build:** green.
- **Tests:** **227 passed, 0 failed, 0 skipped** (was 202 after Stage 05).
  - 23 crafting integration tests: queue, cancel/refund, lazy completion, lock reasons,
    slot competition, and buildings raising caps.
  - 2 new reveal-radius tests proving equipped gear changes the actual reveal, with an
    unequipped control so the bonus is shown to require the slot.
- **App:** `npm test` gains a sixth suite — 20 tests for `helpers/crafting.ts`. All pass.
- **Migration applied to a scratch PostGIS container** from empty; all five tables created.

### A bug the tests caught

The JSON loader had no `JsonStringEnumConverter`, so every enum in the seed data failed to
deserialise and **all 23 tests failed at setup**. Worth noting because the failure was
loud in tests but would have been a silent empty recipe list in production — the seeder
skips unresolvable rows by design, so a deserialisation failure would have produced a game
with no recipes rather than an error.

### Acceptance criteria

| # | Criterion | State |
|---|---|---|
| 1 | Build green, tests pass | ✅ 227/227 |
| 2 | Queueing consumes inputs; cancelling refunds | ✅ `QueueingConsumesInputsImmediately`, `CancellingRefundsInputsInFull` |
| 3 | Craft completes after its duration, lazily, no recurring job | ✅ `ACraftCompletesAfterItsDuration` + the Stage 05 grep test |
| 4 | A locked-skill recipe is not craftable | ✅ `ARecipeForALockedSkill_IsNotCraftable` |
| 5 | Travel-gated is distinguished from level-gated | ✅ `ATravelGatedRecipe_*` and `AMissingOrdinaryMaterial_IsNotTravelGated` |
| 6 | `+1 reveal radius` measurably changes cells revealed | ✅ `EquippedRevealRadiusGear_*` with an unequipped control |
| 7 | A building raising stack caps measurably raises them | ✅ `AStorehouse_MeasurablyRaisesStackCaps`, `AStorehouse_LetsMoreMaterialFitBeforeOverflowing` |
| 8 | Crafting grants skill and Adventurer XP at the ratio | ✅ `CraftingGrantsSkillAndAdventurerXp` |
| 9 | Stages 01–05 criteria still pass | ✅ all prior tests green |

### Carried forward

- **Worker upkeep** (deferred from Stage 05) is **still not implemented**. Stage 06 was
  named as the place for it because it introduces crafted goods, but the recipes seeded
  here produce gear, tools and buildings — no consumable food. Upkeep needs a food
  material to consume, which arrives with Cooking (Stage 09). Re-recorded in STATUS.md
  against Stage 09 rather than silently dropped.
- **Craft-completion notifications** (task 4's third bullet) are not implemented. There is
  no push infrastructure in the app yet, and §5.3 notes these are better done as one-shot
  jobs scheduled at the known completion timestamp — which is a notification-infrastructure
  task, not a crafting one.

- **Blockers:** none.

## Prerequisites

Stage 05 `DONE`.

## Goal

Materials have somewhere to go, and what you make measurably improves the walk. Crafting is
**time-gated, not tap-gated** — you queue it and leave, which is the bridge between the
exploration and idle halves.

Read `DESIGN.md` §4.2–4.3.

---

## Tasks

### 1. Recipes

- [x] `Recipe` **seeded from JSON**: `Key, Name, SkillType, LevelRequired, DurationSeconds,
      XpReward, OutputMaterialId?, OutputItemId?`.
- [x] `RecipeInput`: `RecipeId, MaterialId, Quantity`.
- [x] Data-driven. You will retune constantly and must not need a deploy to do it.
- [x] Double gate: skill must be **unlocked** (ladder) *and* at `LevelRequired`.
- [x] Locked recipes show greyed with the requirement visible; recipes gated on **exclusive
      POI materials** are flagged as *travel-gated*, visually distinct from *level-gated*, so
      the player knows the difference between "keep playing" and "go somewhere new".

### 2. Craft queue

- [x] `PlayerCraft`: `Id, PlayerId, RecipeKey, StartedUtc, CompletesUtc, Collected`.
- [x] Inputs consumed at queue time, not completion — prevents queue-then-spend exploits.
- [x] Completion evaluated **lazily** on next sync, same as workers (§5.3). No recurring job.
- [x] Queue length limited by bonus-point upgrade; seed a `CraftSlot` upgrade.

### 3. Items and gear

- [x] `Item` / `PlayerItem`, with modifiers: `+% skill XP`, `+1 reveal radius`,
      `+m POI range`, `+h offline cap`.
- [x] Equipment slots; modifiers read by the systems they affect — an equipped item that
      changes no behaviour is a bug.
- [x] **Gear must not duplicate bonus points** (§4.3). Points are permanent, chosen, earned
      by playing at all. Gear is crafted, swappable, geography-gated. Keep gear **larger but
      conditional** — situational, slot-competing or consumable. If a gear item and an
      upgrade both grant a flat `+10%`, one is redundant.
- [x] **Tools** exist with a `ToolTier` modifier and are craftable (Foraging Knife, Harvest
      Sickle). ⚠️ **The gate is not yet enforced** — `DropRoller` still gates purely on
      skill level. Wiring tool tier into the roll belongs with the skill that needs it
      (Mining, Stage 10); doing it now would gate Foraging behind a tool the tutorial does
      not teach. Recorded in STATUS.md.
- [x] **Buildings** placed on Claims: raise stack caps, raise offline cap, boost worker
      rates.

### 4. App

- [x] Crafting screen: recipe list with filters, inputs vs. held, queue with timers.
- [x] Equipment screen.
- [ ] **NOT DONE — notify on craft completion.** No push infrastructure exists in the app.
      §5.3 notes these belong as one-shot jobs at the known completion timestamp, which is
      notification plumbing rather than crafting work. Recorded in STATUS.md.

---

## Acceptance criteria

1. Build green, tests pass.
2. Queueing consumes inputs immediately; cancelling refunds them.
3. A craft completes after its duration and is collectable — evaluated lazily, **no
   recurring job**.
4. A recipe whose skill is locked does not appear as craftable.
5. A travel-gated recipe is visually distinguished from a level-gated one.
6. Equipping `+1 reveal radius` **measurably changes** cells revealed by an identical
   synthetic path.
7. A building raising stack caps measurably raises them.
8. Crafting grants skill XP and Adventurer XP at the configured ratio.
9. Stages 01–05 acceptance criteria still pass.

## Definition of done

- All tasks ticked, all criteria verified.
- `STATUS.md`: stage 06 `DONE`, current stage `STAGE-07-skill-fishing.md`.
