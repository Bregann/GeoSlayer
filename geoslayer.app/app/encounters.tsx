import { useQuery, useQueryClient } from '@tanstack/react-query';
import { router } from 'expo-router';
import { useState } from 'react';
import { ActivityIndicator, ScrollView, Text, TouchableOpacity, View } from 'react-native';

import { authApiClient } from '@/helpers/apiClient';
import {
  blockedReason,
  emptyStateMessage,
  lifetimeLabel,
  rangeLabel,
  sortEncounters,
  winChanceLabel,
} from '@/helpers/encounters';
import { useMutationPost } from '@/helpers/mutations/useMutationPost';
import { QueryKeys } from '@/helpers/QueryKeys';
import type { Encounter } from '@/interfaces/api/combat/Encounter';
import type { EncounterResult } from '@/interfaces/api/combat/EncounterResult';
import { progressionStyles as styles } from '@/styles/progression';

/**
 * Combat encounters (DESIGN.md §5C).
 *
 * §5C.2 is the line this screen must not cross: historic POIs are a boost, never the only
 * venue. Roaming encounters lead the list and the empty state says outright that they turn
 * up anywhere — a player with no ruin nearby should never infer they are locked out.
 */
export default function EncountersScreen() {
  const queryClient = useQueryClient();
  const [error, setError] = useState<string | null>(null);
  const [outcome, setOutcome] = useState<EncounterResult | null>(null);

  const encounters = useQuery<Encounter[]>({
    queryKey: [QueryKeys.Encounters],
    queryFn: async () =>
      (await authApiClient.get<Encounter[]>('/api/Combat/GetEncounters')).data,
  });

  const failureMessage = (err: unknown): string => {
    const data = (err as { response?: { data?: unknown } })?.response?.data;
    if (typeof data === 'string' && data) return data;
    return err instanceof Error ? err.message : 'Something went wrong';
  };

  const fight = useMutationPost<number, EncounterResult>({
    url: (id) => `/api/Combat/Resolve?id=${id}`,
    queryKey: [QueryKeys.Encounters],
    invalidateQuery: true,
    // A win grants materials and can fill a Museum plinth.
    alsoInvalidate: [[QueryKeys.Inventory], [QueryKeys.Skills], [QueryKeys.Museum]],
    onSuccess: (result) => {
      setError(null);
      if (result) setOutcome(result);
    },
    onError: (err) => setError(failureMessage(err)),
  });

  const list = encounters.data ?? [];
  const empty = emptyStateMessage(list);

  return (
    <View style={styles.screen}>
      <View style={styles.header}>
        <View style={styles.headerRow}>
          <Text style={styles.title}>⚔️ ENCOUNTERS</Text>
          <TouchableOpacity style={styles.backButton} onPress={() => router.back()}>
            <Text style={styles.backText}>← BACK</Text>
          </TouchableOpacity>
        </View>

        <Text style={styles.xpText}>
          Fights find you as you move. Walk up to one to take it on.
        </Text>
      </View>

      {encounters.isLoading && (
        <View style={styles.centred}>
          <ActivityIndicator color="#39ff14" />
          <Text style={styles.message}>Loading…</Text>
        </View>
      )}

      {!encounters.isLoading && (
        <ScrollView contentContainerStyle={styles.scroll}>
          {error && <Text style={styles.errorText}>{error}</Text>}

          {outcome && (
            <View style={styles.card}>
              <Text style={styles.cardName}>{outcome.message}</Text>

              <Text style={styles.cardMeta}>
                +{outcome.skillXpEarned} Combat XP
                {outcome.levelledUp ? ` · level ${outcome.combatLevel}!` : ''}
              </Text>

              {outcome.materials.length > 0 && (
                <Text style={styles.effectText}>
                  {outcome.materials.map((m) => `${m.name} ×${m.quantity}`).join(', ')}
                </Text>
              )}

              <TouchableOpacity style={styles.buyButton} onPress={() => setOutcome(null)}>
                <Text style={styles.buyText}>DISMISS</Text>
              </TouchableOpacity>
            </View>
          )}

          {empty && <Text style={styles.message}>{empty}</Text>}

          {sortEncounters(list).map((encounter) => {
            const blocked = blockedReason(encounter);

            return (
              <View key={encounter.id} style={styles.card}>
                <View style={styles.cardRow}>
                  <Text style={styles.cardName}>
                    {encounter.isTrainingGround ? '🏰 ' : '⚔️ '}
                    {encounter.name}
                  </Text>
                  <Text style={styles.cardLevel}>{lifetimeLabel(encounter)}</Text>
                </View>

                <Text style={styles.cardMeta}>{encounter.description}</Text>
                <Text style={styles.cardMeta}>at {encounter.poiName}</Text>

                <Text style={styles.effectText}>
                  {winChanceLabel(encounter)} to win · {rangeLabel(encounter)}
                </Text>

                {blocked && <Text style={styles.cardMeta}>{blocked}</Text>}

                {!blocked && (
                  <TouchableOpacity
                    style={styles.buyButton}
                    disabled={fight.isPending}
                    onPress={() => {
                      setOutcome(null);
                      fight.mutate(encounter.id);
                    }}
                  >
                    <Text style={styles.buyText}>FIGHT</Text>
                  </TouchableOpacity>
                )}
              </View>
            );
          })}

          <TouchableOpacity
            style={styles.buyButton}
            onPress={() =>
              queryClient.invalidateQueries({ queryKey: [QueryKeys.Encounters] })
            }
          >
            <Text style={styles.buyText}>↻ LOOK AROUND</Text>
          </TouchableOpacity>
        </ScrollView>
      )}
    </View>
  );
}
