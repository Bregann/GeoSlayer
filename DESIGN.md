# GeoSlayer — Core Loop Design

Status: proposal, not yet built. Written against the codebase as of the RevealedCells
grid model (streets removed in `20260504154606_AddPlayerAntiCheatFields`).

---

## 1. What the game is

An **idle/exploration RPG played on the real world**. You walk; the map reveals; your
skills level; your gatherers keep working while the app is closed; you craft the things
that make the next walk more productive.

The novel asset is already built: ~80 OSM tag→skill mappings that turn real places into
skill trainers. A church is a Prayer altar. A quarry is a Mining node. That mapping is
the game's identity and everything below is built to exploit it.

Three design commitments, in priority order:

1. **Walking is the only true resource.** Everything else is downstream of distance
   covered and places visited. Idle accrual must never outpace walking, or the walking
   stops.
2. **The phone lives in the pocket.** The primary play posture is screen-off, walking.
   Anything requiring sustained attention on the map is secondary.
3. **Infinite progression, no cap.** Exponential curves, prestige layers, and content
   that scales with the player rather than running out.

### Design pillars in tension

Worth naming the central risk up front: **idle and exploration pull against each other.**
A pure idle game rewards not playing. A pure exploration game punishes you for having a
job. The resolution used throughout this doc is that **idle systems can only process what
walking has gathered** — Claims generate from territory you revealed, Workers consume
materials you collected, offline progress is capped by structures you built by visiting
real POIs. Idle is the *refinery*; walking is the *mine*. If a system ever lets you
progress meaningfully without leaving home, it's a bug in the design.

---

## 2. The loop

```
        ┌──────────────────────────────────────────────────┐
        │                                                  │
        │                                                  │
   WALK ──→ reveal cells ──→ Exploration XP + materials    │
     │      cross terrain ──→ gathering skill XP           │
     │                                                     │
     ├────→ visit POI ──────→ BIG skill XP boost           │
     │                        + exclusive materials        │
     │                        + (later) POI minigames      │
     │                                                     │
     ├────→ claim territory ─→ worker slots                │
     │                              │                      │
     │                              ↓                      │
   IDLE ├──── workers train skills + gather, offline       │
     │        (the floor — guarantees nobody stalls)       │
     │                                                     │
   CRAFT ──→ gear / tools / buildings ────────────────────→┘
              (increase yield, range, offline cap)

   ── and running alongside all of it ──────────────────────
   every action also pays ADVENTURER XP (reduced rate for idle)
          │
          ├──→ level up ──→ unlocks next skill / system
          └──→ level up ──→ +1 BONUS POINT ──→ spend on upgrades
                                               (workers, radius,
                                                offline cap, yield)
```

Four verbs: **Walk, Visit, Idle, Craft.** Each feeds the next. The loop closes three ways:
crafted items increase walk yield, Adventurer levels unlock new skills to train, and Bonus
Points let the player amplify whichever half of the game they actually play.

The three training routes are deliberately ranked **POI k walk > idle** (§3.3). Idle
guarantees you never stall; walking is the real game; POIs are the spike worth a detour.

---

## 3. Levels, Skills & XP

### 3.0 Two parallel progressions

There are **two** XP tracks, and every action pays into both:

| | **Adventurer Level** (global) | **Skill Levels** (per-skill) |
|---|---|---|
| Pool | One `AdventurerXp` on the player | One `Xp` per unlocked `PlayerSkill` |
| Earned from | A cut of *everything*, plus milestones | The specific skill you trained |
| Drives | **Unlocks** — skills, slots, capacity, features | Yield, rates, recipe access, gear tiers |
| Answers | "How far into the game am I?" | "How good am I at this thing?" |

Chop a tree: Woodcutting XP **and** Adventurer XP. Visit a cathedral: Prayer XP **and**
Adventurer XP. Claim territory: no skill XP, but Adventurer XP.

> **Why global level is its own pool, not the sum of skill levels.** An earlier draft
> derived it as a RuneScape-style total level. Three problems. (1) It couples unlock pacing
> to skill balance — buff Fishing XP and you silently accelerate everyone's unlock ladder,
> so you can never tune one without breaking the other. (2) On an exponential curve, early
> levels are cheap, so fifteen skills at level 10 beats one skill at 99 — players would
> rationally spread thin to farm unlock progress rather than play naturally. (3) Non-skill
> actions like claiming territory can't contribute at all. A dedicated pool fixes all three
> and gives you one clean dial for the whole progression spine.

**Adventurer Level drives unlocks in two ways:**

1. **Automatic unlocks** at threshold levels — the skill ladder (§3.1). These are the
   scripted backbone: everyone gets Fishing at 3. Guarantees pacing and a shared vocabulary
   between players.
2. **Bonus Points** — one per level, spent by the player on upgrades of their choosing
   (§3.0a). This is where builds diverge.

Splitting it this way gets both properties: the skill ladder stays predictable enough to
design content against, while everything *incremental* becomes a player decision rather than
a scripted drip.

Total skill level survives as a **display stat and leaderboard axis** — it's a good flex
number and it rewards breadth. It just no longer drives anything mechanical.

### 3.0a Bonus Points

**Every Adventurer level grants 1 Bonus Point.** Points are spent in a persistent upgrade
menu, never expire, and are the main thing a levelling player actually *engages* with.

```
Player          ... BonusPointsEarned (int), BonusPointsSpent (int)
PlayerUpgrade   PlayerId, UpgradeKey, Rank    -- unique (PlayerId, UpgradeKey)
UpgradeDefinition            -- seeded
  Key, Name, Category, MaxRank, CostCurve, EffectPerRank, MinAdventurerLevel?
```

Indicative upgrade tree — the categories matter more than the specific entries:

| Category | Upgrade | Effect per rank | Notes |
|---|---|---|---|
| **Workers** | Extra Worker Slot | +1 worker | Escalating cost: 1, 2, 4, 7, 11& |
| | Worker Efficiency | +10% worker output | Cheap, many ranks |
| | Worker Upkeep Reduction | −8% upkeep | Matters once you have many |
| **Idle** | Offline Cap | +2h offline accrual | The headline idle upgrade |
| | Second Wind | Workers keep 25% rate past cap | Late, expensive, removes the hard wall |
| **Exploration** | Reveal Radius | +1 cell radius | Very strong; high cost, few ranks |
| | Cartographer | +10% Exploration XP | |
| | POI Range | +10m interact radius | Makes marginal POIs reachable |
| **Territory** | Claim Slot | +1 Claim | Escalating |
| | Surveyor | Claims need fewer revealed cells | |
| **Yield** | Gatherer | +5% all material yield | Broad, incremental |
| | Scholar | +5% all skill XP | The compounding pick |
| | Lucky Find | +2% rare drop chance | |

Design rules for the tree:

- **No dead points.** Every upgrade must be useful to some build. If an upgrade is never
  the right pick, cut it or buff it — a tree where three options are obviously correct is
  just a slower automatic ladder.
- **Escalating costs on the powerful ones.** Worker slots and reveal radius are strong
  enough that rank 5 should cost several levels' worth of points.
- **Some upgrades gated behind a minimum level** (`MinAdventurerLevel`) so the tree reveals
  itself gradually rather than showing forty options at level 2.
- **Respec should exist**, for a material or currency cost. Players will mis-invest early
  while learning the systems, and a permanently wrong build is a churn event. Make it
  cheap the first time, then escalating.
- **Seeded data, not code.** Same reasoning as everywhere else — this tree will be retuned
  constantly.

The reason this is a strong fit specifically for *this* game: an idle/exploration hybrid
has two very different play patterns — the player who walks 10km a day and the player who
checks in twice a day between meetings. A points tree lets both optimise toward what they
actually do (reveal radius and POI range for the walker; worker slots, offline cap and
efficiency for the idler) without the design having to pick a winner.

### 3.0b How Adventurer XP is earned

A **flat cut of all skill XP**, plus discrete milestones. The flat cut means Adventurer
Level automatically tracks total activity without a parallel reward system to maintain and
rebalance.

