# Stage 02 — Progression core

> Two XP tracks, the skills table, the unlock ladder, and bonus points. The spine every
> later stage hangs off.

## Status

- **State:** BLOCKED
- **Completed:** _(none)_
- **Remaining:** all tasks
- **Notes:**
  - Stage 01 is `DONE`, so the stated prerequisite is met: the RS curve is in place at
    `GeoSlayer.Domain/Services/Progression/XpCurve.cs`.
  - The blocker is `DESIGN.md`. It has **never existed in this repository** — no file on
    disk, and `git log --diff-filter=D` finds no commit that deleted one. The stage
    files reference it throughout.
- **Blockers:**
  - **`DESIGN.md` is missing, and this stage cannot be written without it.**

    Stage 01 survived the same gap because the one thing it needed from `DESIGN.md`
    (§3.2, the XP curve) was pinned exactly by the three known values the task listed —
    they identify the standard RuneScape curve and nothing else. That is not true here.
    This stage needs *content*, not a formula, and the stage file gives defaults for
    only some of it:

    - §3.0 — how Adventurer XP relates to skill XP beyond `GLOBAL_XP_RATIO` (0.25 given).
    - §3.0b — the milestone grant values: new cell, first POI visit, new region, craft
      complete, skill unlock. **No values given anywhere.**
    - §3.1 — the unlock ladder: which skills and systems unlock at which Adventurer
      level. Only level 1 is specified (Exploration + Foraging). **The rest of the
      ladder is the substance of task 3 and is unspecified.**
    - §3.1c — the roadmap/celebration UX.
    - §3.3 — the idle XP rules ("0.25x the normal cut" is given, the surrounding rules
      are not).
    - §3.0a — bonus points: the four upgrades are named, but `CostCurve`,
      `EffectPerRank` and `MinAdventurerLevel` are not.

    Guessing these would be worse than stopping. The README's own rule is that game data
    is seeded so it can be retuned without a deploy — but seeding *invented* numbers
    still bakes invented game design into migrations, and acceptance criteria 3, 5, 6
    and 7 all assert behaviour against values that only `DESIGN.md` defines.

    **To unblock:** add `DESIGN.md` at the repository root with at least §3.0, §3.0a,
    §3.0b, §3.1, §3.1c and §3.3. Alternatively, state the missing values directly in
    this stage file and remove the `DESIGN.md` references.

## Prerequisites

Stage 01 `DONE`. In particular the XP curve must already be the RS curve.

## Goal

Adventurer XP exists as its own pool, skills exist as rows, levelling grants bonus points,
and the unlock ladder is seeded data. No individual skill is *playable* yet (that starts at
Stage 04) — this stage builds the machinery they all plug into.

Read `DESIGN.md` §3.0 through §3.3 before starting. The reasoning there matters; several
decisions look arbitrary without it.

---

## Tasks

### 1. Adventurer XP as its own pool

Per `DESIGN.md` §3.0. **Reuse the existing `Player.Xp` / `Player.Level` columns** — rename
rather than adding new state.

- [ ] Rename `Player.Xp` → `AdventurerXp` (long), `Player.Level` → `AdventurerLevel`.
- [ ] Migration includes a one-off rescale of existing values onto the new curve.
- [ ] Central `IProgressionService.GrantXp(playerId, skill?, amount, source)` — **every** XP
      grant in the codebase goes through it. No exceptions; later stages depend on this being
      the only path.
- [ ] Dual payout: skill XP (if a skill is given) **plus**
      `floor(amount * GLOBAL_XP_RATIO)` Adventurer XP.
- [ ] `GLOBAL_XP_RATIO` in seeded config (default 0.25). Idle sources use a reduced ratio
      (default 0.25× the normal cut) — see §3.3.
- [ ] Milestone grants (§3.0b): new cell, first visit to a POI, new region, craft complete,
      skill unlock. Seeded values.

### 2. Skills

- [ ] `PlayerSkill` table: `PlayerId, SkillType, Xp, Level, UnlockedAtUtc`, unique
      `(PlayerId, SkillType)`. **Row exists ⟺ unlocked** — no level-0 state.
- [ ] Add `Foraging` to the `SkillType` enum.
- [ ] Skill levels use the same RS curve as Adventurer, undivided.
- [ ] `GET /api/player/skills` returns unlocked skills plus the locked ladder (name + unlock
      level — the roadmap, §3.1c).

### 3. Unlock ladder

- [ ] `UnlockDefinition` table, **seeded**: `AdventurerLevel, UnlockType (Skill|System),
      Payload`.
- [ ] Seed the ladder from `DESIGN.md` §3.1. Level 1 grants **Exploration and Foraging**
      (the cold-start fix — both, not just Exploration).
- [ ] On Adventurer level-up, apply any unlocks transactionally with the level change.
- [ ] Unlock events returned in the sync response so the app can celebrate them.

### 4. Bonus points

Per `DESIGN.md` §3.0a.

- [ ] `Player.BonusPointsEarned` / `BonusPointsSpent`.
- [ ] `PlayerUpgrade` table: `PlayerId, UpgradeKey, Rank`, unique `(PlayerId, UpgradeKey)`.
- [ ] `UpgradeDefinition` **seeded**: `Key, Name, Category, MaxRank, CostCurve,
      EffectPerRank, MinAdventurerLevel`.
- [ ] One point per Adventurer level.
- [ ] **Ship a deliberately small tree** — four upgrades only: Worker Slot, Reveal Radius,
      Scholar (+5% skill XP), Offline Cap. Four balanced beats fifteen unbalanced. Later
      stages add more.
- [ ] `POST /api/player/upgrades/{key}/purchase` — validates points, rank cap, min level.
- [ ] **Respec** from day one: `POST /api/player/upgrades/respec`, refunds all points for a
      cost. Players will mis-invest while learning; a permanently wrong build is churn.
- [ ] Upgrade effects must be **read by the systems they affect** — Reveal Radius changes the
      actual reveal, Scholar changes actual XP. An upgrade that displays but does nothing is
      worse than no upgrade.

### 5. App

- [ ] Skills screen (wire up the ✨ stub in `components/hud.tsx`). Unlocked skills with
      progress; locked ones **named and greyed with their unlock level** — the ladder is a
      roadmap and should be visible.
- [ ] Upgrade screen: points available, the four upgrades, purchase, respec.
- [ ] Unlock celebration — full screen, not a toast (§3.1c).
- [ ] HUD shows Adventurer level and XP. Remove the hardcoded `hp={85}` / `gold={0}`; show
      real values or omit those elements until the systems exist.

---

## Acceptance criteria

1. Build green, tests pass.
2. Every XP grant flows through `IProgressionService` — verified by grep; no direct
   `player.Xp +=` outside it.
3. Granting skill XP raises both the skill and Adventurer XP at the configured ratio.
4. A new account starts with Exploration **and** Foraging unlocked, and no others.
5. Reaching an unlock threshold grants the skill and returns an unlock event.
6. Each Adventurer level grants exactly one bonus point.
7. Purchasing Reveal Radius **measurably changes** the cells revealed by an identical
   synthetic path.
8. Respec refunds every spent point and clears purchased ranks.
9. Skills screen shows unlocked skills and the locked ladder with unlock levels.
10. Nothing from Stage 01 regressed — re-run its acceptance criteria.

## Definition of done

- All tasks ticked, all criteria verified.
- `STATUS.md`: stage 02 `DONE`, current stage `STAGE-03-materials-inventory.md`.

## Out of scope

Actual skill *content* — no materials, no gathering, no terrain training yet. Stage 03 adds
materials; Stage 04 makes Foraging playable. This stage is machinery only.
