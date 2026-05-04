import AsyncStorage from '@react-native-async-storage/async-storage';
import * as Location from 'expo-location';
import * as TaskManager from 'expo-task-manager';

import { API_BASE_URL } from '@/constants/api';
import { keychainHelper } from './keychainHelper';

/* ------------------------------------------------------------------ */
/*  Constants                                                          */
/* ------------------------------------------------------------------ */

export const BG_LOCATION_TASK = 'background-location-task';
const BG_BREADCRUMBS_KEY = 'bg_breadcrumbs';
const BG_PLAYER_KEY = 'gs_player';

/** Minimum distance between breadcrumb points (metres). */
const MIN_DISTANCE_M = 3;

/* ------------------------------------------------------------------ */
/*  Breadcrumb persistence (shared between bg task & foreground)       */
/* ------------------------------------------------------------------ */

export interface Coord {
  latitude: number;
  longitude: number;
  timestamp: number;
}

/** Append breadcrumbs gathered in the background. */
async function appendBreadcrumbs(coords: Coord[]): Promise<void> {
  try {
    const raw = await AsyncStorage.getItem(BG_BREADCRUMBS_KEY);
    const existing: Coord[] = raw ? JSON.parse(raw) : [];
    existing.push(...coords);
    // Cap at 5 000 to avoid memory issues on very long walks
    const capped = existing.length > 5000 ? existing.slice(-5000) : existing;
    await AsyncStorage.setItem(BG_BREADCRUMBS_KEY, JSON.stringify(capped));
  } catch {
    // Best-effort
  }
}

/** Read & clear breadcrumbs collected while in the background. */
export async function drainBackgroundBreadcrumbs(): Promise<Coord[]> {
  try {
    const raw = await AsyncStorage.getItem(BG_BREADCRUMBS_KEY);
    if (!raw) return [];
    await AsyncStorage.removeItem(BG_BREADCRUMBS_KEY);
    return JSON.parse(raw);
  } catch {
    return [];
  }
}

/* ------------------------------------------------------------------ */
/*  Haversine (duplicated so the bg task has zero React dependencies)  */
/* ------------------------------------------------------------------ */

function haversineMetres(a: Coord, b: Coord): number {
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

/* ------------------------------------------------------------------ */
/*  Background task definition                                         */
/* ------------------------------------------------------------------ */

TaskManager.defineTask(BG_LOCATION_TASK, async ({ data, error }) => {
  if (error) return;
  if (!data) return;

  const { locations } = data as { locations: Location.LocationObject[] };
  if (!locations || locations.length === 0) return;

  // Gather breadcrumbs
  const newCrumbs: Coord[] = [];
  let prev: Coord | null = null;
  try {
    const raw = await AsyncStorage.getItem(BG_BREADCRUMBS_KEY);
    const existing: Coord[] = raw ? JSON.parse(raw) : [];
    prev = existing.length > 0 ? existing[existing.length - 1] : null;
  } catch {
    // ignore
  }

  for (const loc of locations) {
    const coord: Coord = {
      latitude: loc.coords.latitude,
      longitude: loc.coords.longitude,
      timestamp: loc.timestamp,
    };
    if (prev && haversineMetres(prev, coord) < MIN_DISTANCE_M) continue;
    newCrumbs.push(coord);
    prev = coord;
  }

  if (newCrumbs.length > 0) {
    await appendBreadcrumbs(newCrumbs);
  }

  // Sync with backend using the latest position (reveals fog cells)
  const latest = locations[locations.length - 1];
  try {
    const [accessToken, playerRaw] = await Promise.all([
      keychainHelper.getAccessToken(),
      AsyncStorage.getItem(BG_PLAYER_KEY),
    ]);
    if (!accessToken || !playerRaw) return;

    const player = JSON.parse(playerRaw);

    await fetch(`${API_BASE_URL}/api/journey/sync`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${accessToken}`,
      },
      body: JSON.stringify({
        latitude: latest.coords.latitude,
        longitude: latest.coords.longitude,
        playerId: player.playerId,
        timestampMs: latest.timestamp,
      }),
    });
  } catch {
    // Network error in background — will retry next tick
  }
});

/* ------------------------------------------------------------------ */
/*  Start / stop helpers (called from foreground)                      */
/* ------------------------------------------------------------------ */

export async function startBackgroundLocation(): Promise<boolean> {
  const { status: fgStatus } = await Location.requestForegroundPermissionsAsync();
  if (fgStatus !== 'granted') return false;

  const { status: bgStatus } = await Location.requestBackgroundPermissionsAsync();
  if (bgStatus !== 'granted') return false;

  const isRunning = await TaskManager.isTaskRegisteredAsync(BG_LOCATION_TASK);
  if (isRunning) return true;

  await Location.startLocationUpdatesAsync(BG_LOCATION_TASK, {
    accuracy: Location.Accuracy.High,
    distanceInterval: 5,
    deferredUpdatesInterval: 10_000,
    showsBackgroundLocationIndicator: true,
    foregroundService: {
      notificationTitle: 'GeoSlayer',
      notificationBody: 'Tracking your adventure...',
      notificationColor: '#39ff14',
    },
  });

  return true;
}

export async function stopBackgroundLocation(): Promise<void> {
  const isRunning = await TaskManager.isTaskRegisteredAsync(BG_LOCATION_TASK);
  if (isRunning) {
    await Location.stopLocationUpdatesAsync(BG_LOCATION_TASK);
  }
}