```
adventurerXp += floor(skillXp * GLOBAL_XP_RATIO)     // ~0.25, one tunable constant
```

Plus one-off grants for things that aren't skill training:

| Milestone | Adventurer XP |
|---|---|
| Reveal a new cell | small, flat |
| First-ever visit to a given POI | moderate — rewards exploring, not farming |
| Claim territory | large |
| Complete a craft | moderate |
| Unlock a new skill | large — momentum into the next one |
| First time reaching a new region/city | large |

Note what this incentivises: the milestone list is deliberately weighted toward **novelty**
(new cell, new POI, new region) rather than repetition. A player grinding one skill at one
spot levels that skill fast and their Adventurer Level slowly. A player who ranges widely
unlocks new systems faster. That's exactly the behaviour the product wants, and expressing
it here means it doesn't have to be bolted onto each skill separately.

`GLOBAL_XP_RATIO` is the single most important balance constant in the game — it sets how
fast the whole unlock spine moves. Keep it in seeded config, not code.

### 3.0c The curve

Adventurer Level uses the **same RuneScape curve** as skills (§3.2) but with its own,
shallower scaling — unlock milestones should land at a satisfying pace, not the punishing
grind that suits a maxed skill. Practically: same formula, divide the requirement, tune
`GLOBAL_XP_RATIO` against it.

It is **uncapped** like everything else. Past the last skill unlock (~level 30), Adventurer
Levels keep arriving and each still grants a **Bonus Point** (§3.0a), so the upgrade tree
keeps deepening long after the last skill unlocks. That's the long-tail progression for
players well past the content ladder, and it means the number never stops meaning something.
When the tree itself is exhausted, surplus points convert to prestige currency.

### 3.1 You start with nothing, and skills unlock on Adventurer Level

**A new player has one skill.** Not fifteen — one. The rest arrive on a fixed ladder as
your **Adventurer Level** rises.

This is the core progression fantasy: you are nobody, and you earn the right to do things.
But crucially, that ladder is **geography-independent**. Everyone gets Fishing at Adventurer
Level 3 whether they live on a harbour or in Zone 1.

> **Why not unlock skills by finding POIs?** An earlier draft of this doc had skills
> discovered by visiting a matching POI — walk past a church, unlock Prayer. It's an
> appealing idea and it is *wrong*, because it makes access to the game depend on where you
> happen to live. Mining maps to quarries, mineshafts and caves. A player in a flat suburb
> would never unlock it. Worse is the near-miss case: you find one quarry on holiday,
> unlock Mining, go home, and now own a permanently stalled bar you can never train. The
> player did everything right and got a dead end. **The unlock ladder must not be
> geographic.** See §3.1b for what POIs do instead.

#### The ladder

Indicative shape — the numbers are for tuning, the *structure* is the point. **Every level
also grants a Bonus Point** (§3.0a), so levels without a skill unlock are never empty — the
player always gets a decision to make.

| Adv. Level | Unlocks skill | System unlocked | Why here |
|---|---|---|---|
| 1 (start) | **Exploration** + **Foraging** *(new)* | Materials + inventory, workers, 1 slot | Both from minute one. See the cold-start note below. |
| 3 | **Fishing** | — | First terrain-specific skill. Teaches "some places are better than others." |
| 5 | **Woodcutting** | **Claims** | Second terrain skill; player now has a reason to compare routes. Territory arrives alongside. |
| 8 | **Cooking** | **Crafting** | First *production* skill — consumes what Fishing/Foraging gathered. Closes the first mini-loop. |
| 12 | **Mining** | — | Rare-terrain gathering. Workers are established by now to cover thin geography. |
| 16 | **Smithing** | Buildings | Consumes Mining output. Second production loop. |
| 20 | **Farming** | — | Slow-burn, worker-heavy, idle-flavoured. |
| 25 | **Trading** | Economy / market | |
| 30+ | Prayer, Knowledge, Healing, Athletics, Tavern, Banking, Combat | POI features (§3.1d) | Later tier; several are POI-flavoured and POIs are a familiar system by now. |
| — | — | — | Bonus Points continue forever (§3.0c). |

Only **skills and whole systems** unlock automatically here. Everything incremental — worker
slots beyond the first, offline cap, reveal radius, Claim capacity — is bought with Bonus
Points instead. That keeps this table short and stable, and puts the frequent, granular
decisions in the player's hands.

#### The cold-start problem

An earlier draft gave level 1 players *only* Exploration, with Foraging at 2. That's wrong:
a player whose first walk paints cells but drops no materials, with nothing to craft, build,
or assign, is using a map-painting utility, not playing an RPG. The first session is where
retention is won and it cannot be the emptiest one.

So **level 1 ships with Exploration, Foraging, materials, inventory, and one worker.** The
first walk must produce something you can hold, and the first time the app is closed, a
worker must already be working. Everything after that is expansion; this is the minimum
viable *game*.

Corollary for the level curve (§3.0c): levels 1–3 should be reachable within the first
session or two. The ladder's early rungs are onboarding, not progression — pace them for
teaching, not for challenge.

Four things this buys:

- **Guaranteed progression.** Nobody is ever locked out of content by their postcode.
- **Paced onboarding.** A new player gets one thing to learn, then another a few days
  later. Fifteen skills at once is paralysing; one every few levels is a drip-feed of
  novelty through the critical first fortnight.
- **Deliberate teaching order.** Gather before produce. Common terrain before rare. Each
  unlock can assume the player already understands the previous one.
- **One dial for the whole spine.** Because every unlock hangs off a single pool, pacing
  the entire game is one constant (`GLOBAL_XP_RATIO`) plus one seeded table.

#### Schema

```
Player                                    -- existing table, repurposed
  ...
  AdventurerXp (long)                     -- replaces the current Xp
  AdventurerLevel (int)                   -- replaces the current Level, cached

PlayerSkill
  PlayerId, SkillType, Xp (long), Level (int), UnlockedAtUtc
  unique (PlayerId, SkillType)

UnlockDefinition                          -- seeded, not hardcoded
  AdventurerLevel, UnlockType, Payload    -- Skill | System
```

Note this **reuses `Player.Xp` / `Player.Level`** rather than adding columns — those fields
already exist and already hold a global number. They're simply renamed and re-curved. The
migration is a rename plus a one-off XP rescale, not new state.

`PlayerSkill` row exists ⟺ skill is unlocked. No "level 0" state to special-case.

Unlocks are granted server-side whenever `AdventurerLevel` crosses a threshold: one lookup
against `UnlockDefinition`, applied transactionally with the level-up. Keeping unlocks as
*seeded data* rather than a C# switch is what lets you retune the entire progression spine
without a deploy — and you will, repeatedly.

The ladder is self-pacing in a mild way: more unlocked skills means more sources of skill
XP, which means more Adventurer XP via the flat cut, which means the next unlock arrives
sooner. That gentle acceleration is a feature — the early game should feel like it's opening
up — but unlike the summed-levels model it can't be gamed by deliberately spreading thin.

### 3.1b What POIs are for

POIs stop being gates and become **amplifiers**. This is the key inversion, and it's what
makes geography matter without making it punishing:

| | Role |
|---|---|
| **Workers** (§5.2) | Baseline. Train any unlocked skill, slowly, from anywhere, offline. The floor nobody falls below. |
| **Walking / adventuring** | Accelerator. Covering ground trains the relevant skills faster than idling. Rewards actually playing. |
| **POIs** | **Multiplier + exclusive source.** Big XP boost, materials you cannot get elsewhere, and eventually location-based minigames (§3.1d). |

So: a player with no quarry nearby still levels Mining — via workers, slowly, and via
walking rocky terrain, faster. A player with a quarry levels it *much* faster and gets
Rare Ore that the first player simply cannot obtain any other way.

**Geography changes how well you play, not whether you can play.** That's the line this
design holds, and every future system should be checked against it.

