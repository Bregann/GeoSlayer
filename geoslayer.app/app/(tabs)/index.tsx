import {
  Camera,
  GeoJSONSource,
  Layer,
  Map,
  type CameraRef,
} from '@maplibre/maplibre-react-native';
import * as Location from 'expo-location';
import { useCallback, useEffect, useRef, useState } from 'react';
import { AppState, StyleSheet, Text, TouchableOpacity, View } from 'react-native';

import { Hud } from '@/components/hud';
import { router } from 'expo-router';
import { PlayerMarker } from '@/components/playerMarker';
import { PoiClusterModal } from '@/components/poiClusterModal';
import { PoiDetailModal } from '@/components/poiDetailModal';
import { PoiMarker } from '@/components/poiMarker';
import { MAP_STYLE_URL } from '@/constants/mapStyle';
import { useAuth } from '@/contexts/authContext';
import { authApiClient } from '@/helpers/apiClient';
import {
  drainBackgroundBreadcrumbs,
  startBackgroundLocation,
} from '@/helpers/backgroundLocation';
import { buildFogGeoJSON, clusterPois, distanceMetres, toBreadcrumbGeoJSON } from '@/helpers/geo';
import { mapScreenStyles as styles, overviewStyles } from '@/styles/mapScreen';
import { pickupStyles } from '@/styles/progression';
import type { CellDto, Coord, NearbyPoi, SyncData } from '@/types/map';
import type { UnlockEvent } from '@/types/progression';
import { UnlockCelebration } from '@/components/unlockCelebration';
import { formatPickups, hasOverflow } from '@/helpers/inventory';
import { shouldShowWelcomeBack, type OfflineAccrual } from '@/helpers/idle';
import { WelcomeBack } from '@/components/welcomeBack';

/* ------------------------------------------------------------------ */
/*  Constants                                                          */
/* ------------------------------------------------------------------ */

const SYNC_INTERVAL_MS = 10_000;
const MIN_DISTANCE_M = 3;
const BREADCRUMB_COLOR = '#39ff14';
const BREADCRUMB_WIDTH = 4;

/* ------------------------------------------------------------------ */
/*  Component                                                          */
/* ------------------------------------------------------------------ */

