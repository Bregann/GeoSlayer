import type { NearbyPoi } from '@/interfaces/api/journey/NearbyPoi';

/**
 * Client-side map types.
 *
 * API response shapes live in interfaces/api/; these two are the app's own — a device
 * position fix, and the result of clustering markers for display.
 */
export interface Coord {
  latitude: number;
  longitude: number;
  timestamp: number;
  /** Horizontal accuracy in metres. Optional — older stored breadcrumbs lack it. */
  accuracy?: number | null;
}

export interface ClusteredPoi extends NearbyPoi {
  clusteredPois?: NearbyPoi[];
}