This also means POI exclusivity is now *safe* to lean on hard. Exclusive materials behind
rare POIs were dangerous when POIs gated skills — they'd compound a lockout. With the
ladder guaranteeing access, exclusives become a genuine reason to travel rather than a
tax on where you live. Lean into them: a cathedral should give something a chapel never
does.

### 3.1c Unlock as an event

The unlock moment was the best hook in the discovery model, and it survives the change
intact — it just fires on level-up instead of on arrival. Full-screen celebration, push
notification, and the new skill lands at level 1 with its first worker slot ready to
assign. Don't let this be a toast.

Locked skills should show as **named but greyed, with their unlock level visible** —
"Mining → unlocks at 12". Unlike the discovery model, where hiding them preserved mystery,
here a visible ladder is a *roadmap*. It's one of the strongest retention levers available:
the player always knows exactly what's next and roughly how far away it is.

### 3.1d POI minigames (future)

Flagged as direction, not spec. Once POIs are amplifiers rather than gates, they become
the natural home for **location-based minigames** — a short interactive activity you can
only do while physically at a place. Fishing at a harbour, a dig at a quarry, a puzzle at
a museum.

This is the long-term reason the POI system earns its complexity, and it's worth keeping
the visit endpoint (§8, Phase 2) general enough to grow into it: a visit should be a
*session* with a server-issued token, not a fire-and-forget XP grant. Getting that shape
right early costs nothing and avoids a rewrite later.

### 3.1e The POI→skill map is the part that already works

The ~80 OSM tag→skill mappings in
[PoiImportService.cs](GeoSlayer.Domain/Services/PoiImportService.cs) are **done and good**,
and nothing above changes them. Castles→Combat, bakeries→Cooking, quarries→Mining — that
table is the bridge between real geography and game systems, and it already carries
per-POI `XpReward` weighting (a cathedral is 25, a chapel 10).

Under the ladder model that table's job shifts from *gating* to *bonus routing*: it answers
"which skill does this place boost, and what exclusive material does it drop." Bonuses,
material drops (§4.1) and tool gating (§4.3) are all just readers of it. The work ahead is
consuming it, not rebuilding it. Additions worth making are gradual: more tag coverage, and
a `Rarity` derived from local POI density (§7).

One addition the ladder does require: **`Foraging` as a new `SkillType`**, plus terrain
mappings for the gathering skills so that walking through woods trains Woodcutting without a
POI present. That's the mechanism that makes "walking accelerates training" true everywhere
rather than only near tagged places.

### 3.2 The XP curve

The current `level * 100` at [FogService.cs:157](GeoSlayer.Domain/Services/FogService.cs#L157)
is linear and must go. Linear curves make early levels crawl and late levels meaningless.

Use the RuneScape curve for **both** tracks — it is battle-tested for exactly this shape of
game (slow burn, never caps, feels good at every stage):

```
xpForLevel(n) = floor( (1/4) * →_{i=1}^{n-1} floor(i + 300 * 2^(i/7)) )
```

Level 2 = 83 XP. Level 50 = 101k. Level 92 = half of 99. Level 99 = 13M.

Skills use it as-is. **Adventurer Level uses the same curve divided by a constant** (§3.0c)
so that unlock milestones land at a human pace rather than a maxed-skill grind. Same lookup
table, different divisor — no second curve to reason about or balance.

**Do not cap at 99**, on either track. Continue the formula indefinitely. Levels past 99 get
progressively slower on their own; no artificial ceiling needed. Display 99+ with a distinct
colour and keep counting. This is the "infinite levelling" requirement — the curve handles
it, no special-casing.

Implement as a precomputed `long[]` lookup table built to ~200 levels at startup, with
the formula as fallback beyond. Table lookup, no per-sync maths.

### 3.3 What grants XP

Every XP-granting action pays **twice**: into the relevant skill, and a flat cut into
Adventurer XP (§3.0b). The table below gives skill XP; Adventurer XP is
`floor(skillXp * GLOBAL_XP_RATIO)` on top, plus the milestone grants in §3.0b.

Every unlocked skill has three training routes, by design (§3.1b). Rates are indicative and
exist to show the *ratio* — POIs should feel worth a detour, workers should feel worth
setting up, walking should feel worth doing.

| Action | Skill | Skill XP | Route |
|---|---|---|---|
| Reveal a new cell | Exploration | 2 base, scaled (§3.4) | walk |
| Walk through matching terrain | that terrain's gathering skill | ~1/cell | walk |
| **Visit a POI in range** | **that POI's skill** | **`XpReward`, ~20× a terrain cell** | **POI** |
| Worker assigned to a skill | that skill | slow trickle, offline, capped (§5.3) | idle |
| Craft an item | the crafting skill | recipe-defined | craft |

The ordering matters more than the numbers: **POI k walking > idle**. Workers are the floor
that guarantees progress; walking is the meaningful everyday activity; POIs are the spike
that makes a specific place worth going to. If workers ever approach walking in rate, the
walking stops and the game is dead — see the pillar in §1.

**Everything pays Adventurer XP, but idle pays less.** Workers, crafts, visits, walking —
all of it contributes, so no activity feels like it's "not counting." But idle sources use a
**reduced ratio** (say 0.25× the normal cut) so that a player who never leaves the house
still progresses, just slowly. This preserves the §1 pillar — walking remains by far the
fastest route — without the harshness of excluding idle entirely.

The distinction matters at the margin: a busy week where you only check in on workers should
still move your Adventurer Level a little. It just shouldn't keep pace with a week of
walking.

### 3.4 First-visit bonus and diminishing returns

The single most important anti-degeneracy rule: **revisiting the same POI must decay.**

```
visitXp = XpReward * decay(visitCount)
decay(n) = max(0.05, 1 / (1 + 0.5*n))
```

First visit full. Second 67%. Fifth ~29%. Floors at 5% so your local pub is never
literally worthless but is never a farm either. Decay resets slowly — regain 1 "visit
charge" per 24h, max back to full over a couple of weeks. This makes **new ground
strictly better than old ground**, which is the entire point of an exploration game.

Same principle on cells: a re-revealed cell is worth 0 (already the behaviour — the
unique index on `(PlayerId, GridLat, GridLng)` handles it for free).

### 3.5 Cell reveal scaling

`RevealRadius = 0` is too stingy — one ~100m cell per sync, and the background path
(§6) reveals almost nothing. Proposal:

- Reveal radius becomes **gear- and level-dependent**: base 1 (a 3×3 block), rising with
  Exploration level and a craftable "Cartographer's Lens" item.
- The server reveals the **swept path** between the last position and the current one,
  not just the endpoint cell. This is what makes background walking work and is the
  single highest-value fix in the doc.
- `MaxNewCellsPerSync` rises accordingly — keep it as an anti-cheat ceiling but compute
  it from elapsed time → max speed, not a flat 9.

---

## 4. Materials & crafting

### 4.1 Materials come from two places

**Cells** yield raw, common materials by terrain. The OSM data needed to classify a cell
is already being fetched for POIs — tag the cell at reveal time:

| Cell contains | Yields |
|---|---|
| forest / wood | Timber |
| water | Fish, Reeds |
| industrial / quarry | Ore, Stone |
| farmland | Grain, Fibre |
| urban / residential | Scrap, Coin |
| anything | Dust (universal filler) |

**POIs** yield rarer, skill-specific materials on visit:

| Skill | Material |
|---|---|
| Prayer | Incense, Blessed Water |
| Smithing | Ingots, Coal |
| Cooking | Spices, Preserved Food |
| Mining | Gems, Rare Ore |
| Knowledge | Scrolls, Ink |
| Combat | Relics, Weapon Fragments |
| & | one or two each |

```
Material        Id, Key, Name, Tier, SkillType?, LevelRequired, BaseGatherSeconds
PlayerMaterial  PlayerId, MaterialId, Quantity   -- unique (PlayerId, MaterialId)
```

### 4.1a Material tiers — the RuneScape pattern

Materials are **tiered by skill level**, RuneScape-style. This is the core of what makes a
gathering skill feel like progression rather than a number going up.

| | Rule |
|---|---|
| **Access** | Each material has a `LevelRequired`. You cannot obtain it below that level, at all. |
| **Speed** | Higher tiers have a longer `BaseGatherSeconds` — better materials take longer per unit. |
| **Obsolescence** | Lower tiers stay available but become irrelevant. That's intentional, not a flaw. |

Indicative shape for a gathering skill — roughly one new tier every 10–15 levels, so there's
always a next unlock in view:

| Tier | Level | Example (Mining) | Gather time | Value |
|---|---|---|---|---|
| 1 | 1 | Rough Stone | 3s | trivial |
| 2 | 10 | Copper Ore | 5s | low |
| 3 | 20 | Iron Ore | 9s | moderate |
| 4 | 35 | Silver Ore | 15s | good |
| 5 | 50 | Gold Ore | 24s | high |
| 6 | 70 | Gemstone | 40s | rare |
| 7 | 90 | Meteoric Ore | 60s | prestige |

**Why longer gather times, not just rarer drops.** The two look similar but behave very
differently. Pure rarity means high-level play is a lottery with long dry spells; explicit
gather time means it's *reliably slower*, which reads as progression rather than bad luck.
It also keeps XP/hour roughly flat across tiers — higher tiers grant proportionally more XP
per unit, so the curve stays smooth and the player is never punished for advancing.

#### The geography trap — and the rule that avoids it

There's an obvious-looking design here that is **wrong**: make low tiers come from common
terrain and high tiers from rare terrain. It's thematically tempting (gems come from
quarries!) and it silently rebuilds the geographic lockout the whole unlock ladder exists to
prevent — a player in a flat suburb would cap at tier 2 permanently.

