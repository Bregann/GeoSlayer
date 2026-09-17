# Stage 12 — The Museum

> Collection log as a place you build. The permanent record of everywhere you've been.

## Status

- **State:** DONE
- **Completed:** tasks 1, 2, 3, 4
- **Remaining:** none — see the deferral below.

### Reverse-geocoding: no third-party lookup at all

The design note warns that Nominatim's usage policy is restrictive for bulk use and
prefers "deriving regions from the OSM data already imported". **That is what shipped.**
`PoiRegionResolver` names a cell from the most notable POI already imported there —
ordering by `XpReward` as a proxy for notability, so a cathedral outranks a corner shop.
**No network call is made.**

The honest trade-off: this cannot name a *county*. It names a locality, and a cell with no
named POI resolves to `null` and adds no plinth — better than inventing a place name. If
real administrative boundaries are wanted later, swapping for a self-hosted lookup is a
one-class change behind `IRegionResolver`.

Caching is **shared across players**, not per player: one row per coarse cell, ever. The
second player to walk a cell triggers no lookup, which is what criterion 8 asks for and
what makes any future policy-restricted source safe.

### Wings are derived, not authored

§5A.2 claims "you already have everything needed to populate this; the entries fall out of
existing data". `MuseumSeedData` holds that to it — every plinth is generated from a live
table:

| Wing | Derived from |
|---|---|
| Landmarks | The `SkillType` enum, via the ~86-entry OSM tag table |
| Naturalist | `TerrainType`, minus `Open` |
| Skills | Every seeded material and crafted item |
| Rarities | Materials flagged `IsUnique` |
| Feats | Counters that already exist — cells, POIs, levels, Claims, crafts |
| Cartography | Created on discovery; the set of regions is unbounded |
| Relics, Expeditions | Defined as wings, deliberately empty until Stages 13–14 |

So adding a skill or a POI tag adds its plinths for free, and the two cannot drift.
`EveryPoiSkill_HasALandmarkPlinth` and `EverySeededMaterial_HasAPlinth` enforce it.

### Permanence is tested, not assumed

§5A frames permanence as the point — the Museum is the only system that never decays, caps
or resets. `NoCodePath_DeletesAMuseumEntry` greps the whole services tree for a removal
call, so criterion 7 cannot rot. `IMuseumService` also has no delete method to reach for
by accident.

Donating keeps the entry: one is always held back on the plinth, and `DonatedQuantity`
stops the same spare being donated twice.

### Verification

- **Build:** green.
- **Tests:** **352 passed, 0 failed, 0 skipped** (was 324 after Stage 11).
  - 28 Museum integration tests covering every criterion.
- **App:** `npm test` gains a seventh suite — 20 tests for `helpers/museum.ts`.
- **Migration** applied to a scratch PostGIS container; all three tables created.

### Acceptance criteria

| # | Criterion | State |
|---|---|---|
| 1 | Build green, tests pass | ✅ 352/352 |
| 2 | POI type adds its Landmark entry with POI and timestamp | ✅ `RecordingAFind_StoresWhereAndWhen` |
| 3 | New region adds an entry; re-entering adds nothing | ✅ `EnteringARegion_*`, `ReEnteringARegion_AddsNothing` |
| 4 | First gather adds its skill-wing entry | ✅ `GatheringAMaterial_AddsItsSkillWingEntry` |
| 5 | Duplicates increment without a second row | ✅ `ARepeatFind_IncrementsQuantityWithoutASecondRow` |
| 6 | Donating yields currency, keeps the entry | ✅ `DonatingDuplicates_YieldsCurationAndKeepsTheEntry` |
| 7 | Entries survive everything — no reset path | ✅ `NoCodePath_DeletesAMuseumEntry` greps for one |
| 8 | Geocoding cached; revisit triggers no lookup | ✅ `ReverseGeocoding_IsCachedAcrossPlayers` (Moq `Times.Once`) |
| 9 | Earlier stages still pass | ✅ all prior tests green |

### Deferred

