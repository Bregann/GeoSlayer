import AsyncStorage from '@react-native-async-storage/async-storage';
import * as Location from 'expo-location';
import * as TaskManager from 'expo-task-manager';

import { API_BASE_URL } from '@/constants/api';
import { keychainHelper } from './keychainHelper';
import { refreshAccessToken } from './tokenRefresh';

/* ------------------------------------------------------------------ */
/*  Constants                                                          */
/* ------------------------------------------------------------------ */

export const BG_LOCATION_TASK = 'background-location-task';
const BG_BREADCRUMBS_KEY = 'bg_breadcrumbs';
const BG_PLAYER_KEY = 'gs_player';
const BG_PENDING_KEY = 'bg_pending_batches';

/** Minimum distance between breadcrumb points (metres). */
const MIN_DISTANCE_M = 3;

/**
 * Cap on unsent batches held for retry.  A walk with no signal should not grow
 * AsyncStorage without bound; past this we drop the oldest.
 */
const MAX_PENDING_BATCHES = 50;

/**
 * Gap between consecutive posts when draining a backlog.  The server enforces a 2s
 * minimum between syncs; this clears it with a margin.
 */
const SYNC_SPACING_MS = 2500;

/**
 * How many batches to send per tick.  At `SYNC_SPACING_MS` apart, this bounds the work
 * to a few seconds so it fits inside the OS's background execution window.
 */
const MAX_BATCHES_PER_TICK = 5;

const delay = (ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms));

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
/*  Pending sync batches (survive a network drop mid-walk)             */
/* ------------------------------------------------------------------ */

/** One GPS fix as the sync endpoint wants it. */
export interface SyncPosition {
  latitude: number;
  longitude: number;
  timestampMs: number;
  accuracy: number | null;
}

async function readPending(): Promise<SyncPosition[][]> {
  try {
    const raw = await AsyncStorage.getItem(BG_PENDING_KEY);
    return raw ? JSON.parse(raw) : [];
  } catch {
    return [];
  }
}

async function writePending(batches: SyncPosition[][]): Promise<void> {
  try {
    // Drop from the tail, not the head: the queue is oldest-first, and the newest
    // ground gets re-covered by the next GPS fix anyway.
    const capped =
      batches.length > MAX_PENDING_BATCHES
        ? batches.slice(0, MAX_PENDING_BATCHES)
        : batches;
    await AsyncStorage.setItem(BG_PENDING_KEY, JSON.stringify(capped));
  } catch {
    // Best-effort — losing the queue is bad but crashing the task is worse.
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
/*  Sync                                                               */
/* ------------------------------------------------------------------ */

/** POST one batch of positions. Returns the HTTP status, or null on a network error. */
async function postBatch(
  positions: SyncPosition[],
  accessToken: string,
): Promise<number | null> {
  try {
    const res = await fetch(`${API_BASE_URL}/api/Journey/Sync`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${accessToken}`,
      },
      // The whole path, not just the last fix — the server sweeps the line between
      // consecutive positions, so dropping the middle would lose most of the walk.
      body: JSON.stringify({ positions }),
    });
    return res.status;
  } catch {
    return null;
  }
}

/**
 * Send every queued batch plus the new one, oldest first.
 *
 * Anything that fails goes back on the queue rather than being dropped: a long walk
 * through a signal dead zone must not cost the player the ground they covered.
 */
async function syncBatches(fresh: SyncPosition[]): Promise<void> {
  const [playerRaw, queued] = await Promise.all([
    AsyncStorage.getItem(BG_PLAYER_KEY),
    readPending(),
  ]);

  const batches = fresh.length > 0 ? [...queued, fresh] : queued;
  if (batches.length === 0) return;

  // `gs_player` is written by contexts/authContext.tsx on login.  Without it the user
  // is not logged in, so hold the batches until they are.
  if (!playerRaw) {
    await writePending(batches);
    return;
  }

  let accessToken = await keychainHelper.getAccessToken();
  if (!accessToken) {
    accessToken = await refreshAccessToken();
  }
  if (!accessToken) {
    await writePending(batches);
    return;
  }

  const unsent: SyncPosition[][] = [];
  let refreshed = false;

  // Spacing means a full backlog would outlive the OS's background execution window.
  // Drain a few per tick; the remainder waits and is sent by the next one.
  const drainCount = Math.min(batches.length, MAX_BATCHES_PER_TICK);

  for (let i = 0; i < drainCount; i++) {
    const batch = batches[i];

    // The server rejects syncs less than 2s apart, so draining a backlog in a tight
    // loop would get every batch after the first thrown away. Space them out.
    if (i > 0) await delay(SYNC_SPACING_MS);

    let status = await postBatch(batch, accessToken);

    // A long walk outlives an access token.  Previously this 401'd into an empty
    // catch and silently lost everything from there on.
    if (status === 401 && !refreshed) {
      refreshed = true;
      const renewed = await refreshAccessToken();

      if (!renewed) {
        // Cannot authenticate at all — keep this batch and everything after it.
        unsent.push(...batches.slice(i, drainCount));
        break;
      }

      accessToken = renewed;
      status = await postBatch(batch, accessToken);
    }

    if (status === null || status === 401 || status >= 500) {
      // Network drop or server trouble — retry this and the rest next tick.
      unsent.push(...batches.slice(i, drainCount));
      break;
    }

    // A 4xx other than 401 means the server rejected this batch on its merits
    // (stale timestamps, failed anti-cheat).  Retrying would never help, so drop it.
  }

  // Batches we never attempted this tick go last, preserving overall order.
  unsent.push(...batches.slice(drainCount));

  await writePending(unsent);
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

  // Sync the whole batch (reveals fog cells along the swept path)
  const positions: SyncPosition[] = locations.map((loc) => ({
    latitude: loc.coords.latitude,
    longitude: loc.coords.longitude,
    timestampMs: loc.timestamp,
    // The server's accuracy cutoff needs this; null means "unknown", not "perfect".
    accuracy: loc.coords.accuracy ?? null,
  }));

  try {
    await syncBatches(positions);
  } catch {
    // syncBatches already persists what it could not send; this catch only stops an
    // unexpected throw from killing the task registration.
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