**The rule: tier gates on skill level; terrain gates on speed.**

- *What* you can obtain is determined **only** by your skill level.
- *How fast* you obtain it is determined by terrain, POIs, gear and tools.

So a landlocked player at Mining 50 can gather Gold Ore. It takes them appreciably longer
than the player who lives by a quarry, and that's the correct amount of difference —
meaningful, not disqualifying. Same principle as §5.2, applied to materials.

#### Interaction with existing systems

- **Cell reveals** roll the drop table for the highest tier the player has unlocked, with
  weighted fallback to lower tiers. Walking one cell doesn't take 60 seconds — the gather
  time scales the *quantity* yielded per cell rather than blocking on a timer.
- **Workers** (§5.2) gather at the same tier rules, applying `BaseGatherSeconds` directly
  since they work in real time. This makes high-tier worker output genuinely slow, which is
  the right pressure.
- **Tools** (§4.3) reduce gather time within a tier — this is what makes crafting a pickaxe
  worthwhile, and gives Smithing a clear purpose.
- **Museum** (§5A) gets a natural entry per material per tier, so the collection log fills
  as a side effect of normal progression.

#### Production skills

The same pattern, inverted: recipes have a `LevelRequired` and higher-tier recipes take
longer (`DurationSeconds`) and consume higher-tier inputs. A level 50 Smithing recipe needs
level 50 Mining materials — which is what couples the gathering and production skills into
a genuine chain rather than two parallel bars.

### 4.2 Recipes

```
Recipe        Id, Key, Name, SkillType, LevelRequired, DurationSeconds, XpReward, OutputMaterialId?, OutputItemId?
RecipeInput   RecipeId, MaterialId, Quantity
```

Data-driven, seeded from a JSON file, not hardcoded C#. You will iterate on balance
constantly and you do not want a redeploy for each tweak.

`SkillType` + `LevelRequired` on a recipe is a **double gate**: you need the skill unlocked
(via the account-level ladder, §3.1), then at level. Show locked recipes greyed with their
requirement visible, matching the skill ladder's roadmap treatment — the player should
always be able to see what they're working toward.

Recipes gated on **exclusive POI materials** are the exception worth handling carefully:
those are genuinely unobtainable for some players until they travel. Mark them visually as
*travel-gated* rather than *level-gated*, so the player understands the difference between
"keep playing" and "go somewhere new."

Crafting is **time-gated, not tap-gated** — this is the idle hook. A recipe takes real
minutes to hours. You queue it and walk away. That's the bridge between the two genres:
you craft *because* you're about to stop playing.

### 4.3 What crafting produces

Three output classes, each closing the loop differently:

1. **Gear** — equipment with modifiers. `+15% Exploration XP`, `+1 reveal radius`,
   `+20% POI interact range`, `+2h offline cap`. Directly multiplies walk yield.

   Gear and Bonus Points (§3.0a) touch the same stats deliberately — they're different
   *acquisition* routes to similar power, which is what makes both interesting. Points are
   permanent, chosen, and earned by playing at all; gear is crafted, swappable, and gated on
   materials and geography. Keep gear bonuses **larger but conditional** (situational,
   durability-limited, or slot-competing) so they don't merely duplicate the points tree.
   If a point and a gear item ever grant the same flat `+10%`, one of them is redundant.
2. **Tools** — gate access. A Pickaxe is required for Mining POIs; a Fishing Rod for
   Fishing. Tiered, so better tools unlock better nodes. Gives crafting a *reason*.
3. **Buildings** — placed on claimed territory, drive the idle layer (§5).

---

## 5. The idle layer

This is the part that doesn't exist at all yet and needs the most thought, because it's
where the game is won or lost for players with 20-minute commutes.

### 5.1 Claims

Revealed cells are ephemeral progress. **Claims** make territory persistent and
productive.

- Reveal a contiguous block of cells (say 3×3 fully revealed) → it becomes **claimable**.
- Claiming costs materials and is permanent.
- A Claim has a **terrain profile** derived from its cells, which determines what it can
  produce.
- Claims are where Buildings go.

This gives exploration a *goal state* — the thing streets were providing before they were
removed. You're not just painting the map, you're annexing it. And unlike streets, it
composes cleanly with the grid you already have: a Claim is just a rectangle of
`RevealedCells`.

**Claim density cap** — one Claim per N cells of revealed territory, so you cannot claim
your entire neighbourhood on day one. Forces spreading out.

### 5.2 Workers

The idle engine, and — per §3.1b — **the accessibility floor for every skill**. Workers are
assigned to a Claim + a skill.

```
Worker  Id, PlayerId, ClaimId?, Name, Tier, AssignedSkill?, StartedAtUtc?
```

A working Worker produces two things at once:

1. **Materials** — at `rate(tier, claimQuality)` per hour, terrain-appropriate.
2. **Skill XP in `AssignedSkill`** — the trickle that guarantees a player with no quarry
   still levels Mining.

This dual output is what makes workers load-bearing rather than decorative. They are the
answer to "what if the geography near me is bad."

**Worker slots are the flagship Bonus Point purchase** (§3.0a). You start with one; every
additional slot is bought, at escalating cost. This is deliberately the most attractive
early upgrade for a player whose local geography is thin — the design's answer to bad
terrain is something the player actively chooses, rather than a consolation prize handed
out automatically.

- Workers run **offline**. This is the core idle promise.
- Offline accrual is **capped** — base 4 hours, extended by the Offline Cap upgrade,
  Buildings and gear up to ~24h. The cap is the anti-degeneracy lever: you must come back,
  but you needn't be glued.
- Workers consume **upkeep** (food, coin). Unfed workers idle. Upkeep is the material
  sink that stops infinite stockpiling.
- **Terrain is a multiplier, never a gate.** A worker can train *any* unlocked skill on
  *any* Claim. Matching terrain grants a large bonus — call it +50% to +100% yield and XP —
  so varied, well-chosen territory is strictly better, but a landlocked player's worker
  still trains Fishing at base rate.

  > **This rule is load-bearing.** An earlier draft made terrain a hard requirement
  > ("Fishing needs a water Claim"), which silently reintroduced the exact geographic
  > lockout the unlock ladder exists to prevent: a player in a landlocked area unlocks
  > Fishing at level 3 and can then never train it offline. The multiplier form keeps
  > everything the gate was for — territory matters, varied Claims are better, choosing
  > where to expand is a real decision — without any player ever hitting a wall. **Check
  > every future system against this: geography changes how well you play, never whether
  > you can.**

