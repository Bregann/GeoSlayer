/**
 * The item enums, mirrored from GeoSlayer.Domain.Enums.
 *
 * Kept as ordered arrays rather than TypeScript enums so a dropdown can render names while
 * the API receives the numeric value it expects. **Order matters** — these must match the
 * C# declaration order, because that is what the numbers mean.
 */

export const ItemKinds = ['Gear', 'Tool', 'Building'] as const

export const ItemSlots = ['None', 'Head', 'Body', 'Feet', 'Trinket', 'Tool'] as const

export const ItemModifiers = [
  'SkillXpPercent',
  'RevealRadius',
  'PoiRangeMetres',
  'OfflineCapHours',
  'SellPricePercent',
  'WorkerRatePercent',
  'ToolTier',
  'GatherSpeedPercent',
  'CombatPowerLevels',
] as const

export const MaterialCategories = [
  'Dust',
  'Woodland',
  'Water',
  'Farmland',
  'Urban',
  'Industrial',
  'Rocky',
  'Coastal',
  'Foraged',
  'Caught',
  'Logged',
  'Cooked',
  'Mined',
  'Grown',
  'Traded',
  'Sacred',
  'Written',
  'Remedy',
  'Athletic',
  'Brewed',
  'Coin',
  'Martial',
  'Forged',
  'Relic',
] as const

export const SkillTypes = [
  'Prayer',
  'Knowledge',
  'Woodcutting',
  'Fishing',
  'Healing',
  'Athletics',
  'Tavern',
  'Trading',
  'Banking',
  'Combat',
  'Mining',
  'Farming',
  'Smithing',
  'Cooking',
  'Exploration',
  'Foraging',
] as const

export const MuseumWings = [
  'Cartography',
  'Landmarks',
  'Naturalist',
  'Skills',
  'Rarities',
  'Feats',
  'Relics',
  'Expeditions',
] as const

export const MuseumRarities = ['Common', 'Uncommon', 'Rare', 'Legendary'] as const

export const UnlockTypes = ['Skill', 'System'] as const

/**
 * What a sprite belongs to.
 *
 * Ordinals are stored in the database, so this must never be reordered — an insert in the
 * middle would repoint every existing sprite at a different kind of thing.
 */
export const SpriteOwners = ['Item', 'Material', 'Encounter', 'MuseumEntry'] as const

/**
 * The wire value for a sprite owner, by name.
 *
 * Used instead of a literal at call sites so the parity test protects them: hard-coding
 * `ownerType={1}` would keep compiling if the enum were reordered, and would then upload
 * material art against encounters.
 */
export const spriteOwner = (name: (typeof SpriteOwners)[number]): number =>
  SpriteOwners.indexOf(name)

/** Options shaped for a Mantine Select. */
export const asOptions = (names: readonly string[]) =>
  names.map((label, value) => ({ value: String(value), label }))
