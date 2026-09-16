# Stage 02 — Progression core

> Two XP tracks, the skills table, the unlock ladder, and bonus points. The spine every
> later stage hangs off.

## Status

- **State:** IN PROGRESS — backend complete (tasks 1–4), app pending (task 5)
- **Completed:** tasks 1, 2, 3, 4
- **Remaining:** task 5 (app screens)
- **Notes:**
  - **The `DESIGN.md` blocker is resolved.** The file now exists at the repo root (1182
    lines, added in `277e40e`) and covers §3.0, §3.0a, §3.0b, §3.1, §3.1c and §3.3. The
    earlier note that it "has never existed" was written before that commit and is stale.
  - §3.1 pins the unlock ladder concretely (levels 1/3/5/8/12/16/20/25 with the skills and
    systems at each), so task 3 needed no invention — it is transcribed in
    `ProgressionSeedData.Ladder`.
  - Two things §3.0a/§3.0b give only qualitatively were chosen as **tuning values**, which
    is what DESIGN.md asks for ("seeded data, not code… this tree will be retuned
    constantly"; "the numbers are for tuning, the structure is the point"). Each is
    documented with its reasoning in `ProgressionDefaults.cs`:
    - **Milestone XP** (§3.0b says "small, flat" / "moderate" / "large"): anchored to the
      one rate §3.3 does pin — a revealed cell is 2 skill XP — giving 1 / 20 / 50 / 100
      while preserving §3.0b's stated ordering.
    - **Upgrade cost curves** (§3.0a's table is "indicative"): Worker Slot uses the
      `1,2,4,7,11` curve §3.0a gives verbatim; the others follow its stated shape rules
      (Reveal Radius "high cost, few ranks"; the percentage upgrades cheap with many ranks).
  - Effects, the four upgrade choices and the ladder are all taken from DESIGN.md directly.

### Verification

- **Build:** green (`dotnet build GeoSlayer.sln`).
- **Tests:** **99 passed, 0 failed, 0 skipped** (was 76 before this stage).
  - Note: the 12 Stage 01 database tests had been *silently skipping*. `TestContainerSetup`
    was a `[SetUpFixture]` in namespace `GeoSlayer.Tests.Infrastructure`, but NUnit scopes
    a setup fixture to its own namespace and descendants — the tests live in the sibling
    `GeoSlayer.Tests.Services.*`, so `OneTimeSetUp` never ran and the container never
    started. Moved to the root `GeoSlayer.Tests` namespace; they now genuinely execute.
    **This clears the highest-value item in the STATUS.md manual queue.**
- **Migration verified against real legacy data**, not just a fresh schema: applied the
  Stage 01 schema to a scratch PostGIS container, inserted a player with `Xp=4000, Level=10`,
  then applied `Stage02ProgressionCore`. Result: `AdventurerXp` rescaled to 1000 (the 0.25
  cut), Exploration skill row created holding the full original 4000, and both cached levels
  recomputed to 9 and 19 — matching the C# `XpCurve` for those totals exactly.
  - EF's scaffolder had guessed the rename as `Level` → `RespecCount`, which would have
    moved every player's level into the respec counter and left `AdventurerLevel` at 0.
    Corrected to drop the derived column and recompute it in SQL.

### Acceptance criteria

| # | Criterion | State |
|---|---|---|
| 1 | Build green, tests pass | ✅ 99/99 |
| 2 | Every XP grant flows through `IProgressionService` | ✅ verified by grep — no direct XP mutation outside it |
| 3 | Skill XP raises both pools at the configured ratio | ✅ `GrantXp_RaisesBothTheSkillAndAdventurerXp` |
| 4 | New account starts with Exploration **and** Foraging only | ✅ `NewAccount_StartsWithExplorationAndForagingOnly` |
| 5 | Unlock threshold grants the skill and returns an event | ✅ `ReachingLevel3_UnlocksFishingAndReturnsAnEvent` |
| 6 | Each Adventurer level grants exactly one bonus point | ✅ `EachAdventurerLevel_GrantsExactlyOneBonusPoint` |
| 7 | Reveal Radius measurably changes cells revealed | ✅ `RevealRadiusUpgradeTests` (with a control test) |
| 8 | Respec refunds every point and clears ranks | ✅ `Respec_RefundsEveryPointAndClearsRanks` |
| 9 | Skills screen shows unlocked + locked ladder | ⚠️ API verified (`GetSkills_*`); **screen is task 5** |
| 10 | Nothing from Stage 01 regressed | ✅ all Stage 01 tests pass, now actually executing |

- **Blockers:** none.

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

- [x] Rename `Player.Xp` → `AdventurerXp` (long), `Player.Level` → `AdventurerLevel`.
- [x] Migration includes a one-off rescale of existing values onto the new curve.
- [x] Central `IProgressionService.GrantXp(playerId, skill?, amount, source)` — **every** XP
      grant in the codebase goes through it. No exceptions; later stages depend on this being
      the only path.
- [x] Dual payout: skill XP (if a skill is given) **plus**
      `floor(amount * GLOBAL_XP_RATIO)` Adventurer XP.
- [x] `GLOBAL_XP_RATIO` in seeded config (default 0.25). Idle sources use a reduced ratio
      (default 0.25× the normal cut) — see §3.3.
- [x] Milestone grants (§3.0b): new cell, first visit to a POI, new region, craft complete,
      skill unlock. Seeded values.

### 2. Skills

- [x] `PlayerSkill` table: `PlayerId, SkillType, Xp, Level, UnlockedAtUtc`, unique
      `(PlayerId, SkillType)`. **Row exists ⟺ unlocked** — no level-0 state.
- [x] Add `Foraging` to the `SkillType` enum.
- [x] Skill levels use the same RS curve as Adventurer, undivided.
- [x] `GET /api/player/skills` returns unlocked skills plus the locked ladder (name + unlock
      level — the roadmap, §3.1c).

### 3. Unlock ladder

- [x] `UnlockDefinition` table, **seeded**: `AdventurerLevel, UnlockType (Skill|System),
      Payload`.
- [x] Seed the ladder from `DESIGN.md` §3.1. Level 1 grants **Exploration and Foraging**
      (the cold-start fix — both, not just Exploration).
- [x] On Adventurer level-up, apply any unlocks transactionally with the level change.
- [x] Unlock events returned in the sync response so the app can celebrate them.

### 4. Bonus points

Per `DESIGN.md` §3.0a.

- [x] `Player.BonusPointsEarned` / `BonusPointsSpent`.
- [x] `PlayerUpgrade` table: `PlayerId, UpgradeKey, Rank`, unique `(PlayerId, UpgradeKey)`.
- [x] `UpgradeDefinition` **seeded**: `Key, Name, Category, MaxRank, CostCurve,
      EffectPerRank, MinAdventurerLevel`.
- [x] One point per Adventurer level.
- [x] **Ship a deliberately small tree** — four upgrades only: Worker Slot, Reveal Radius,
      Scholar (+5% skill XP), Offline Cap. Four balanced beats fifteen unbalanced. Later
      stages add more.
- [x] `POST /api/player/upgrades/{key}/purchase` — validates points, rank cap, min level.
- [x] **Respec** from day one: `POST /api/player/upgrades/respec`, refunds all points for a
      cost. Players will mis-invest while learning; a permanently wrong build is churn.
- [x] Upgrade effects must be **read by the systems they affect** — Reveal Radius changes the
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