Workers are acquired by levelling, crafting, or visiting rare POIs.

**Balance guardrail:** worker XP rate must stay well below walking rate for the same skill
(§3.3). The worker is a floor, not a substitute. If a player can rationally decide to stop
walking because the workers have it covered, the rate is wrong.

### 5.3 Offline calculation

Do **not** tick workers on a timer. Compute lazily on next sync:

```
elapsed = min(now - lastCollectedAtUtc, offlineCapHours)
yield   = rate * elapsed * modifiers
upkeep  = consumption * elapsed
```

One calculation on login, no Hangfire job, no scaling problem. Hangfire is already wired
up and currently registers zero jobs — keep it that way for this; lazy evaluation is
strictly better here.

**Why lazy beats ticking, concretely:** a ticking model costs O(players × workers) work
every tick forever, including for the 90% of players who are asleep. Lazy costs O(workers)
once, only for players who actually return. At 10k players with 10 workers each, a
1-minute tick is 100k writes/min against Postgres for nothing. Lazy is ~0. The only thing
ticking buys you is push notifications ("your workers are idle!"), and those are better
scheduled as one-shot jobs at the known cap-reached timestamp.

The returning-player screen — "While you were away: 340 Timber, 12 Ore, 2 levels" — is
the single highest-value piece of UX in the idle half. It's what makes closing the app
feel good rather than like quitting.

### 5.4 Worker Expeditions

**The problem this solves:** POI value currently expires. You visit a cathedral on holiday,
get a one-off boost, and it's dead to you forever — which is a shame, because that cathedral
is the most memorable thing in your game.

**Expeditions** let you dispatch a worker to any POI you have *personally visited at least
once*, however long ago. The worker travels (a multi-hour timer scaled by real distance) and
returns with that POI's exclusive materials and skill XP.

- Requires a genuine prior visit — the visit log already exists for decay purposes (§3.4),
  so this is nearly free to implement.
- Expedition duration scales with real-world distance, so far-flung POIs are high-value,
  low-frequency plays.
- Occupies the worker for the duration, so it competes with Claim work. A real decision.

This turns every trip you ever take into a permanent asset. Travel stops being a transient
boost and becomes **portfolio-building** — and it gives holidays, work trips and day trips
lasting mechanical weight, which is exactly the behaviour a location game should celebrate.
It also softens rural/urban imbalance (§7.3) from a different angle: a rural player who
visits a city once permanently gains access to urban materials.

### 5.5 District synergies

Claims of *varied* terrain placed contiguously form a **District** with a combined bonus —
Forest + Water + Farmland becomes a "Homestead" at +15% total output, say.

This gives Claim placement genuine strategic depth. Without it, the optimal play is to claim
nine identical cells of your best terrain; with it, you're reading the actual map and looking
for variety. It also pairs well with the terrain-multiplier rule (§5.2): you want varied
terrain both for per-worker bonuses and for District completion.

Districts are a natural home for named set-bonuses and a good long-term content lever —
new District recipes cost nothing but data.

### 5.6 Resource Surges

Temporary, localised buffs: "Herb Bloom in local parks, 48 hours," "Meteor sightings at high
elevation." Applied to a POI category or terrain type within a region.

The purpose is **route disruption**. The biggest long-run risk to an exploration game is
that players settle into a fixed commute and the world stops feeling alive. Surges give a
recurring, cheap reason to deviate — and because they're generated server-side against
regions that already exist, they cost almost nothing to run.

Keep them frequent, small, and clearly signposted on the map. A surge should feel like a
nudge, not an obligation; a player who ignores every surge should still progress fine.

### 5.7 Patrol Routes

**The problem this solves:** diminishing returns (§3.4) are necessary but they make your
daily walk progressively less rewarding — and your daily walk is the habit the whole game
depends on. Punishing routine is dangerous.

