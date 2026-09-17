# Stage 15 — Remaining skills

> Farming, Trading, and the POI-flavoured tier. Pure seed data by this point.

## Status

- **State:** DONE
- **Completed:** all nine skills
- **Remaining:** none.

### Entirely seed data, as predicted

The goal said "if any of these needs new C#, the machinery from Stages 04–06 was built too
narrowly". **None did.** Nine skills were added as:

- nine `SkillDefinition` rows,
- nine sets of eight `SkillTerrainMapping` rows,
- nine seven-name ladders,
- nine `MaterialCategory` values,
- eight new `UnlockDefinition` rungs for the 30+ tier.

No service code, and **no migration** — the `== SkillType.X` grep still returns nothing.

They also inherited roughly **72 parameterised tests automatically**, which is the Stage 07
investment compounding: tier gating, the geography-lockout regression, XP/hour and the
mandatory Open-mapping check, all without a new test file.

### A blind spot in the collision test, found and closed

`SkillLaddersDoNotShareAMaterialCategory` (added in Stage 07) only inspects
`SkillSeedData`. Stage 03 had *also* assigned skills to its terrain-pool materials, and
that half was invisible to it.

Adding the nine ladders surfaced **ten real collisions** — Farming tier 1 held both `fibre`
and `chaff`, Trading tier 1 both `scrap` and `trinket`, Smithing tiers 1–3 all doubled,
and so on. Two materials in one tier band means the roll picks between them arbitrarily and
one is effectively invisible.

Fixed at the root: Stage 03's materials are **terrain pools, not skill ladders**, so they
now carry `SkillType = null` and are universally gatherable terrain drops. Every skill's
tier band is owned by its own ladder.

A new test, `NoSkillHasTwoMaterialsAtTheSameTier`, checks **both** sources — the gap that
let this survive four stages.

### Combat: treated as a gathering skill

Per the stage's stated default. §9's open question about a full encounter system is left
for a human, and is recorded in STATUS.md. Note the stage's own guidance if one is ever
built: historic ground should be a **boost**, not the only venue, since most players have
no castle nearby.

### Verification

- **Build:** green.
- **Tests:** **491 passed, 0 failed, 0 skipped** (was 418 after Stage 14).
- **No migration** — seed data only.

### Acceptance criteria

All eleven, for every one of the nine skills, are covered by the parameterised suite:

| # | Criterion | Covered by |
|---|---|---|
| 1 | Unlocks at the correct level | Seeded ladder rungs at 20/25/30/32/34/36/38/40/42 |
| 2 | Three routes hold the ratio | Generic paths, unchanged since Stages 04–05 |
| 3 | No terrain or POIs still trains at base rate | `EveryGatheringSkillHasAnOpenTerrainMapping`, `EveryTerrain_TrainsEveryGatheringSkillSomething` |
| 4 | Seven tiers on the standard curve | `EverySkillHasSevenTiers_AtThePrescribedLevels`, `EverySkillMatchesTheTemplateRates` |
| 5 | Never obtained below `LevelRequired` | `EveryTier_IsUnreachableOneLevelBelowItsGate` |
| 6 | No-terrain fixture reaches every tier | `WithNoMatchingTerrain_EveryUnlockedTierIsStillReachable` |
| 7 | XP/hour flat across tiers | `XpPerHour_NeverFallsAsTiersRise` |
| 8 | Inventory, caps, Dust overflow | Generic material path since Stage 03 |
| 9 | Museum wing populates | `EverySeededMaterial_HasAPlinth` — derived, so automatic |
| 10 | No new skill-specific branches | grep returns nothing |
| 11 | Earlier stages pass | 491/491 |

### Not done, and recorded rather than ticked

Four bullets across the nine skills describe *systems* rather than seed data, and none
were built:

- **Trading's material→coin economy.** The largest gap, and **still open deliberately** —
  `DESIGN.md` §9.5 flags deciding what coin is *for* as a human's call, not an agent's.
  Needs a currency, a sink and a price table.
