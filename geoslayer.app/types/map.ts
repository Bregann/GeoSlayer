export interface Coord {
  latitude: number;
  longitude: number;
  timestamp: number;
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
  xp: number;
  level: number;
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