A **Patrol Route** is a saved loop the player defines. Re-walking it grants no cell-reveal
XP (correctly — it's not new ground), but *completing the circuit* awards a reliable batch
of **upkeep goods**: food and coin to keep workers fed (§5.2).

This is a neat resolution of the tension. Novelty stays the only route to *progress*, while
routine becomes the route to *maintenance*. The player who walks the same loop daily isn't
levelling fast, but they're sustaining their idle engine — which is a genuinely satisfying
role for a habitual walk, and it means the upkeep sink has a dependable supply that doesn't
require constant novelty.

It also creates a pleasing rhythm: patrol on weekdays to keep the machine fed, explore
somewhere new at the weekend for real progress.

---

## 5A. The Museum (collection log)

**The problem this solves:** once you've drained a POI's XP, the doc gives you no reason to
ever care about it again. Decay (§3.4) actively pushes you away from it. That's correct for
*progression* but it means the world becomes disposable — and a game about real places
shouldn't make places disposable.

The **Museum** is a persistent collection log, framed as a building you fill rather than a
checklist you tick. Every entry is permanent, and permanence is the point: it's the record
of everywhere you've been and everything you've found, and it's the only system in the game
that never decays, caps or resets.

### 5A.1 Why a Museum rather than a bare log

Framing matters here. A "collection log" is a completionist's spreadsheet; a Museum is a
*place you built*. It gives:

- **A physical home** — a screen that visibly fills up, with wings that unlock as you
  progress. Empty plinths are a stronger pull than empty checkboxes.
- **A reason to display** — a shareable profile, which is the natural social hook for a
  single-player v1 (§9).
- **A sink for duplicates** — donate spares for Museum-only currency. Gives the material
  overflow from §7.4 somewhere dignified to go.

### 5A.2 What goes in it — the wings

You already have everything needed to populate this; the entries fall out of existing data.

| Wing | Filled by | Entry count | Source |
|---|---|---|---|
| **Cartography** | Regions, cities, counties, countries entered | Grows with travel | Already know player position; reverse-geocode a region on first entry |
| **Landmarks** | POI *types* you've stood at — cathedral, castle, lighthouse, pier, quarry, windmill | ~80 | The existing OSM tag table (§3.1e) |
| **Naturalist** | Terrain types traversed — ancient woodland, tidal water, moorland, marsh | ~20 | Cell terrain classification (§4.1) |
| **Skill wings** (one per skill) | Materials gathered, items crafted, at each tier | ~20 each | Material and recipe tables |
| **Rarities** | Low-drop-rate finds | ~50 | Gathering drop tables |
| **Relics** | Clue scroll rewards (§5B) | ~60 | Clue reward tables |
| **Expeditions** | Trophies from distant POIs (§5.4) | Grows | Expedition results |
| **Feats** | Milestones — 1000 cells, first level 50, a 20km walk, all four seasons | ~40 | Derived from existing counters |

Two wings worth highlighting because they're near-free and unusually good:

**Cartography** is the one that makes the Museum *yours*. "Counties visited: 7 of 48" is
compelling in a way generic item lists aren't, and it costs one reverse-geocode per new
region. It also gives long-distance travel a permanent trophy, reinforcing Expeditions
(§5.4).

**Landmarks** turns the existing ~80-entry tag table directly into content. Every OSM
category you already map becomes a plinth. Zero new data modelling — just a `PlayerLandmark`
row on first visit to each type.

### 5A.3 Rules

- **Entries are permanent.** Never lost, never decayed. This is the one system immune to
  every other system's pressure.
- **First-find is what counts**, not quantity — so the Museum rewards *breadth*, complementing
  skills which reward depth.
- **Donating duplicates** yields Museum currency, spent on wing expansions and cosmetic
  display upgrades. A dignified sink for §7.4 overflow.
- **Set bonuses, sparingly.** Completing a wing grants a modest permanent bonus. Keep these
  small — the Museum should be pursued for its own sake, and heavy bonuses would make it
  mandatory rather than beloved.

```
MuseumEntryDefinition   Key, Wing, Name, Description, Rarity, UnlockCondition
PlayerMuseumEntry       PlayerId, EntryKey, FirstAcquiredUtc, Quantity, AcquiredAtPoiId?
                        unique (PlayerId, EntryKey)
```

`AcquiredAtPoiId` and the timestamp are what make it a diary rather than a tally — "found at
Durham Cathedral, 3 May" is the detail that makes the whole system land.

---

## 5B. Clue Scrolls

**The problem this solves:** every other system rewards walking *in general*. Nothing in the
doc gives the player a reason to walk **somewhere specific**. Clues are the directed-travel
system, and they're the natural content engine for the Museum.

This is also the system that best exploits what's genuinely unique about the product: real
geography as a puzzle surface.

### 5B.1 The shape

A **Clue Scroll** is a chain of steps. Each step names a location, cryptically or directly;
you must physically go there. Complete the chain for a reward roll from a tier-specific
table, heavy with Museum-only Relics.

- Scrolls drop from gathering, POI visits, and rarely from workers.
- Tiers — **Wandering / Roaming / Pilgrim / Odyssey** — scale in step count, travel distance
  and reward quality.
- One active scroll per tier at a time. Prevents hoarding and keeps each one meaningful.
- Steps are generated **relative to the player's own area**, so a rural and an urban player
  both get workable clues.

### 5B.2 Step types

All of these are answerable from OSM data you already import, which is what makes the system
tractable rather than a content-authoring treadmill.

| Type | Example | Validated by |
|---|---|---|
| **Direct** | "Visit the church on Mill Lane." | POI id |
| **Cryptic** | "Where the faithful gather beneath three spires." | POI tags + a generated riddle template |
| **Category** | "Stand at any lighthouse." | POI category — works anywhere |
| **Coordinate** | A map pin with a search radius. | Position |
| **Terrain** | "Find still water surrounded by trees." | Cell terrain classification |
| **Relational** | "Where two rivers meet." / "The highest point within a mile." | Spatial query on imported geometry |
| **Sequence** | "From the old mill, walk north until you find stone." | Position + bearing |

**Cryptic clues are generated, not hand-written.** Template + POI tags:
`"Where the {faithful gather} beneath {three spires}"` from
`amenity=place_of_worship` + `building:levels`. That gives effectively unlimited content
from data already in the database — a hand-authored clue system would die of content debt
within a month.

### 5B.3 Rules and guardrails

- **Always achievable.** Generate steps only against POIs and terrain within a reachable
  radius of the player's revealed territory. Never generate a step requiring a 200-mile
  trip — the §5.2 geography rule applies here too.
- **Skippable steps.** Allow one skip per scroll at a cost. A clue the player genuinely
  cannot solve must never become a permanent blocker on their only scroll of that tier.
- **No time limits.** Clues are for savouring, not stressing. They pair naturally with
  weekend walks.
- **Respect the anti-cheat.** Arrival is validated by the same server-side position checks
  as POI visits (§7.2) — clue completion must not become a spoofing vector.
- **Rewards are mostly cosmetic and collection.** Relics for the Museum, display items,
  Museum currency, some materials. Keep raw power low so clues stay optional; a player who
  ignores clues entirely should not fall behind.

```
ClueScrollDefinition  Key, Tier, StepCount, RewardTableKey
PlayerClueScroll      Id, PlayerId, Tier, CurrentStep, StartedUtc, CompletedUtc?
ClueStep              ScrollId, StepIndex, StepType, TargetPoiId?, TargetLat?, TargetLng?,
                      TargetRadius?, RiddleText, SolvedUtc?
```

### 5B.4 Why this pairs with the Museum

Clues generate Relics; Relics fill the Museum; the Museum gives Relics a reason to exist.
Neither system carries its weight alone — a collection log with nothing interesting to
collect is a chore, and clues with forgettable rewards aren't worth the walk. Together they
form the game's **exploration-for-its-own-sake loop**, parallel to the progression loop and
deliberately independent of it.

That independence is the point. A player who has maxed their practical progression still has
a museum to fill, and the clue system points them at parts of their own city they've never
walked.

---

## 5C. Combat — encounters

**Decided (Stage 15 follow-up).** Combat is an encounter system, not another gathering
ladder. Two sources, deliberately different in character:

### 5C.1 Two kinds of encounter

| | **Roaming** | **Training grounds** |
|---|---|---|
| Where | Spawns at random POIs near the player | Fixed, at historic POIs |
| Lifetime | Temporary — appears, expires if ignored | Permanent |
| Feels like | An event you happened upon | A place you go back to |
| Purpose | Rewards being out and about | Rewards knowing your area |

**Roaming encounters** spawn server-side against POIs the player could plausibly reach,
on the same pattern as Resource Surges (§5.6) — deterministic per cell per window, shared
between players in the same place, cheap to generate. They expire. Missing one costs
nothing.

**Training grounds** are fixed to `historic=castle|fort|ruins|battlefield` and
`military=*`. They do not expire, and they are the reliable route: the player who knows a
ruin two streets away has somewhere to train whenever they choose.

### 5C.2 The geography rule still applies

This is the part that must not be got wrong, and §9's original note already said it:
**historic POIs are a boost, never the only venue.**

- Roaming encounters spawn at *any* POI category, so a player with no castle still meets
  them.
- Training grounds are better — reliable, repeatable, no waiting — which is the
  compensating advantage for someone who has one.
- A player with zero historic POIs trains Combat entirely through roaming encounters. It
  is slower. It is never blocked.

Same shape as every other skill: *what* you can reach depends on level, *how fast*
depends on geography.

### 5C.3 Resolution

Auto-resolving, in keeping with the idle half. The player arrives, the encounter resolves
against their Combat level and equipped gear, and yields XP and materials from the
existing Combat ladder. No real-time input — the walk was the input.

Difficulty scales with Combat level so an encounter stays worth attempting, and losing
costs time rather than materials. §7.4's rule holds here too: nothing should punish a
player for being away.

### 5C.4 What this needs

Its own stage, not a bolt-on:

- `EncounterDefinition` (seeded), `PlayerEncounter`, spawn scheduling reusing the Surge
  pattern.
- Arrival validated by the same server-side position check as POI visits and clue steps
  (§7.2) — an encounter must not become a spoofing vector.
- The Combat ladder already exists and needs no change.

---

## 5D. Coin — the economy

**Resolves §9.5.** The open question was never "should coin exist" — §5.2 and §5.7 both
already assumed it — but *what coin is for*. A second currency with no job is worse than
none, so this section exists to give it one before it was built.

### 5D.1 Coin comes from selling, at shops

Every material has a coin value. Materials are sold at **Trading POIs** — markets,
supermarkets, malls, corner shops — and **only while standing at one**, validated against
the server's own last verified position exactly as POI visits are (§7.2).

**This is the whole design.** Coin is a *location* mechanic, not a menu, in a game whose
premise is going outside. A sell button in the inventory would have made money something
you generate from an armchair.

It does not reintroduce a geography gate. Trading POIs are the densest category in the
game — §4.1a asks that geography change how well you play, never whether you can, and
almost everyone has a shop within a walk. A rural player walks further to sell, the same
way they walk further for everything, and is never locked out.

**Prices are derived, never enumerated.** Value comes from tier and category: roughly
`tier²`, times a per-category multiplier. There are over a hundred materials across fifteen
ladders and a hand-written price list would rot the moment a ladder was added — the same
content-debt trap §5B.2 rejects for clue text. Three properties fall out of the formula and
are worth stating, because each is a rule rather than a tuning value:

- **Everything is worth at least one coin.** Dust is tier 1, and its entire job is that a
  walk across featureless ground still pays *something*. A floor of zero would make a
  thin-geography player's haul literally worthless.
- **Coin per gathering second rises with tier.** A tier-7 material takes 20× as long to
  gather as tier 1, so if it were not worth appreciably more, levelling would cut your
  income — the same rule §4.1a applies to XP.
- **Produced goods outvalue their inputs.** A crafted item costs materials *and* a craft
  timer. If it sold for the same as its parts, crafting would be a way to lose money.

**Selling is per material, with a junk shortcut.** One button that empties the bag would
eventually sell the ore someone was saving for a recipe, which is the kind of loss a player
does not forgive. The shortcut therefore reaches only low tiers and never Relics.

### 5D.2 Banking — deposits and interest

Coin can be deposited at **Banking POIs** (banks, ATMs, post offices) and earns interest
while away. Deposited coin **cannot be spent until withdrawn**; that illiquidity is the
entire trade, and merging it with coin in hand would make the bonus free.

**The rate is deliberately tiny — a nudge for locking money away, not an income.** A game
about walking outside must never make sitting still the efficient play. Two bounds enforce
that, and both are rules rather than preferences:

- **Interest accrues only over the offline-cap window** (4h base, extended by the same
  Offline Cap upgrades and gear the worker layer reads). A month away pays exactly what one
  window pays. §5.2 already decided how much a single absence may pay; a second, different
  idle bound would be a rule to learn twice and would drift.
- **Interest is simple, not compounding.** Compounding on an uncapped balance is how an
  idle economy runs away from every other income source in the game.

Banking's material ladder was **Copper Coin / Silver Coin / Gold Coin**; with real currency
it is reflavoured to valuables (Copper Token, Silver Bar, Gold Bullion). "Sell 40 Gold Coins
for 800 coin" reads as a bug rather than a trade. Those valuables are the highest-priced
category, which gives a bank a reason to be visited before deposits are unlocked at all.

### 5D.3 What coin is *for*

Coin's first job is the one §5.2 and §5.7 already assumed — it is a store of the value a
haul represents, spendable on upkeep and (in time) anything else the game charges for.
Its immediate job is more important than that: **it makes a full satchel worth something.**

Before it existed, gathering past a stack cap converted to Dust at a poor rate, and Dust
bought nothing. A good night's gathering turned into a number that did nothing. Selling
replaces that dead end with an errand.

**Still open:** what else coin buys. Upkeep is currently paid in food alone, and nothing
else in the game has a coin price yet. That is deliberate — the sink should be designed
rather than accreted — but it is the next question, not a settled one.

---

## 6. Background & offline behaviour

Covered in detail in the review, restated here as design requirements:

1. **Sync the full location batch**, not `locations[last]`. The server reveals the swept
   path. Without this, screen-off walking loses most of its ground.
2. **Refresh tokens in the background task.** Currently it reads the access token and
   never refreshes, so long walks silently 401 into an empty catch.
3. **Server-side path interpolation** — given two points and a timestamp delta, reveal
   the cells the line crosses, subject to the speed cap. Reuse the existing anti-cheat
   maths; it already computes exactly the distance/time check needed.
4. **Batch writes.** A 40-minute walk might reveal 200 cells; that should be one insert,
   not 200 round trips.

The anti-cheat in [FogService.cs](GeoSlayer.Domain/Services/FogService.cs) is genuinely
good and should be preserved through this change — the 2s cooldown just needs to become
"reject syncs closer than 2s apart" rather than "reject the whole batch."

---

## 7. Anti-degeneracy

Location games die to exploits. Each of these needs an answer before launch, and two of
them (transit sweeping, desk drift) are *passive* — they require no cheating intent and
will happen by default unless specifically prevented.

| Exploit | Mitigation |
|---|---|
| **GPS spoofing** | Speed cap (built), plus mock-location detection on Android, plus statistical flagging — impossibly efficient cell coverage, perfectly straight paths, zero GPS accuracy variance. Flag, don't autoban. |
| **Farming one POI** | Diminishing returns per POI (§3.4). The dominant strategy must be *new ground*. |
| **Transit sweeping** | §7.1 — reveals become Uncharted Transit above cycling pace. |
| **Desk drift** | §7.2 — displacement gate + accuracy cutoff. |

### 7.1 Transit sweeping

**The problem.** A 30-minute train journey crosses thousands of cells. Decaying *XP* above
walking pace (the earlier draft's fix) is insufficient, because the cells still reveal —
and cell reveal is the map, the Claims, the material drops and the visible progress. A
commuter would paint their entire region from a seat, which invalidates the §1 pillar
outright.

**Rejected fix: hard-cap reveals above ~18 km/h.** It's the obvious answer and I think it's
wrong. It punishes cyclists, who are a legitimate audience moving under their own power at
20–25 km/h, and it gives a bus commuter *nothing* for genuinely passing through new
territory. A rule that can't distinguish a cyclist from a passenger is too blunt for a game
whose entire premise is real movement.

**Preferred fix: Uncharted Transit.** Ground covered above the walking-pace threshold does
not reveal. It banks as **Uncharted Transit** — a per-player store of "places you passed
but did not see":

- Transit is stored as the *cells traversed*, capped per day so a long-haul flight doesn't
  bank a continent.
- Walking **converts** it: every cell you reveal on foot also redeems banked transit cells
  nearby, at some ratio (1 walked cell redeems 2–3 banked).
- Unredeemed transit decays over ~a week, so it's an opportunity, not an obligation.

Why this is better: the commuter's journey isn't wasted — it becomes a *reason to walk*,
seeded specifically along routes they already travel. It converts the game's biggest
exploit into its best retention mechanic, and it's thematically right: you glimpsed
somewhere from a train and now you want to go back and actually see it.

Cyclists sit naturally between the two: a speed-graded conversion curve (full reveal at
walking pace, partial at cycling pace, pure transit above) handles them without a special
case.

**If Uncharted Transit proves too complex to ship early**, the interim rule is the hard cap
— but treat that as a stopgap, and don't let it ossify.

### 7.2 Desk drift

**The problem.** Indoor GPS drifts 5–30m continuously. A phone sitting on a desk with the
background task running will wander across cell boundaries indefinitely, passively farming
cells, Exploration XP and terrain drops. No intent required — this is the *default*
behaviour for anyone who leaves the app running at work, and it's the most corrosive
exploit here because it rewards not playing.

It also poisons the data: without a displacement test there's no way to distinguish a
drifting desk from a slow walk, so every downstream heuristic degrades.

**Mitigation — all three, together:**

1. **Minimum displacement vector.** Require net movement >15m in a *consistent direction*
   before sweeping cells. Drift is random-walk: it oscillates around a centroid and nets
   near zero. Walking has bearing persistence. Compare net displacement against path
   length — a ratio near zero means drift, near one means travel.
2. **Accuracy cutoff.** Reject fixes with poor reported accuracy (`coords.accuracy` > ~25m).
   Indoor fixes are typically far worse than outdoor ones and this alone removes much of
   the problem.
3. **Dwell detection.** If the centroid of the last N fixes hasn't moved beyond a radius
   over several minutes, treat the player as stationary and suspend reveals until genuine
   displacement resumes.

These are cheap to implement and belong in the same server-side validation pass as the
existing speed cap in [FogService.cs](GeoSlayer.Domain/Services/FogService.cs). Do them
server-side, not on the client, for the same reason as everything else in this section.

### 7.3 Rural vs urban imbalance

A city player has 50× the POI density of a rural one. Mitigate by scaling POI rarity and
yield inversely with local density — the one church in a village is worth far more than one
of forty in a city. Compute density from the POI count already stored per imported region.

### 7.4 Material bloat

**The problem.** ~80 POI categories dropping exclusives, plus 6+ terrain materials, trivially
exceeds 60 unique item types. That's unmanageable in a phone UI, and it creates a nasty
interaction with the idle layer: uncapped inventory makes material management meaningless,
while strict caps mean offline workers fill up and stall overnight — turning the core idle
promise into a chore of pre-emptive clearing.

**Mitigation:**

- **Tier the materials, don't enumerate them.** Most POIs should drop from a *shared* pool
  by category and tier (Common Urban, Rare Industrial&), not a bespoke item each. Reserve
  genuinely unique named materials for a small set — perhaps 10–15 — attached to the rarest
  and most memorable POI types. Exclusivity means nothing if everything is exclusive.
- ~~**Cap per-material, not per-inventory.**~~ **Reversed — caps were removed entirely
  when coin arrived (§5D).** The original rule capped each material, with overflow
  converting to Dust at a poor rate, so that filling up created pressure to return.

  Two things were wrong with it in practice. Dust bought nothing, so overflow was a
  *deletion* dressed as a conversion — a good night's gathering became a number that did
  nothing. And the pressure it created was punitive: "come back or lose value" is a chore,
  where §5D's "come back and sell this" is an errand. Quantities are now `long`, so the
  only ceiling is one no walk reaches.

  **What replaced the pacing.** The offline *time* cap (§5.2) already did that work — §9.3
  says as much when rejecting stamina. Caps-as-pressure was always the weaker of the two
  levers and the only one that generated busywork.

- ~~**Buildings raise caps.**~~ The Storehouse became the **Counting House** and raises
  sell price instead. The acquisition route and the feel are unchanged — it still rewards
  the player who gathers more than they immediately need — and §4.3's rule that a modifier
  nothing reads is a bug meant it had to be repointed rather than left in place. The Museum
  Skills wing bonus and Banking level moved the same way.

The guiding principle: **the idle layer must never punish you for sleeping.** Diminishing
returns are fine; a hard stop that voids hours of accrual is not.

---

## 8. What to build, in order

Sequenced so each phase is independently playable.

**Phase 1 — make the existing loop correct** *(foundation, no new systems)*
- Swept-path reveal + full-batch background sync (§6)
- Token refresh in the background task
- `[Authorize]` on `JourneyController`, derive `playerId` from the JWT not the body
- Replace the linear XP curve with the RS curve (§3.2)
- Bump `RevealRadius` to 1
- **Desk-drift prevention (§7.2)** — displacement gate, accuracy cutoff, dwell detection.
  This belongs in Phase 1, not later: swept-path reveal *amplifies* the drift exploit, so
  shipping the two together is the only safe order.
- **Speed-graded reveal (§7.1)** — at minimum the interim hard cap above cycling pace, so
  transit sweeping is never the established norm. Uncharted Transit can follow later; what
  matters is not shipping unlimited reveal-at-speed and then taking it away.

**Phase 2 — skills become real** *(the game starts existing)*
- `PlayerSkill` table, per-skill XP; add `Foraging` to `SkillType`
- **Adventurer XP as its own pool** (§3.0) — rename `Player.Xp`/`Level`, dual payout on
  every XP grant
- The **unlock ladder** (§3.1) — level thresholds as seeded data, not hardcoded, so the
  pacing can be retuned without a deploy
- **Bonus Points + upgrade tree** (§3.0a). Ship with a small tree — Worker Slot, Reveal
  Radius, Scholar, Offline Cap — and grow it. A thin tree that works beats a wide one that
  isn't balanced. Include respec from day one; early players *will* mis-invest.
- Unlock celebration + push notification (§3.1c)
- `POST /api/journey/poi/{id}/visit` — the endpoint that doesn't exist yet, with range
  validation server-side. **Design it as a session, not a one-shot** (§3.1d) so POI
  minigames can grow into it later.
- Terrain→skill training on cell reveal, so walking trains gathering skills everywhere
- Visit decay (§3.4)
- Skills screen; wire up the ✨ stub button. Locked skills shown **named and greyed with
  their unlock level** — the ladder is a roadmap and should be visible.

Note the dependency: the worker trickle that makes rare skills accessible is Phase 5, but
skills unlock from Phase 2. Between the two, a player with poor local geography will have
unlocked skills that train slowly. Terrain training (above) covers most of this gap — but
if Phase 5 slips, consider a temporary flat passive trickle so no unlocked skill is ever
fully stalled.

**Phase 3 — materials & inventory**
- `Material` / `PlayerMaterial`, cell terrain tagging, POI material drops
- **Tiered material pools, not per-POI bespoke items** (§7.4) — get this right at the
  schema stage; retrofitting a tier system over 60 hand-made materials is miserable
- Per-material stack caps with overflow→currency conversion (§7.4)
- Inventory screen; wire up the 🎒 stub button

**Phase 4 — crafting**
- Recipes seeded from JSON, timed crafts, gear with modifiers
- The first real material sink

**Phase 5 — the idle layer**
- Claims, Buildings, Workers, offline accrual, the welcome-back screen
- Terrain as multiplier, never gate (§5.2)

**Phase 6 — retention systems**
- Worker Expeditions (§5.4) — highest value of the four; makes past travel permanently
  valuable and is nearly free given the visit log already exists
- Patrol Routes (§5.7) — protects the daily-walk habit from diminishing returns
- Uncharted Transit (§7.1), replacing the Phase 1 interim cap
- District synergies (§5.5), Resource Surges (§5.6)

**Phase 7 — depth**
- Rural/urban balancing, prestige, seasons, leaderboards by total level

Phases 1–2 are the ones that convert this from "tech demo" to "game." Phase 6 is what makes
it survive contact with month two.

**A sequencing caveat worth stating plainly.** The cold-start fix (§3.1) says level 1 must
have materials, inventory and a working worker — but those are Phases 3 and 5. The phase
order is an *engineering* sequence, not a release plan: **do not ship to real players until
Phases 3 and 5 exist**, or the first-session experience is the empty map-painting utility
the cold-start note warns about. Either treat Phases 1–5 as one release, or build a stripped
vertical slice of materials and one worker earlier.

---

## 9. Open questions

Things I'd want decided before Phase 3, flagged rather than assumed:

1. ~~**Is there combat?**~~ **DECIDED — encounters. See §5C.**
2. **Multiplayer?** Claims imply territory, territory implies contest. Shared-world claim
   competition is compelling but a big scope jump and a moderation burden. Single-player
   with leaderboards is the safe v1.
3. **Energy/stamina?** Most location games gate with one. Given "idle," probably not —
   the offline cap already does the pacing work.
4. **Monetisation, if any?** It shapes the offline cap and worker slots more than
   anything else in this doc. Better decided early than retrofitted.

5. ~~**Trading's coin economy.**~~ **DECIDED and BUILT — see §5D.**

   Coin is earned by selling materials at Trading POIs, while standing at one. Prices are
   derived from tier and category rather than listed. Banking gained deposits and a
   deliberately tiny interest rate, bounded by the offline cap.

   The investigation changed the question. Dust already *was* the second currency with no
   job — seeded, produced by two sources, and spendable on nothing — so the real choice was
   not whether to add one but what to do about the one already there. Stack caps went with
   it (§7.4), because overflow-into-Dust was a deletion dressed as a conversion.

   **What remains open is the sink.** Upkeep is still paid in food alone and nothing else
   has a coin price. That is the next question, and it should be designed rather than
   accreted.

---

## 10. What this replaces

For the record, since the history is confusing:

**Streets.** An earlier design had `Streets`, `UserStreetProgress` with linear-referenced
`CoveredMin/MaxFraction`, and street conquest as the progression spine. Removed in
`20260504154606`. **Streets are not coming back** — Claims (§5.1) serve the same
"completable objective" role using the cell grid already in place, without a second
geometry system to import and maintain.

**Discovery-based skill unlocks.** An earlier draft of *this document* had skills unlock by
visiting a matching POI. Replaced by the account-level ladder (§3.1) because it made access
to game systems depend on the player's postcode, and produced a specific bad failure —
unlocking a skill at a one-off rare POI and then being unable to train it. Recorded here so
the idea isn't independently reinvented; the reasoning against it is in the §3.1 callout.

Three stale references to the old model remain and should be cleaned up:
- Three permission strings in [app.json](geoslayer.app/app.json) promising "street progress"
- The `StreetImportService` docstring reference in [PoiImportService.cs:15](GeoSlayer.Domain/Services/PoiImportService.cs#L15)
- The `financemanagercontainer` check in the debug seed guard ([Program.cs:154](GeoSlayer/Program.cs#L154)) — template leftover, unrelated but equally stale
—