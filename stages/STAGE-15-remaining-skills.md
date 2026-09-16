# Stage 15 — Remaining skills

> Farming, Trading, and the POI-flavoured tier. Pure seed data by this point.

## Status

- **State:** NOT STARTED
- **Completed:** _(none)_
- **Remaining:** all tasks
- **Notes:** _(none)_
- **Blockers:** _(none)_

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
- [ ] Terrain: `Farmland` best; others base rate
- [ ] Seed 7 tiers: Chaff, Grain, Root Crop, Orchard Fruit, Prize Livestock, Heirloom Seed, Goldenwheat
- [ ] POI tags: `landuse=farmland`, `landuse=allotments`, `leisure=garden`, `building=farm`
- [ ] Worker-heavy and slow-burn by design — it should feel like the idle-flavoured skill

### Trading — level 25, Social
- [ ] Terrain: `Urban` best
- [ ] POI tags: `amenity=marketplace`, `shop=supermarket`, `shop=mall`, `shop=general`
- [ ] Seed 7 tiers of trade goods: Trinket, Textile, Spice, Fine Cloth, Jewellery, Artefact, Royal Commission
- [ ] Opens the economy: converting materials to coin, coin to upkeep
- [ ] Trading is the natural home for a **material→coin sink**, which the upkeep economy
      (Stage 05) needs

### Prayer — level 30, Social
- [ ] Seed 7 tiers: Tallow Candle, Incense, Blessed Water, Relic Shard, Sacred Text, Reliquary, Saints Token
- [ ] POI tags: `amenity=place_of_worship`, `building=church|cathedral|chapel|mosque|temple|synagogue`
- [ ] Cathedral (25 XP) vs chapel (10 XP) weighting already exists in `TagMappings`

### Knowledge — level 32, Social
- [ ] Seed 7 tiers: Scrap Note, Ink, Parchment, Bound Tome, Rare Manuscript, Star Chart, Lost Codex
- [ ] POI tags: `amenity=library|school|university|college`, `tourism=museum`, `shop=books`
- [ ] Natural synergy with the Museum (Stage 12) — consider a set bonus

### Healing — level 34, Social
- [ ] Seed 7 tiers: Clean Water, Bandage, Salve, Tincture, Antidote, Panacea, Elixir of Life
- [ ] POI tags: `amenity=hospital|pharmacy|clinic|doctors|veterinary`

### Athletics — level 36, Social
- [ ] Seed 7 tiers: Worn Laces, Chalk, Resin, Training Weights, Endurance Draught, Champions Sash, Victors Laurel
- [ ] POI tags: `leisure=sports_centre|stadium|fitness_centre|swimming_pool|pitch|track`
- [ ] Obvious candidate for a distance-walked synergy

### Tavern — level 38, Social
- [ ] Seed 7 tiers: Small Ale, Cider, Stout, Aged Wine, Spirits, Vintage Reserve, Legendary Cask
- [ ] POI tags: `amenity=pub|bar|restaurant|cafe|nightclub|biergarten`
- [ ] Very high POI density in cities — a good place to verify the rural/urban rarity
      scaling from §7.3 actually works

### Banking — level 40, Social
- [ ] Seed 7 tiers: Copper Coin, Silver Coin, Gold Coin, Promissory Note, Deed, Bearer Bond, Royal Charter
- [ ] POI tags: `amenity=bank|atm|post_office`
- [ ] Storage-flavoured: a natural home for stack-cap upgrades

### Combat — level 42, Gathering
- [ ] Seed 7 tiers: Rusted Fragment, Iron Shard, Steel Fitting, Officers Insignia, Warlords Seal, Ancient Blade, Kings Relic
- [ ] POI tags: `historic=castle|fort|ruins|battlefield|monument|memorial`, `military=*`
- [ ] **Open design question** (`DESIGN.md` §9): gathering skill or full encounter system?
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
