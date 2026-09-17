import { useQuery } from '@tanstack/react-query';
import * as Location from 'expo-location';
import { router } from 'expo-router';
import { useState } from 'react';
import {
  ActivityIndicator,
  ScrollView,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from 'react-native';

import { authApiClient } from '@/helpers/apiClient';
import { useMutationPost } from '@/helpers/mutations/useMutationPost';
import {
  completionLabel,
  draftBlockedReason,
  emptyStateMessage,
  isReady,
  routeStatus,
  routeSummary,
  sortRoutes,
  type PatrolRoute,
  type PatrolWaypoint,
} from '@/helpers/patrols';
import { progressionStyles as styles } from '@/styles/progression';
import { QueryKeys } from '@/helpers/QueryKeys';

/**
 * Patrol Routes (DESIGN.md §5.7).
 *
 * The commute you already walk, paying for the workers you already have. §5.7 keeps
 * novelty as the only route to *progress* and makes routine the route to *maintenance*,
 * so everything here talks about rations and upkeep and never about XP.
 *
 * Waypoints are captured from where the player is actually standing rather than tapped on
 * a map: a patrol is a walk you make, and marking it while walking it is both the simpler
 * interaction and the honest one.
 */
export default function PatrolsScreen() {
  const [error, setError] = useState<string | null>(null);
  const [name, setName] = useState('');
  const [draft, setDraft] = useState<PatrolWaypoint[]>([]);
  const [capturing, setCapturing] = useState(false);

  const routes = useQuery<PatrolRoute[]>({
    queryKey: [QueryKeys.Patrols],
    queryFn: async () => (await authApiClient.get<PatrolRoute[]>('/api/Retention/GetPatrols')).data,
  });

  const failureMessage = (err: unknown): string => {
    const data = (err as { response?: { data?: unknown } })?.response?.data;
    if (typeof data === 'string' && data) return data;
    return err instanceof Error ? err.message : 'Something went wrong';
  };

  const create = useMutationPost<{ name: string; waypoints: PatrolWaypoint[] }, PatrolRoute>({
    url: '/api/Retention/CreatePatrol',
    queryKey: [QueryKeys.Patrols],
    invalidateQuery: true,
    onSuccess: () => { setError(null); setDraft([]); setName(''); },
    onError: (err) => setError(failureMessage(err)),
  });

  const addWaypoint = async () => {
    setCapturing(true);

    try {
      const { status } = await Location.requestForegroundPermissionsAsync();

      if (status !== 'granted') {
        setError('Location permission is needed to mark a stop.');
        return;
      }

      const fix = await Location.getCurrentPositionAsync({
        accuracy: Location.Accuracy.Balanced,
      });

      setError(null);
      setDraft((prev) => [
        ...prev,
        { latitude: fix.coords.latitude, longitude: fix.coords.longitude },
      ]);
    } catch (err: unknown) {
      setError(failureMessage(err));
    } finally {
      setCapturing(false);
    }
  };

  const blocked = draftBlockedReason(draft);
  const empty = emptyStateMessage(routes.data ?? []);

  return (
    <View style={styles.screen}>
      <View style={styles.header}>
        <View style={styles.headerRow}>
          <Text style={styles.title}>🚶 PATROLS</Text>
          <TouchableOpacity style={styles.backButton} onPress={() => router.back()}>
            <Text style={styles.backText}>← BACK</Text>
          </TouchableOpacity>
        </View>

        <Text style={styles.xpText}>
          A walk you already make, paying rations to feed your workers. Once a day.
        </Text>
      </View>

      {routes.isLoading && (
        <View style={styles.centred}>
          <ActivityIndicator color="#39ff14" />
          <Text style={styles.message}>Loading…</Text>
        </View>
      )}

      {!routes.isLoading && (
        <ScrollView contentContainerStyle={styles.scroll}>
          {error && <Text style={styles.errorText}>{error}</Text>}

          {empty && <Text style={styles.message}>{empty}</Text>}

          {sortRoutes(routes.data ?? []).map((route) => {
            const walked = completionLabel(route);

            return (
              <View key={route.id} style={styles.card}>
                <View style={styles.cardRow}>
                  <Text style={styles.cardName}>{route.name}</Text>
                  {isReady(route) && <Text style={styles.cardLevel}>READY</Text>}
                </View>

                <Text style={styles.cardMeta}>{routeSummary(route)}</Text>
                <Text style={styles.effectText}>{routeStatus(route)}</Text>

                {walked && <Text style={styles.cardMeta}>{walked}</Text>}
              </View>
            );
          })}

          <Text style={styles.sectionTitle}>NEW ROUTE</Text>

          <View style={styles.card}>
            <TextInput
              style={styles.input}
              placeholder="Route name"
              placeholderTextColor="#6a6a8a"
              value={name}
              onChangeText={setName}
            />

            <Text style={styles.cardMeta}>
              {draft.length === 0
                ? 'Walk your route and mark each stop as you reach it.'
                : `${draft.length} stop${draft.length === 1 ? '' : 's'} marked`}
            </Text>

            {blocked && <Text style={styles.effectText}>{blocked}</Text>}

            <TouchableOpacity
              style={styles.buyButton}
              disabled={capturing}
              onPress={addWaypoint}
            >
              <Text style={styles.buyText}>
                {capturing ? 'MARKING…' : '📍 MARK THIS STOP'}
              </Text>
            </TouchableOpacity>

            {draft.length > 0 && (
              <TouchableOpacity style={styles.buyButton} onPress={() => setDraft([])}>
                <Text style={styles.buyText}>CLEAR</Text>
              </TouchableOpacity>
            )}

            {!blocked && (
              <TouchableOpacity
                style={styles.buyButton}
                disabled={create.isPending}
                onPress={() => create.mutate({ name: name.trim() || 'Patrol', waypoints: draft })}
              >
                <Text style={styles.buyText}>SAVE ROUTE</Text>
              </TouchableOpacity>
            )}
          </View>
        </ScrollView>
      )}
    </View>
  );
}
