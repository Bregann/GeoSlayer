/**
 * Presentation logic for the Museum (Stage 12 task 4).
 *
 * §5A.1 is explicit that framing matters: "a collection log is a completionist's
 * spreadsheet; a Museum is a place you built". So this helper is deliberately opinionated
 * about *showing gaps* — empty plinths pull harder than empty checkboxes, and a UI that
 * hid them would be the spreadsheet.
 */

export type MuseumRarity = 'Common' | 'Uncommon' | 'Rare' | 'Legendary';

export interface MuseumEntry {
  key: string;
  wing: string | number;
  name: string;
  description: string;
  rarity: MuseumRarity | number;
  unlockCondition: string;
  isFound: boolean;
  firstAcquiredUtc: string | null;
  acquiredAtName: string | null;
  quantity: number;
  donatableQuantity: number;
}

export interface MuseumWing {
  wing: string | number;
  name: string;
  found: number;
  total: number;
  isComplete: boolean;
  entries: MuseumEntry[];
}

export interface Museum {
  wings: MuseumWing[];
  totalFound: number;
  totalEntries: number;
  curation: number;
}

const RARITY_NAMES = ['Common', 'Uncommon', 'Rare', 'Legendary'] as const;

/** Rarity as a name, whether the API sent an ordinal or a string. */
export function rarityName(rarity: MuseumRarity | number): string {
  return typeof rarity === 'number' ? (RARITY_NAMES[rarity] ?? 'Common') : rarity;
}

/** Colour per rarity, so a Legendary plinth reads differently at a glance. */
export function rarityColour(rarity: MuseumRarity | number): string {
  switch (rarityName(rarity)) {
    case 'Legendary': return '#ffcc00';
    case 'Rare': return '#bb66ff';
    case 'Uncommon': return '#39ff14';
    default: return '#c8c8e0';
  }
}

export const WING_ICONS: Record<string, string> = {
  Cartography: '🗺️',
  Landmarks: '🏛️',
  Naturalist: '🌿',
  Skills: '⚒️',
  Rarities: '💎',
  Feats: '🏆',
  Relics: '📜',
  Expeditions: '🧭',
};

export function wingIcon(name: string): string {
  return WING_ICONS[name] ?? '📦';
}

/** Completion as a percentage, 0–100. */
export function wingPercent(wing: MuseumWing): number {
  if (wing.total <= 0) return 0;
  return Math.round((wing.found / wing.total) * 100);
}

/**
 * "7 of 48" — §5A.2 calls this out specifically as compelling in a way item lists are
 * not, so it is the primary label rather than a percentage.
 */
export function wingProgress(wing: MuseumWing): string {
  return `${wing.found} of ${wing.total}`;
}

/**
 * The diary line for a found entry: where and when.
 *
 * Returns null when neither is known, so the UI can fall back rather than printing
 * "Found at null".
 */
export function foundLabel(entry: MuseumEntry): string | null {
  if (!entry.isFound) return null;

  const when = entry.firstAcquiredUtc
    ? new Date(entry.firstAcquiredUtc).toLocaleDateString(undefined, {
        day: 'numeric',
        month: 'short',
        year: 'numeric',
      })
    : null;

  if (entry.acquiredAtName && when) return `Found at ${entry.acquiredAtName}, ${when}`;
  if (entry.acquiredAtName) return `Found at ${entry.acquiredAtName}`;
  if (when) return `Found ${when}`;

  return null;
}

/**
 * Wings worth showing, in the order they should appear.
 *
 * A wing with no entries at all (Relics, Expeditions before Stages 13–14) is kept but
 * sorted last: an absent wing reads as "not in this game", an empty one as "not yet".
 */
export function sortWings(wings: MuseumWing[]): MuseumWing[] {
  return [...wings].sort((a, b) => {
    if (a.total === 0 && b.total > 0) return 1;
    if (b.total === 0 && a.total > 0) return -1;
    return b.found - a.found || a.name.localeCompare(b.name);
  });
}

/**
 * Entries ordered so found ones lead and gaps follow.
 *
 * Deliberately *not* hiding the gaps — showing what is missing is the whole mechanic.
 */
export function sortEntries(entries: MuseumEntry[]): MuseumEntry[] {
  return [...entries].sort(
    (a, b) => Number(b.isFound) - Number(a.isFound) || a.name.localeCompare(b.name),
  );
}

/** Overall completion for the header. */
export function overallPercent(museum: Museum): number {
  if (museum.totalEntries <= 0) return 0;
  return Math.round((museum.totalFound / museum.totalEntries) * 100);
}

/** Entries with spares available to donate. */
export function donatableEntries(museum: Museum): MuseumEntry[] {
  return museum.wings
    .flatMap((w) => w.entries)
    .filter((e) => e.donatableQuantity > 0);
}
