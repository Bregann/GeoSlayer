import type { MaterialGain } from './inventory';
import type { OfflineAccrual } from '../helpers/idle';
import type { UnlockEvent } from './progression';

export interface Coord {
  latitude: number;
  longitude: number;
  timestamp: number;
  /** Horizontal accuracy in metres. Optional — older stored breadcrumbs lack it. */
  accuracy?: number | null;
}

export interface CellDto {
  gridLat: number;
  gridLng: number;
  south: number;
  west: number;
  north: number;
  east: number;
}

export interface SyncData {
  newCells: CellDto[];
  /** Cumulative Adventurer XP (DESIGN.md §3.0). */
  xp: number;
  /** Adventurer level. */
  level: number;
  /** Ladder rungs crossed by this sync, for the celebration (§3.1c). */
  unlocks: UnlockEvent[];
  /** Bonus Points granted by this sync's level-ups. */
  bonusPointsGranted: number;
  /** Materials picked up this sync (Stage 03). */
  materials: MaterialGain[];
  /** What workers produced while away, or null when nothing did (Stage 05). */
  offlineAccrual: OfflineAccrual | null;
  nearbyPois: NearbyPoi[];
}

export interface NearbyPoi {
  id: number;
  name: string;
  skill: string;
  latitude: number;
  longitude: number;
  xpReward: number;
  distanceMetres: number;
  inRange: boolean;
}

export interface ClusteredPoi extends NearbyPoi {
  clusteredPois?: NearbyPoi[];
}
