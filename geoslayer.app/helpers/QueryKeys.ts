/**
 * Centralised query keys for @tanstack/react-query.
 * Every useQuery / useMutation in the app should reference these keys rather
 * than using raw string literals.
 */
export enum QueryKeys {
  // ── Player ──
  Skills = 'skills',
  Inventory = 'inventory',
  Upgrades = 'upgrades',

  // ── Map / journey ──
  Revealed = 'revealed',

  // ── Idle ──
  Workers = 'workers',
  Claims = 'claims',

  // ── Crafting ──
  Recipes = 'recipes',
  CraftQueue = 'craftQueue',
  Items = 'items',

  // ── Museum ──
  Museum = 'museum',

  // ── Clues ──
  Clues = 'clues',

  // ── Retention ──
  Expeditions = 'expeditions',
  ExpeditionDestinations = 'expeditionDestinations',
  Patrols = 'patrols',
  District = 'district',
  Surges = 'surges',
  Transit = 'transit',
}