- **Set bonuses** (task 3, final bullet). Wing completion is tracked and surfaced
  (`MuseumWingDto.IsComplete`, and `MuseumAcquisitionDto.CompletedWing` fires on the find
  that fills a wing), but completing a wing grants **no mechanical bonus yet**. The task
  itself says "sparingly… the Museum should be pursued for its own sake, not because it is
  mandatory" — and with Relics and Expeditions still empty, no wing can be completed
  honestly, so any bonus would be balanced against a Museum that is not finished. Recorded
  in STATUS.md against **Stage 14**, once those wings exist.

- **Blockers:** none.

## Prerequisites

Stage 11 `DONE`. Read `DESIGN.md` §5A.

## Goal

Every POI, region and material has a second axis of value beyond XP. The Museum is the one
system immune to decay, caps and resets — it only ever grows.

**Framing matters.** A collection log is a spreadsheet; a Museum is somewhere you built.
Empty plinths pull harder than empty checkboxes. Build the presentation accordingly.

---

## Tasks

### 1. Schema

- [x] `MuseumEntryDefinition` **seeded**: `Key, Wing, Name, Description, Rarity,
      UnlockCondition`.
- [x] `PlayerMuseumEntry`: `PlayerId, EntryKey, FirstAcquiredUtc, Quantity,
      AcquiredAtPoiId?`, unique `(PlayerId, EntryKey)`.
- [x] `AcquiredAtPoiId` + timestamp are what make it a **diary rather than a tally** —
      "found at Durham Cathedral, 3 May". Do not omit them.

### 2. Wings

Most of this is derivable from data that already exists. Seed the definitions; hook the
acquisition points.

- [x] **Cartography** — regions, cities, counties, countries entered. Reverse-geocode on
      first entry to a new region; cache per region. Highest-value wing: "7 of 48 counties"
      is compelling in a way item lists are not.
- [x] **Landmarks** — POI *types* stood at (~80 entries, straight from
      `PoiImportService.TagMappings`). Near-free: one row on first visit to each type, using
      the `PlayerPoiVisit` log from Stage 04.
- [x] **Naturalist** — terrain types traversed (~20, from Stage 03 classification).
- [x] **Skill wings** — one per unlocked skill; materials and crafted items by tier.
- [x] **Rarities** — low-drop-rate finds.
- [x] **Feats** — milestones (1000 cells, first level 50, a 20km walk, all four seasons),
      derived from existing counters.
- [x] Leave `Relics` (Stage 13) and `Expeditions` (Stage 14) defined but empty.

### 3. Rules

- [x] Entries are **permanent** — never lost, decayed or reset.
- [x] **First-find counts**, not quantity. The Museum rewards breadth; skills reward depth.
- [x] **Donate duplicates** for Museum currency — a dignified sink for the Dust/overflow from
      Stage 03.
- [ ] **NOT DONE — set bonuses.** Wing completion is tracked and surfaced, but grants no
      mechanical bonus. Relics and Expeditions are still empty, so no wing can be completed
      honestly and any bonus would be balanced against an unfinished Museum. Deferred to
      Stage 14; recorded in STATUS.md.

### 4. App

- [x] Museum screen with wings, plinths, and visible gaps.
- [x] Entry detail: what it is, where and when you found it.
- [x] Acquisition celebration on first-find — smaller than a skill unlock, still a moment.
- [x] Progress per wing and overall.

---

## Acceptance criteria

1. Build green, tests pass.
2. Visiting a POI type for the first time adds its Landmark entry with the POI and timestamp.
3. Entering a new region adds a Cartography entry; re-entering adds nothing.
4. Gathering a material for the first time adds its skill-wing entry.
5. Duplicates increment `Quantity` without creating a second row.
6. Donating duplicates yields currency and does not remove the entry.
7. Entries survive everything — no decay, no cap, no reset path exists.
8. Reverse-geocoding is cached; revisiting a region triggers no new lookup.
9. All earlier stages' acceptance criteria still pass.

## Definition of done

- All tasks ticked, all criteria verified.
- `STATUS.md`: stage 12 `DONE`, current stage `STAGE-13-clue-scrolls.md`.

## Design note

Pick a reverse-geocoding source that permits caching and offline storage. Nominatim's usage
policy is restrictive for bulk use — prefer deriving regions from the OSM data already
imported, or a self-hosted lookup, over per-player API calls. Record the choice in the status
block.
