# Stage 04 — Foraging

> The first playable skill, end to end. **This stage is the template for every later skill.**

## Status

- **State:** NOT STARTED
- **Completed:** _(none)_
- **Remaining:** all tasks
- **Notes:** _(none)_
- **Blockers:** _(none)_

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

- [ ] `SkillDefinition` **seeded**: `SkillType, Name, Description, Icon, UnlockLevel,
      Category (Gathering|Production|Social)`.
- [ ] `SkillTerrainMapping` **seeded**: `SkillType, Terrain, XpPerCell, YieldMultiplier`.
- [ ] Terrain is a **multiplier, never a gate** (§5.2). A skill with no matching terrain
      nearby still trains at base rate.
- [ ] On cell reveal, grant XP to every skill whose terrain mapping matches — via
      `IProgressionService` from Stage 02.
- [ ] Generic POI-visit training: a POI's skill gets XP at the POI rate. No per-skill code.

### 2. POI visit endpoint

Does not exist yet. Design it as a **session, not a one-shot** (`DESIGN.md` §3.1d) so POI
minigames can grow into it later without a rewrite.

- [ ] `POST /api/journey/poi/{id}/visit` — validates range **server-side** against the
      player's last verified position. Never trust a client-supplied position.
- [ ] Returns a session token and the outcome (XP, materials, first-visit flag).
- [ ] Apply visit decay (§3.4): `decay(n) = max(0.05, 1/(1 + 0.5n))`, one charge regained
      per 24h.
- [ ] `PlayerPoiVisit`: `PlayerId, PoiId, VisitCount, LastVisitUtc, FirstVisitUtc`. Stage 12
      (Museum) and Stage 14 (Expeditions) both depend on this log — get it right now.
- [ ] Apply the same anti-cheat as sync (§7.2) — clue and POI arrivals must not be a
      spoofing vector.

### 3. Foraging content (seed data only)

- [ ] Ladder entry: unlocked at Adventurer level 1, with Exploration.
- [ ] Terrain mappings: `Woodland` and `Farmland` best; `Open`/`Urban` base rate; everything
      trains *something*.
- [ ] Verify `PoiImportService.TagMappings` covers foraging-ish POIs (`leisure=garden`,
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

- [ ] Seed all seven with `LevelRequired`, `BaseGatherSeconds`, `XpPerUnit`.
- [ ] Drop tables weighted to the highest unlocked tier, falling back to lower tiers.
- [ ] **Tier gates on level; terrain gates on speed.** A player with no woodland still
      reaches Everleaf at level 90 — it just takes longer. Never put a tier behind terrain.

### 4. App

- [ ] Foraging shows in the skills screen with progress and level.
- [ ] Materials show in inventory.
- [ ] POI markers show whether in range; tapping an in-range POI calls the visit endpoint.
- [ ] `components/poiDetailModal.tsx` is currently read-only — add the visit action, the
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