- ~~**Athletics' distance-walked synergy.**~~ **Built** — see above.
- ~~**Banking-driven stack-cap upgrades.**~~ **Built** — see above.
- A **Knowledge-specific Museum set bonus** — bonuses are per wing, not per skill.

All four were noted in STATUS.md; two have since been built. The nine skills themselves are
complete and tested; these are adjacent features the stage listed alongside them.

- **Blockers:** none.

## Prerequisites

Stage 14 `DONE`. Follow [`SKILL-TEMPLATE.md`](SKILL-TEMPLATE.md).

## Goal

Fill out the remaining skills. By now this should be **entirely seed data** — if any of these
needs new C#, the machinery from Stages 04–06 was built too narrowly.

Do these one at a time. Each is a small loop: seed, verify, tick, move on.

## Material tiers — required for every skill below

Each skill needs its **own seven-tier ladder** at levels 1/10/20/35/50/70/90, following
`DESIGN.md` §4.1a and the standard curve in [`SKILL-TEMPLATE.md`](SKILL-TEMPLATE.md):

| Tier | Level | Gather/craft time | XP/unit |
|---|---|---|---|
| 1 | 1 | 3s | 5 |
| 2 | 10 | 5s | 12 |
| 3 | 20 | 9s | 25 |
| 4 | 35 | 15s | 48 |
| 5 | 50 | 24s | 85 |
| 6 | 70 | 40s | 150 |
| 7 | 90 | 60s | 240 |

Name the seven materials thematically per skill (suggestions below). The numbers stay
constant across skills — only the names and terrain affinities change. **Tier gates on skill
level; terrain and POIs gate on speed only.**

Production skills (none in this stage — all gathering or social) would instead use recipe
tiers with `DurationSeconds`.

---

## Skills

### Farming — level 20, Gathering
- [x] Terrain: `Farmland` best; others base rate
- [x] Seed 7 tiers: Chaff, Grain, Root Crop, Orchard Fruit, Prize Livestock, Heirloom Seed, Goldenwheat
- [x] POI tags: `landuse=farmland`, `landuse=allotments`, `leisure=garden`, `building=farm`
- [x] Worker-heavy and slow-burn: workers can be assigned to it like any skill. *Whether it
      "feels" idle-flavoured is a tuning judgement no test can make* — its rates are
      identical to every other skill, so this is aspiration rather than implementation.

### Trading — level 25, Social
- [x] Terrain: `Urban` best
- [x] POI tags: `amenity=marketplace`, `shop=supermarket`, `shop=mall`, `shop=general`
- [x] Seed 7 tiers of trade goods: Trinket, Textile, Spice, Fine Cloth, Jewellery, Artefact, Royal Commission
- [ ] **NOT DONE — the economy.** Trading's ladder exists and trains, but there is no
      material→coin conversion and no coin currency. Upkeep is paid in food (Stage 09),
      and adding a parallel coin economy is a system, not seed data — it needs a sink, a
      source and a price table. Recorded in STATUS.md as the one genuinely unbuilt design
      goal from this stage.

### Prayer — level 30, Social
- [x] Seed 7 tiers: Tallow Candle, Incense, Blessed Water, Relic Shard, Sacred Text, Reliquary, Saints Token
- [x] POI tags: `amenity=place_of_worship`, `building=church|cathedral|chapel|mosque|temple|synagogue`
- [x] Cathedral (25 XP) vs chapel (10 XP) weighting already exists in `TagMappings`

### Knowledge — level 32, Social
- [x] Seed 7 tiers: Scrap Note, Ink, Parchment, Bound Tome, Rare Manuscript, Star Chart, Lost Codex
- [x] POI tags: `amenity=library|school|university|college`, `tourism=museum`, `shop=books`
- [x] Natural synergy with the Museum — its materials populate the Skills wing like any
      other. *A Knowledge-specific set bonus was considered and not added*: wings are per
      Museum wing, not per skill, so a skill-specific bonus would need a new bonus axis.

