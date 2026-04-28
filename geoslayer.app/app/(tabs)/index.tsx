import {
  Camera,
  GeoJSONSource,
  Layer,
  Map,
  type CameraRef,
} from '@maplibre/maplibre-react-native';
import * as Location from 'expo-location';
import { useEffect, useRef, useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';

import { Hud } from '@/components/hud';
import { PlayerMarker } from '@/components/playerMarker';
import { PoiMarker } from '@/components/poiMarker';
import { MAP_STYLE_URL } from '@/constants/mapStyle';
import { useAuth } from '@/contexts/authContext';
import { authApiClient } from '@/helpers/apiClient';
import { mapScreenStyles as styles } from '@/styles/mapScreen';

const BREADCRUMB_COLOR = '#39ff14';
const BREADCRUMB_WIDTH = 4;

/** Minimum distance (metres) between breadcrumb points to avoid jitter. */
const MIN_DISTANCE_M = 3;

/** How often to sync with the backend (ms). */
const SYNC_INTERVAL_MS = 10_000;

interface Coord {
  latitude: number;
  longitude: number;
}

interface SyncData {
  streetName: string | null;
  percentComplete: number;
  justConquered: boolean;
  xp: number;
  level: number;
  nearbyPois: NearbyPoi[];
}

interface NearbyPoi {
  id: number;
  name: string;
  skill: string;
  latitude: number;
  longitude: number;
  xpReward: number;
  distanceMetres: number;
  inRange: boolean;
}

function distanceMetres(a: Coord, b: Coord): number {
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

/** Convert breadcrumb coords to a GeoJSON LineString. */
function toBreadcrumbGeoJSON(coords: Coord[]): GeoJSON.FeatureCollection {
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

export default function MapScreen() {
  const cameraRef = useRef<CameraRef>(null);
  const [location, setLocation] = useState<Coord | null>(null);
  const [breadcrumbs, setBreadcrumbs] = useState<Coord[]>([]);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [syncData, setSyncData] = useState<SyncData | null>(null);
  const [permissionGranted, setPermissionGranted] = useState(false);

  const { player } = useAuth();

  // Keep a ref so the sync interval always reads the latest position
  // without causing the interval to be recreated on every GPS tick.
  const locationRef = useRef<Coord | null>(null);

  // ── Request location permission on mount ─────────────────────────
  useEffect(() => {
    (async () => {
      const { status } = await Location.requestForegroundPermissionsAsync();
      if (status !== 'granted') {
        setErrorMsg('Location permission denied');
        return;
      }
      setPermissionGranted(true);
    })();
  }, []);

  // ── Start GPS tracking once permission is granted ────────────────
  useEffect(() => {
    if (!permissionGranted) return;
    let sub: Location.LocationSubscription | undefined;

    (async () => {
      // Get initial position
      const initial = await Location.getCurrentPositionAsync({
        accuracy: Location.Accuracy.High,
      });
      const start: Coord = {
        latitude: initial.coords.latitude,
        longitude: initial.coords.longitude,
      };
      setLocation(start);
      locationRef.current = start;
      setBreadcrumbs([start]);

      // Subscribe to real-time updates
      sub = await Location.watchPositionAsync(
        {
          accuracy: Location.Accuracy.High,
          distanceInterval: 2,
          timeInterval: 1000,
        },
        (loc) => {
          const next: Coord = {
            latitude: loc.coords.latitude,
            longitude: loc.coords.longitude,
          };
          setLocation(next);
          locationRef.current = next;
          setBreadcrumbs((prev) => {
            const last = prev[prev.length - 1];
            if (last && distanceMetres(last, next) < MIN_DISTANCE_M) return prev;
            return [...prev, next];
          });
        },
      );
    })();

    return () => {
      sub?.remove();
    };
  }, [permissionGranted]);

  // ── Throttled backend sync (every 10 s) ──────────────────────────
  useEffect(() => {
    if (!player) return;

    const interval = setInterval(async () => {
      const loc = locationRef.current;
      if (!loc) return;

      try {
        const res = await authApiClient.post('/api/journey/sync', {
          latitude: loc.latitude,
          longitude: loc.longitude,
          playerId: player.playerId,
        });

        if (res.status < 400) {
          setSyncData(res.data);
        }
      } catch {
        // Network error – optimistic UI keeps rendering the breadcrumb trail
      }
    }, SYNC_INTERVAL_MS);

    return () => clearInterval(interval);
  }, [player]);

  if (!location) {
    return (
      <View style={styles.loading}>
        <Text style={styles.loadingText}>{errorMsg ?? 'Acquiring GPS signal...'}</Text>
      </View>
    );
  }

  const breadcrumbGeoJSON = toBreadcrumbGeoJSON(breadcrumbs);

  return (
    <View style={styles.container}>
      <Map
        style={StyleSheet.absoluteFillObject}
        mapStyle={MAP_STYLE_URL}
        logo={false}
        attribution={false}
        compass={false}
      >
        <Camera
          ref={cameraRef}
          initialViewState={{
            center: [location.longitude, location.latitude],
            zoom: 16,
          }}
        />

        {/* Breadcrumb trail */}
        <GeoJSONSource id="breadcrumb-source" data={breadcrumbGeoJSON}>
          <Layer
            id="breadcrumb-line"
            type="line"
            layout={{
              'line-join': 'round',
              'line-cap': 'round',
            }}
            paint={{
              'line-color': BREADCRUMB_COLOR,
              'line-width': BREADCRUMB_WIDTH,
            }}
          />
        </GeoJSONSource>

        {/* POI markers */}
        {(syncData?.nearbyPois ?? []).map((poi) => (
          <PoiMarker
            key={poi.id}
            id={poi.id}
            name={poi.name}
            skill={poi.skill}
            coordinate={[poi.longitude, poi.latitude]}
            inRange={poi.inRange}
          />
        ))}

        {/* Custom player marker */}
        <PlayerMarker coordinate={[location.longitude, location.latitude]} />
      </Map>

      {/* HUD overlay */}
      <Hud
        hp={85}
        maxHp={100}
        xp={syncData?.xp ?? 0}
        level={syncData?.level ?? 1}
        gold={0}
        streetName={syncData?.streetName ?? null}
        streetProgress={syncData?.percentComplete ?? 0}
        onInventory={() => {}}
        onSkills={() => {}}
        onStreetLog={() => {}}
      />
    </View>
  );
}
