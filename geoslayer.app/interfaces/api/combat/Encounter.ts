/** An encounter waiting at a POI (DESIGN.md §5C). */
export interface Encounter {
  id: number;
  key: string;
  name: string;
  description: string;
  poiId: number;
  poiName: string;
  latitude: number;
  longitude: number;
  tier: number;
  minCombatLevel: number;
  /** Permanent when true — the reliable route (§5C.1). */
  isTrainingGround: boolean;
  /** Null for a training ground, which never lapses. */
  expiresUtc: string | null;
  winChance: number;
  distanceMetres: number;
  isInRange: boolean;
}