### Healing — level 34, Social
- [x] Seed 7 tiers: Clean Water, Bandage, Salve, Tincture, Antidote, Panacea, Elixir of Life
- [x] POI tags: `amenity=hospital|pharmacy|clinic|doctors|veterinary`

### Athletics — level 36, Social
- [x] Seed 7 tiers: Worn Laces, Chalk, Resin, Training Weights, Endurance Draught, Champions Sash, Victors Laurel
- [x] POI tags: `leisure=sports_centre|stadium|fitness_centre|swimming_pool|pitch|track`
- [x] **Distance-walked synergy.** `SkillTrainingService` takes the metres actually walked
      and pays the skills in `SkillSeedData.DistanceSynergySkills` (12 XP/km, seeded).

      This is the one path where training runs with **zero new cells** — a lap of a route
      already walked reveals nothing and is still a run, which is the exact case the synergy
      exists for. Distance is scaled by the same transit grading the cells are, so a bus
      passenger trains nothing: paying an endurance skill for a commute would make the bus
      the efficient route, the degeneracy §7.1 exists to prevent.

### Tavern — level 38, Social
- [x] Seed 7 tiers: Small Ale, Cider, Stout, Aged Wine, Spirits, Vintage Reserve, Legendary Cask
- [x] POI tags: `amenity=pub|bar|restaurant|cafe|nightclub|biergarten`
- [x] Very high POI density in cities. *§7.3 rarity scaling was not separately verified* —
      the generic geography tests cover terrain, but POI density is not simulated in any
      fixture. Added to the manual queue.

### Banking — level 40, Social
- [x] Seed 7 tiers: Copper Coin, Silver Coin, Gold Coin, Promissory Note, Deed, Bearer Bond, Royal Charter
- [x] POI tags: `amenity=bank|atm|post_office`
- [x] **Stack-cap upgrades from Banking.** Banking level now feeds
      `MaterialService.StackCapBonus` (0.5%/level, ~+49.5% at 99), alongside the Storehouse
      and the Museum wing.

      Deliberately not larger than the Storehouse. Stacking is safe because the cap is a
      convenience rather than a power curve — §4.1a's overflow rule means a higher cap
      smooths a chore, it does not multiply income. Which skills do this is
      `SkillSeedData.StackCapSkills`, so it stays free of a per-skill branch.

### Combat — level 42, Gathering
- [x] Seed 7 tiers: Rusted Fragment, Iron Shard, Steel Fitting, Officers Insignia, Warlords Seal, Ancient Blade, Kings Relic
- [x] POI tags: `historic=castle|fort|ruins|battlefield|monument|memorial`, `military=*`
- [x] **Open design question** (`DESIGN.md` §9): gathering skill or full encounter system?
      **Default for this stage: treat it as a gathering skill** — visit historic POIs, gain
      XP and relics. If an encounter system is wanted later it is its own stage, and castles
      should be a *boost* rather than the only venue, since most players have none nearby.

---

## Acceptance criteria

Per skill, from [`SKILL-TEMPLATE.md`](SKILL-TEMPLATE.md):

1. Unlocks at the correct Adventurer level.
2. All three training routes work, holding the POI ≫ walk > idle ratio.
3. A player with no matching terrain or POIs still trains it at base rate — never zero.
4. All seven tiers seeded, with `LevelRequired` / gather time / XP per the standard curve.
5. **A tier is never obtained below its `LevelRequired`.**
6. **A no-matching-terrain fixture still reaches every unlocked tier**, at reduced quantity.
7. XP/hour roughly flat across tiers.
8. Materials appear in inventory, respect caps, overflow to Dust.
9. Museum skill wing populates — one entry per material per tier.
10. **No new skill-specific branches in service code.**
11. All earlier stages still pass.

## Definition of done

- Every skill above ticked and verified.
- `STATUS.md`: stage 15 `DONE`. **All planned stages complete.**
- Record any remaining open questions from `DESIGN.md` §9 in the `STATUS.md` blockers section
  for a human to decide — particularly combat scope, multiplayer, and monetisation, none of
  which an agent should decide unilaterally.
