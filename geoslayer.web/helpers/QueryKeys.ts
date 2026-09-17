/**
 * Centralised query keys for @tanstack/react-query.
 *
 * Every useQuery / useMutation references these rather than a raw string, for the reason
 * geoslayer.app does the same: a typo'd key does not error, it silently creates a second
 * cache entry that never invalidates.
 */
export enum QueryKeys {
  // ── Admin ──
  Items = 'items',
  Materials = 'materials',
  Recipes = 'recipes',
  AuditTrail = 'auditTrail',
}
