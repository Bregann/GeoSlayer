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

/** Options shaped for a Mantine Select. */
export const asOptions = (names: readonly string[]) =>
  names.map((label, value) => ({ value: String(value), label }))
