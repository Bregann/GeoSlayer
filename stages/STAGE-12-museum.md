# Stage 12 — The Museum

> Collection log as a place you build. The permanent record of everywhere you've been.

## Status

- **State:** NOT STARTED
- **Completed:** _(none)_
- **Remaining:** all tasks
- **Notes:** _(none)_
- **Blockers:** _(none)_

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

- [ ] `MuseumEntryDefinition` **seeded**: `Key, Wing, Name, Description, Rarity,
      UnlockCondition`.
- [ ] `PlayerMuseumEntry`: `PlayerId, EntryKey, FirstAcquiredUtc, Quantity,
      AcquiredAtPoiId?`, unique `(PlayerId, EntryKey)`.
- [ ] `AcquiredAtPoiId` + timestamp are what make it a **diary rather than a tally** —
      "found at Durham Cathedral, 3 May". Do not omit them.

### 2. Wings

Most of this is derivable from data that already exists. Seed the definitions; hook the
acquisition points.

- [ ] **Cartography** — regions, cities, counties, countries entered. Reverse-geocode on
      first entry to a new region; cache per region. Highest-value wing: "7 of 48 counties"
      is compelling in a way item lists are not.
- [ ] **Landmarks** — POI *types* stood at (~80 entries, straight from
      `PoiImportService.TagMappings`). Near-free: one row on first visit to each type, using
      the `PlayerPoiVisit` log from Stage 04.
- [ ] **Naturalist** — terrain types traversed (~20, from Stage 03 classification).
- [ ] **Skill wings** — one per unlocked skill; materials and crafted items by tier.
- [ ] **Rarities** — low-drop-rate finds.
- [ ] **Feats** — milestones (1000 cells, first level 50, a 20km walk, all four seasons),
      derived from existing counters.
- [ ] Leave `Relics` (Stage 13) and `Expeditions` (Stage 14) defined but empty.

### 3. Rules

- [ ] Entries are **permanent** — never lost, decayed or reset.
- [ ] **First-find counts**, not quantity. The Museum rewards breadth; skills reward depth.
- [ ] **Donate duplicates** for Museum currency — a dignified sink for the Dust/overflow from
      Stage 03.
- [ ] **Set bonuses, sparingly.** Completing a wing grants a modest permanent bonus. Keep
      small: the Museum should be pursued for its own sake, not because it is mandatory.

### 4. App

- [ ] Museum screen with wings, plinths, and visible gaps.
- [ ] Entry detail: what it is, where and when you found it.
- [ ] Acquisition celebration on first-find — smaller than a skill unlock, still a moment.
- [ ] Progress per wing and overall.

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
