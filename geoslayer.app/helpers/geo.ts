import type { Coord, CellDto, NearbyPoi, ClusteredPoi } from '@/types/map';

// ── Haversine distance ────────────────────────────────────────────

export function distanceMetres(a: Coord, b: Coord): number {
  const R = 6371000;
  const dLat = ((b.latitude - a.latitude) * Math.PI) / 180;
  const dLon = ((b.longitude - a.longitude) * Math.PI) / 180;
  const sinLat = Math.sin(dLat / 2);
  const sinLon = Math.sin(dLon / 2);
  const h =
    sinLat * sinLat +
    Math.cos((a.latitude * Math.PI) / 180) *
      Math.cos((b.latitude * Math.PI) / 180) *
      sinLon *
      sinLon;
  return R * 2 * Math.atan2(Math.sqrt(h), Math.sqrt(1 - h));
}

// ── Fog-of-war GeoJSON ────────────────────────────────────────────

/**
 * Build fog-of-war GeoJSON: a world-covering polygon with square holes
 * for each revealed cell.  Squares tile perfectly (no gaps, no overlap)
 * which avoids the non-zero winding artefacts that circles caused.
 */
export function buildFogGeoJSON(cells: CellDto[]): GeoJSON.FeatureCollection {
  const outer: number[][] = [
    [-180, -85],
    [-180, 85],
    [180, 85],
    [180, -85],
    [-180, -85],
  ];

  const holes: number[][][] = cells.map((c) => [
    [c.west, c.south],
    [c.east, c.south],
    [c.east, c.north],
    [c.west, c.north],
    [c.west, c.south],
  ]);

  return {
    type: 'FeatureCollection',
    features: [
      {
        type: 'Feature',
        properties: {},
        geometry: {
          type: 'Polygon',
          coordinates: [outer, ...holes],
        },
      },
    ],
  };
}

// ── Breadcrumb GeoJSON ────────────────────────────────────────────

export function toBreadcrumbGeoJSON(coords: Coord[]): GeoJSON.FeatureCollection {
  return {
    type: 'FeatureCollection',
    features:
      coords.length >= 2
        ? [
            {
              type: 'Feature',
              properties: {},
              geometry: {
                type: 'LineString',
                coordinates: coords.map((c) => [c.longitude, c.latitude]),
              },
            },
          ]
        : [],
  };
}

// ── POI clustering ────────────────────────────────────────────────

const CLUSTER_DISTANCE_M = 30;

export function clusterPois(pois: NearbyPoi[]): ClusteredPoi[] {
  const used = new Set<number>();
  const result: ClusteredPoi[] = [];

  for (let i = 0; i < pois.length; i++) {
    if (used.has(i)) continue;
    const group: NearbyPoi[] = [pois[i]];
    used.add(i);

    for (let j = i + 1; j < pois.length; j++) {
      if (used.has(j)) continue;
      const d = distanceMetres(
        { latitude: pois[i].latitude, longitude: pois[i].longitude, timestamp: 0 },
        { latitude: pois[j].latitude, longitude: pois[j].longitude, timestamp: 0 },
      );
      if (d < CLUSTER_DISTANCE_M) {
        group.push(pois[j]);
        used.add(j);
      }
    }

    if (group.length === 1) {
      result.push(group[0]);
    } else {
      result.push({ ...group[0], clusteredPois: group });
    }
  }

  return result;
}