export default function MapScreen() {
  const cameraRef = useRef<CameraRef>(null);
  const [location, setLocation] = useState<Coord | null>(null);
  const [breadcrumbs, setBreadcrumbs] = useState<Coord[]>([]);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [syncData, setSyncData] = useState<SyncData | null>(null);
  const [revealedCells, setRevealedCells] = useState<CellDto[]>([]);
  const [permissionGranted, setPermissionGranted] = useState(false);
  const [heading, setHeading] = useState(0);
  const [overviewMode, setOverviewMode] = useState(false);
  const [selectedPoi, setSelectedPoi] = useState<NearbyPoi | null>(null);
  const [selectedCluster, setSelectedCluster] = useState<NearbyPoi[] | null>(null);
  const [pendingUnlocks, setPendingUnlocks] = useState<UnlockEvent[]>([]);
  const [pickupText, setPickupText] = useState<string | null>(null);
  const [pickupOverflow, setPickupOverflow] = useState(false);
  const [visitCounts, setVisitCounts] = useState<Record<number, number>>({});
  const [welcomeBack, setWelcomeBack] = useState<OfflineAccrual | null>(null);

  // Pickups fade themselves; requiring a tap to clear ambient feedback would be a chore
  // on a walk. Overflow lingers longer because it costs the player something.
  useEffect(() => {
    if (!pickupText) return;

    const timeout = setTimeout(() => setPickupText(null), pickupOverflow ? 6000 : 3000);
    return () => clearTimeout(timeout);
  }, [pickupText, pickupOverflow]);

  const { player, updatePlayer } = useAuth();

  const locationRef = useRef<Coord | null>(null);
  const hasSyncedOnce = useRef(false);
  const hasLoadedRevealed = useRef(false);
  const isFirstRender = useRef(true);

  // ── Overview mode: zoom out and free camera; closing snaps back ─────
  useEffect(() => {
    if (!cameraRef.current || !location) return;
    if (overviewMode) {
      cameraRef.current.easeTo({
        center: [location.longitude, location.latitude],
        zoom: 13,
        bearing: 0,
        duration: 600,
      });
    } else {
      cameraRef.current.easeTo({
        center: [location.longitude, location.latitude],
        zoom: 16,
        bearing: heading,
        duration: 600,
      });
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [overviewMode]);

  // ── Load all previously-revealed cells on mount ──────────────────
  useEffect(() => {
    if (!player || hasLoadedRevealed.current) return;
    hasLoadedRevealed.current = true;

    (async () => {
      try {
        const res = await authApiClient.get('/api/journey/revealed');
        if (res.status < 400 && Array.isArray(res.data)) {
          setRevealedCells(res.data);
        }
      } catch {
        // Will populate incrementally via sync
      }
    })();
  }, [player]);

  // ── Sync helper ──────────────────────────────────────────────────
  const doSync = useCallback(
    async (loc: Coord) => {
      if (!player) return;
      try {
        const res = await authApiClient.post('/api/journey/sync', {
          positions: [
            {
              latitude: loc.latitude,
              longitude: loc.longitude,
              timestampMs: loc.timestamp,
              accuracy: loc.accuracy ?? null,
            },
          ],
        });
        if (res.status < 400) {
          const data: SyncData = res.data;
          setSyncData(data);

          // Merge newly revealed cells
          if (data.newCells.length > 0) {
            setRevealedCells((prev) => [...prev, ...data.newCells]);

            updatePlayer({
              ...player,
              xp: data.xp,
              level: data.level,
            });
          }

          // Crossing a ladder rung is the payoff for several sessions of walking, so it
          // interrupts with a full screen rather than a toast (DESIGN.md §3.1c).
          // Queued rather than replaced: a long background batch can cross more than one.
          if (data.unlocks && data.unlocks.length > 0) {
            setPendingUnlocks((prev) => [...prev, ...data.unlocks]);
          }

          // Materials picked up this sync. Replaced rather than queued: the newest
          // pickup is the interesting one, and a backlog of toasts would obscure the map.
          // Only surfaced when something meaningful accrued, so returning to an empty
          // screen never trains the player to dismiss it unread (§5 task 4).
          if (shouldShowWelcomeBack(data.offlineAccrual)) {
            setWelcomeBack(data.offlineAccrual);
          }

          if (data.materials && data.materials.length > 0) {
            const text = formatPickups(data.materials);
            if (text) {
              setPickupText(text);
              setPickupOverflow(hasOverflow(data.materials));
            }
          }
        }
      } catch {
        // Network error – optimistic UI keeps rendering
      }
    },
    [player, updatePlayer],
  );

  // ── Request location permission on mount ─────────────────────────
  useEffect(() => {
    (async () => {
      const { status } = await Location.requestForegroundPermissionsAsync();
      if (status !== 'granted') {
        setErrorMsg('Location permission denied');
        return;
      }
      setPermissionGranted(true);
      startBackgroundLocation();
    })();
  }, []);

  // ── Rehydrate breadcrumbs when returning from background ─────────
  useEffect(() => {
    const sub = AppState.addEventListener('change', async (state) => {
      if (state === 'active') {
        const bgCrumbs = await drainBackgroundBreadcrumbs();
        if (bgCrumbs.length > 0) {
          setBreadcrumbs((prev) => [...prev, ...bgCrumbs]);
        }
        const loc = locationRef.current;
        if (loc && player) {
          doSync(loc);
        }
      }
    });
    return () => sub.remove();
  }, [player, doSync]);

  // ── Start GPS + compass tracking once permission is granted ──────
  useEffect(() => {
    if (!permissionGranted) return;
    let sub: Location.LocationSubscription | undefined;
    let headingSub: Location.LocationSubscription | undefined;

    (async () => {
      const initial = await Location.getCurrentPositionAsync({
        accuracy: Location.Accuracy.High,
      });
      const start: Coord = {
        latitude: initial.coords.latitude,
        longitude: initial.coords.longitude,
        timestamp: initial.timestamp,
        accuracy: initial.coords.accuracy ?? null,
      };
      setLocation(start);
      locationRef.current = start;
      setBreadcrumbs([start]);

      // Allow animation after the first position is set (skip fly-over on load)
      setTimeout(() => { isFirstRender.current = false; }, 500);

      if (!hasSyncedOnce.current) {
        hasSyncedOnce.current = true;
        doSync(start);
      }

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
            timestamp: loc.timestamp,
            accuracy: loc.coords.accuracy ?? null,
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

      // Track compass heading — heavily smoothed with LERP to prevent wobble
      let smoothHeading = 0;
      headingSub = await Location.watchHeadingAsync((h) => {
        const target = h.trueHeading;
        // Shortest-path angular difference
        let delta = ((target - smoothHeading + 540) % 360) - 180;
        // Only move 15% toward the target each update (heavy smoothing)
        smoothHeading = (smoothHeading + delta * 0.15 + 360) % 360;
        setHeading((prev) => {
          // Only push to state if visible change (>2°)
          const diff = Math.abs(((smoothHeading - prev + 540) % 360) - 180);
          return diff > 2 ? smoothHeading : prev;
        });
      });
    })();

    return () => {
      sub?.remove();
      headingSub?.remove();
    };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [permissionGranted]);

  // ── Throttled backend sync ───────────────────────────────────────
  useEffect(() => {
    if (!player) return;

    const interval = setInterval(() => {
      const loc = locationRef.current;
      if (!loc) return;
      doSync(loc);
    }, SYNC_INTERVAL_MS);

    return () => clearInterval(interval);
  }, [player, doSync]);

  // Union all revealed circles — recalculated only when revealedCells changes.
  // Unioning prevents winding-rule dark patches where circles overlap.
  // Must be before the early return to avoid conditional hook call.

  // ── Render ───────────────────────────────────────────────────────
  if (!location) {
    return (
      <View style={styles.loading}>
        <Text style={styles.loadingText}>
          {errorMsg ?? 'Acquiring GPS signal...'}
        </Text>
      </View>
    );
  }

  // Union all revealed circles — recalculated only when revealedCells changes.
  // Unioning prevents winding-rule dark patches where circles overlap.
  const fogGeoJSON = buildFogGeoJSON(revealedCells);
  const breadcrumbGeoJSON = toBreadcrumbGeoJSON(breadcrumbs);
  const clusteredPois = clusterPois(syncData?.nearbyPois ?? []);

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
          center={overviewMode ? undefined : [location.longitude, location.latitude]}
          zoom={overviewMode ? undefined : 16}
          bearing={overviewMode ? undefined : heading}
          duration={isFirstRender.current ? 0 : 600}
          easing="ease"
          minZoom={overviewMode ? undefined : 16}
          maxZoom={overviewMode ? undefined : 16}
        />

        {/* Fog-of-war overlay */}
        <GeoJSONSource id="fog-source" data={fogGeoJSON}>
          <Layer
            id="fog-fill"
            type="fill"
            paint={{
              'fill-color': '#0a0a1e',
              'fill-opacity': 0.92,
            }}
          />
        </GeoJSONSource>

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
        {clusteredPois.map((poi) => (
          <PoiMarker
            key={poi.id}
            id={poi.id}
            name={
              poi.clusteredPois
                ? `${poi.clusteredPois.length} places`
                : poi.name
            }
            skill={poi.skill}
            coordinate={[poi.longitude, poi.latitude]}
            inRange={poi.inRange}
            clusterCount={poi.clusteredPois?.length}
            onPress={() => {
              if (poi.clusteredPois) {
                setSelectedCluster(poi.clusteredPois);
              } else {
                setSelectedPoi(poi);
              }
            }}
          />
        ))}

        {/* Player marker */}
        <PlayerMarker coordinate={[location.longitude, location.latitude]} />
      </Map>

      {/* Overview toggle button */}
      <TouchableOpacity
        style={overviewStyles.overviewButton}
        onPress={() => setOverviewMode((prev) => !prev)}
        activeOpacity={0.7}
      >
        <Text style={overviewStyles.overviewText}>
          {overviewMode ? '📍 CLOSE' : '🗺️ MAP'}
        </Text>
      </TouchableOpacity>

      {/* HUD overlay */}
      <Hud
        xp={syncData?.xp ?? player?.xp ?? 0}
        level={syncData?.level ?? player?.level ?? 1}
        cellsRevealed={revealedCells.length}
        onInventory={() => router.push('/inventory')}
        onSkills={() => router.push('/skills')}
        onUpgrades={() => router.push('/upgrades')}
        onCrafting={() => router.push('/crafting')}
        onMuseum={() => router.push('/museum')}
      />

      {/* Material pickups from this sync (Stage 03 task 5) */}
      {pickupText && (
        <TouchableOpacity
          style={pickupStyles.container}
          onPress={() => setPickupText(null)}
          activeOpacity={0.8}
        >
          <Text style={pickupStyles.text}>{pickupText}</Text>
          {pickupOverflow && (
            <Text style={pickupStyles.overflow}>Stack full — converted to Dust</Text>
          )}
        </TouchableOpacity>
      )}

      {/* Welcome back — what workers produced while away (Stage 05 task 4) */}
      <WelcomeBack accrual={welcomeBack} onDismiss={() => setWelcomeBack(null)} />

      {/* Unlock celebration (§3.1c) */}
      <UnlockCelebration
        unlocks={pendingUnlocks}
        onDismiss={() => setPendingUnlocks([])}
      />

      {/* POI detail modal */}
      <PoiDetailModal
        poi={selectedPoi}
        visitCount={selectedPoi ? (visitCounts[selectedPoi.id] ?? 0) : 0}
        onClose={() => setSelectedPoi(null)}
        onVisited={(result) => {
          // Track locally so the decay preview is right on a second look without
          // waiting for the next sync to tell us.
          setVisitCounts((prev) => ({ ...prev, [result.poiId]: result.visitCount }));

          if (result.materials.length > 0) {
            const text = formatPickups(result.materials);
            if (text) {
              setPickupText(text);
              setPickupOverflow(hasOverflow(result.materials));
            }
          }

          if (result.unlocks.length > 0) {
            setPendingUnlocks((prev) => [...prev, ...result.unlocks]);
          }
        }}
      />

      {/* Cluster list modal */}
      <PoiClusterModal
        pois={selectedCluster}
        onSelect={(poi) => setSelectedPoi(poi)}
        onClose={() => setSelectedCluster(null)}
      />
    </View>
  );
}
