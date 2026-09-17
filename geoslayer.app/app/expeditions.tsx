import { useQuery } from '@tanstack/react-query';
import { router } from 'expo-router';
import { useState } from 'react';
import { ActivityIndicator, ScrollView, Text, TouchableOpacity, View } from 'react-native';

import { authApiClient } from '@/helpers/apiClient';
import { useMutationPost } from '@/helpers/mutations/useMutationPost';
import { destinationBlockedReason, emptyStateMessage, firstVisitLabel, idleWorkerIds, sortDestinations, sortExpeditions, timeRemaining, tradeOffLabel } from '@/helpers/expeditions';
import { progressionStyles as styles } from '@/styles/progression';
import { QueryKeys } from '@/helpers/QueryKeys';
import type { Expedition } from '@/interfaces/api/retention/Expedition';
import type { ExpeditionDestination } from '@/interfaces/api/retention/ExpeditionDestination';
import type { Worker } from '@/interfaces/api/idle/Worker';

/**
 * Worker Expeditions (DESIGN.md §5.4).
 *
 * The pitch is that a cathedral you visited on holiday is otherwise dead to you forever.
 * Every destination here is somewhere the player personally stood, so the list reads as
 * places you have been rather than a menu of targets — hence the "first found" date on
 * every row.
 *
 * There is no collect button: returns are gathered on sync (JourneyService), which is what
 * keeps the idle layer from punishing you for sleeping (§7.4).
 */
export default function ExpeditionsScreen() {
  const [error, setError] = useState<string | null>(null);
  const [selected, setSelected] = useState<number | null>(null);

  const expeditions = useQuery<Expedition[]>({
    queryKey: [QueryKeys.Expeditions],
    queryFn: async () => (await authApiClient.get<Expedition[]>('/api/Retention/GetExpeditions')).data,
  });

  const destinations = useQuery<ExpeditionDestination[]>({
    queryKey: [QueryKeys.ExpeditionDestinations],
    queryFn: async () =>
      (await authApiClient.get<ExpeditionDestination[]>('/api/Retention/GetDestinations'))
        .data,
  });

  const workers = useQuery<Worker[]>({
    queryKey: [QueryKeys.Workers],
    queryFn: async () => (await authApiClient.get<Worker[]>('/api/Idle/GetWorkers')).data,
  });

  const failureMessage = (err: unknown): string => {
    const data = (err as { response?: { data?: unknown } })?.response?.data;
    if (typeof data === 'string' && data) return data;
    return err instanceof Error ? err.message : 'Something went wrong';
  };

  const dispatch = useMutationPost<{ workerId: number; poiId: number }, Expedition>({
    url: '/api/Retention/Dispatch',
    queryKey: [QueryKeys.Expeditions],
    invalidateQuery: true,
    // Destinations carry IsAvailable, which this dispatch just changed for the POI we
    // sent to. Invalidated explicitly: it used to ride along on the Expeditions prefix
    // when the key was ['player', 'expeditions', 'destinations'], and no longer does.
    alsoInvalidate: [[QueryKeys.Workers], [QueryKeys.ExpeditionDestinations]],
    onSuccess: () => { setError(null); setSelected(null); },
    onError: (err) => setError(failureMessage(err)),
  });

  const isLoading = expeditions.isLoading || destinations.isLoading || workers.isLoading;

  const away = expeditions.data ?? [];
  const available = idleWorkerIds(workers.data ?? [], away);
  const empty = emptyStateMessage(destinations.data ?? []);

  return (
    <View style={styles.screen}>
      <View style={styles.header}>
        <View style={styles.headerRow}>
          <Text style={styles.title}>🧭 EXPEDITIONS</Text>
          <TouchableOpacity style={styles.backButton} onPress={() => router.back()}>
            <Text style={styles.backText}>← BACK</Text>
          </TouchableOpacity>
        </View>

        <Text style={styles.xpText}>
          Send workers back to places you have been. Returns are collected when you next sync.
        </Text>
      </View>

      {isLoading && (
        <View style={styles.centred}>
          <ActivityIndicator color="#39ff14" />
          <Text style={styles.message}>Loading…</Text>
        </View>
      )}

      {!isLoading && (
        <ScrollView contentContainerStyle={styles.scroll}>
          {error && <Text style={styles.errorText}>{error}</Text>}

          {away.length > 0 && (
            <>
              <Text style={styles.sectionTitle}>AWAY</Text>

              {sortExpeditions(away).map((expedition) => (
                <View key={expedition.id} style={styles.card}>
                  <View style={styles.cardRow}>
                    <Text style={styles.cardName}>{expedition.poiName}</Text>
                    <Text style={styles.cardLevel}>{timeRemaining(expedition)}</Text>
                  </View>

                  <Text style={styles.cardMeta}>
                    {expedition.hasReturned
                      ? 'Back — sync to collect the haul'
                      : 'On the road'}
                  </Text>
                </View>
              ))}
            </>
          )}

          <Text style={styles.sectionTitle}>DESTINATIONS</Text>

          {empty && <Text style={styles.message}>{empty}</Text>}

          {sortDestinations(destinations.data ?? []).map((destination) => {
            const blocked = destinationBlockedReason(destination);
            const isOpen = selected === destination.poiId;

            return (
              <View key={destination.poiId} style={styles.card}>
                <View style={styles.cardRow}>
                  <Text style={styles.cardName}>{destination.name}</Text>
                  <Text style={styles.cardLevel}>{destination.skillName}</Text>
                </View>

                <Text style={styles.cardMeta}>{firstVisitLabel(destination)}</Text>
                <Text style={styles.effectText}>{tradeOffLabel(destination)}</Text>

                {blocked && <Text style={styles.cardMeta}>{blocked}</Text>}

                {!blocked && (
                  <TouchableOpacity
                    style={styles.buyButton}
                    onPress={() => setSelected(isOpen ? null : destination.poiId)}
                  >
                    <Text style={styles.buyText}>{isOpen ? 'CANCEL' : 'SEND A WORKER'}</Text>
                  </TouchableOpacity>
                )}

                {isOpen && (
                  <View style={{ gap: 6, marginTop: 6 }}>
                    {available.length === 0 && (
                      <Text style={styles.cardMeta}>
                        Every worker is busy — recall one or hire another.
                      </Text>
                    )}

                    {available.map((workerId) => {
                      const worker = (workers.data ?? []).find((w) => w.id === workerId);

                      return (
                        <TouchableOpacity
                          key={workerId}
                          style={styles.buyButton}
                          disabled={dispatch.isPending}
                          onPress={() =>
                            dispatch.mutate({ workerId, poiId: destination.poiId })
                          }
                        >
                          <Text style={styles.buyText}>
                            SEND {worker?.name ?? `WORKER ${workerId}`}
                          </Text>
                        </TouchableOpacity>
                      );
                    })}
                  </View>
                )}
              </View>
            );
          })}
        </ScrollView>
      )}
    </View>
  );
}
