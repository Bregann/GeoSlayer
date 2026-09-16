# GeoSlayer build stages

Machine-readable build plan. [`../DESIGN.md`](../DESIGN.md) is the vision; this directory is
the execution order.

## How to use this (agent instructions)

You are building GeoSlayer one stage at a time. Each loop:

1. **Read `STATUS.md`** — it names the current stage. Trust it over your own memory.
2. **Open that stage file.** Read it fully before writing code.
3. **Check `## Status`** in the stage file for what's already done.
4. **Do the next unchecked task.** One task, not the whole stage.
5. **Verify it** against `## Acceptance criteria`. Run the build. Run the tests.
6. **Update the stage file's `## Status` block** — tick the task, add notes, record blockers.
7. **If every task is ticked and all acceptance criteria pass**, mark the stage `DONE`,
   update `STATUS.md` to the next stage, and stop.

### Rules

- **Never skip ahead.** A stage depends on its predecessors. If a prerequisite is unmet, say
  so in the status block and stop rather than working around it.
- **Never mark done what you haven't verified.** "Build succeeds" is not "acceptance criteria
  pass". If you cannot verify something (needs a physical walk, needs a device), mark it
  `NEEDS MANUAL VERIFICATION` and say exactly what a human must do.
- **Leave the repo building.** Never end a loop with a broken build. If you must stop
  mid-task, revert to the last working state and note where you got to.
- **Write the tests named in the stage file.** They are part of the task, not optional extra.
- **Update `## Status` before you stop**, even on failure. The next loop depends on it being
  accurate.
- **Ask nothing.** Where a stage leaves a decision open it gives a default — take it and note
  the choice. If genuinely blocked, record the blocker in the status block and stop.

### Conventions

- Backend: .NET 10, EF Core, PostGIS. Services in `GeoSlayer.Domain/Services/`, interfaces in
  `Interfaces/Api/`, registered in `Program.cs`.
- App: Expo / React Native, expo-router, MapLibre. Path alias `@/`.
- **Game data is seeded, never hardcoded.** Curves, unlock tables, recipes, drop tables, clue
  templates all live in seedable config. This is a hard rule — the whole plan assumes
  retuning without a deploy.
- Migrations: `dotnet ef migrations add <Name> -p GeoSlayer.Domain -s GeoSlayer`.

## Stage list

| # | Stage | Depends on | Summary |
|---|---|---|---|
| 01 | [Prototype](STAGE-01-prototype.md) | — | Fix the existing loop. Correct, secure, honest map-painter. |
| 02 | [Progression core](STAGE-02-progression-core.md) | 01 | Adventurer XP, skills table, bonus points, unlock ladder. |
| 03 | [Materials & inventory](STAGE-03-materials-inventory.md) | 02 | Material schema, terrain tagging, drops, inventory UI. |
| 04 | [Foraging](STAGE-04-skill-foraging.md) | 03 | First skill, end to end. **The template for all later skills.** |
| 05 | [Workers & idle](STAGE-05-workers-idle.md) | 04 | Claims, workers, offline accrual, welcome-back screen. |
| 06 | [Crafting](STAGE-06-crafting.md) | 05 | Recipes, timed crafts, gear. |
| 07 | [Fishing](STAGE-07-skill-fishing.md) | 06 | Second gathering skill. Follows the Stage 04 template. |
| 08 | [Woodcutting](STAGE-08-skill-woodcutting.md) | 07 | Third gathering skill. |
| 09 | [Cooking](STAGE-09-skill-cooking.md) | 08 | First production skill. Consumes Fishing/Foraging output. |
| 10 | [Mining](STAGE-10-skill-mining.md) | 09 | Rare-terrain gathering. |
| 11 | [Smithing](STAGE-11-skill-smithing.md) | 10 | Production from Mining. |
| 12 | [Museum](STAGE-12-museum.md) | 11 | Collection log. Wings, entries, donations. |
| 13 | [Clue scrolls](STAGE-13-clue-scrolls.md) | 12 | Directed exploration. Generated cryptic clues. |
| 14 | [Retention systems](STAGE-14-retention.md) | 13 | Expeditions, patrols, transit, districts, surges. |
| 15 | [Remaining skills](STAGE-15-remaining-skills.md) | 14 | Farming, Trading, and the POI-flavoured tier. |

Stages 01–05 are the critical path to something playable. **Do not ship to real players
before 05** — see the cold-start note in `DESIGN.md` §3.1.

## Adding a skill

Stages 07+ follow a repeating pattern documented in
[`SKILL-TEMPLATE.md`](SKILL-TEMPLATE.md). Stage 04 (Foraging) is the worked example — read
it before starting any later skill stage.
