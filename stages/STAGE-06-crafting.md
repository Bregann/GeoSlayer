# Stage 06 — Crafting

> The material sink. Timed recipes, gear, tools, buildings.

## Status

- **State:** NOT STARTED
- **Completed:** _(none)_
- **Remaining:** all tasks
- **Notes:** _(none)_
- **Blockers:** _(none)_

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

- [ ] `Recipe` **seeded from JSON**: `Key, Name, SkillType, LevelRequired, DurationSeconds,
      XpReward, OutputMaterialId?, OutputItemId?`.
- [ ] `RecipeInput`: `RecipeId, MaterialId, Quantity`.
- [ ] Data-driven. You will retune constantly and must not need a deploy to do it.
- [ ] Double gate: skill must be **unlocked** (ladder) *and* at `LevelRequired`.
- [ ] Locked recipes show greyed with the requirement visible; recipes gated on **exclusive
      POI materials** are flagged as *travel-gated*, visually distinct from *level-gated*, so
      the player knows the difference between "keep playing" and "go somewhere new".

### 2. Craft queue

- [ ] `PlayerCraft`: `Id, PlayerId, RecipeKey, StartedUtc, CompletesUtc, Collected`.
- [ ] Inputs consumed at queue time, not completion — prevents queue-then-spend exploits.
- [ ] Completion evaluated **lazily** on next sync, same as workers (§5.3). No recurring job.
- [ ] Queue length limited by bonus-point upgrade; seed a `CraftSlot` upgrade.

### 3. Items and gear

- [ ] `Item` / `PlayerItem`, with modifiers: `+% skill XP`, `+1 reveal radius`,
      `+m POI range`, `+h offline cap`.
- [ ] Equipment slots; modifiers read by the systems they affect — an equipped item that
      changes no behaviour is a bug.
- [ ] **Gear must not duplicate bonus points** (§4.3). Points are permanent, chosen, earned
      by playing at all. Gear is crafted, swappable, geography-gated. Keep gear **larger but
      conditional** — situational, slot-competing or consumable. If a gear item and an
      upgrade both grant a flat `+10%`, one is redundant.
- [ ] **Tools** gate access to higher-tier gathering: a pickaxe for better Mining nodes, a
      rod for better Fishing. This is what gives crafting a purpose beyond stat creep.
- [ ] **Buildings** placed on Claims: raise stack caps, raise offline cap, boost worker
      rates.

### 4. App

- [ ] Crafting screen: recipe list with filters, inputs vs. held, queue with timers.
- [ ] Equipment screen.
- [ ] Notify on craft completion.

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
