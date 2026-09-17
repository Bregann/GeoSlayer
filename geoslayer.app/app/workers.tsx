import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { router } from 'expo-router';
import { useState } from 'react';
import { ActivityIndicator, ScrollView, Text, TouchableOpacity, View } from 'react-native';

import { authApiClient } from '@/helpers/apiClient';
import {
  districtHint,
  districtTitle,
  hasDistrict,
  type DistrictStatus,
} from '@/helpers/districts';
import { hoursUntilCap, terrainNote, workerStatus, type Worker } from '@/helpers/idle';
import { formatDuration } from '@/helpers/idle';
import { progressionStyles as styles } from '@/styles/progression';
import type { PlayerSkills } from '@/types/progression';

interface Claim {
  id: number;
  name: string;
  terrains: string[];
  workerCount: number;
}

/**
 * Worker management (Stage 05 task 5).
 *
 * Assign to a Claim and a skill, see rates, and see time-to-cap so the player can plan a
 * return. Any unlocked skill can go on any Claim — terrain changes the rate, never the
 * eligibility (§5.2), so nothing here filters skills by terrain.
 */
export default function WorkersScreen() {
  const queryClient = useQueryClient();
  const [error, setError] = useState<string | null>(null);
  const [selected, setSelected] = useState<number | null>(null);

  const workers = useQuery<Worker[]>({
    queryKey: ['player', 'workers'],
    queryFn: async () => (await authApiClient.get<Worker[]>('/api/Idle/GetWorkers')).data,
  });

  const claims = useQuery<Claim[]>({
    queryKey: ['player', 'claims'],
    queryFn: async () => (await authApiClient.get<Claim[]>('/api/Idle/GetClaims')).data,
  });

  // Districts are a passive bonus on how Claims cluster (§5.5), so they belong with the
  // Claims rather than on a screen of their own.
  const district = useQuery<DistrictStatus>({
    queryKey: ['player', 'district'],
    queryFn: async () =>
      (await authApiClient.get<DistrictStatus>('/api/Retention/GetDistrict')).data,
  });

  const skills = useQuery<PlayerSkills>({
    queryKey: ['player', 'skills'],
    queryFn: async () => (await authApiClient.get<PlayerSkills>('/api/Player/GetSkills')).data,
  });

  const failureMessage = (err: unknown): string => {
    const data = (err as { response?: { data?: unknown } })?.response?.data;
    if (typeof data === 'string' && data) return data;
    return err instanceof Error ? err.message : 'Something went wrong';
  };

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['player', 'workers'] });
    queryClient.invalidateQueries({ queryKey: ['player', 'claims'] });
  };

  const hire = useMutation<Worker, unknown, void>({
    mutationFn: async () => (await authApiClient.post<Worker>('/api/Idle/HireWorker')).data,
    onSuccess: () => { setError(null); refresh(); },
    onError: (err: unknown) => setError(failureMessage(err)),
  });

  const assign = useMutation<Worker, unknown, { id: number; claimId: number; skill: number }>({
    mutationFn: async (input) =>
      (await authApiClient.post<Worker>(`/api/Idle/AssignWorker?id=${input.id}`, {
        claimId: input.claimId,
        skill: input.skill,
      })).data,
    onSuccess: () => { setError(null); setSelected(null); refresh(); },
    onError: (err: unknown) => setError(failureMessage(err)),
  });

  const isLoading = workers.isLoading || claims.isLoading || skills.isLoading;

  return (
    <View style={styles.screen}>
      <View style={styles.header}>
        <View style={styles.headerRow}>
          <Text style={styles.title}>⛏️ WORKERS</Text>
          <TouchableOpacity style={styles.backButton} onPress={() => router.back()}>
            <Text style={styles.backText}>← BACK</Text>
          </TouchableOpacity>
        </View>

        <Text style={styles.xpText}>
          Workers run while the app is closed. They are a floor, not a substitute for walking.
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

          {/* District status. The unformed case is the one that matters: §5.5's variety
              rule is otherwise invisible, and the player just sees nothing happen. */}
          <View style={styles.card}>
            <View style={styles.cardRow}>
              <Text style={styles.cardName}>🏘️ {districtTitle(district.data)}</Text>
              {hasDistrict(district.data) && (
                <Text style={styles.cardLevel}>
                  {district.data?.claimCount} claims
                </Text>
              )}
            </View>

            <Text style={styles.effectText}>{districtHint(district.data)}</Text>
          </View>

          {(workers.data ?? []).map((worker) => {
            const note = terrainNote(worker);
            const remaining = hoursUntilCap(worker);

            return (
              <View key={worker.id} style={styles.card}>
                <View style={styles.cardRow}>
                  <Text style={styles.cardName}>{worker.name}</Text>
                  <Text style={styles.cardLevel}>T{worker.tier}</Text>
                </View>

                <Text style={styles.cardMeta}>{workerStatus(worker)}</Text>

                {note && <Text style={styles.effectText}>{note}</Text>}

                {!worker.isIdle && (
                  <Text style={styles.cardMeta}>
                    {worker.xpPerHour.toFixed(1)} XP/h ·{' '}
                    {worker.isAtCap
                      ? 'at cap'
                      : `${formatDuration(remaining)} until cap`}
                  </Text>
                )}

                <TouchableOpacity
                  style={styles.buyButton}
                  onPress={() => setSelected(selected === worker.id ? null : worker.id)}
                >
                  <Text style={styles.buyText}>
                    {selected === worker.id ? 'CANCEL' : 'ASSIGN'}
                  </Text>
                </TouchableOpacity>

                {selected === worker.id && (
                  <View style={{ gap: 6, marginTop: 6 }}>
                    {(claims.data ?? []).length === 0 && (
                      <Text style={styles.cardMeta}>
                        No Claims yet — claim territory on the map first.
                      </Text>
                    )}

                    {(claims.data ?? []).map((claim) =>
                      (skills.data?.unlocked ?? []).map((skill) => (
                        <TouchableOpacity
                          key={`${claim.id}-${skill.skillType}`}
                          style={styles.buyButton}
                          disabled={assign.isPending}
                          onPress={() =>
                            assign.mutate({
                              id: worker.id,
                              claimId: claim.id,
                              skill: skill.skillType,
                            })
                          }
                        >
                          <Text style={styles.buyText}>
                            {skill.name} on {claim.name}
                          </Text>
                        </TouchableOpacity>
                      )),
                    )}
                  </View>
                )}
              </View>
            );
          })}

          {(workers.data ?? []).length === 0 && (
            <Text style={styles.message}>No workers yet.</Text>
          )}

          <TouchableOpacity
            style={styles.buyButton}
            disabled={hire.isPending}
            onPress={() => hire.mutate()}
          >
            <Text style={styles.buyText}>HIRE WORKER</Text>
          </TouchableOpacity>

          <TouchableOpacity
            style={styles.buyButton}
            onPress={() => router.push('/expeditions')}
          >
            <Text style={styles.buyText}>🧭 EXPEDITIONS</Text>
          </TouchableOpacity>

          <TouchableOpacity
            style={styles.buyButton}
            onPress={() => router.push('/patrols')}
          >
            <Text style={styles.buyText}>🚶 PATROLS</Text>
          </TouchableOpacity>
        </ScrollView>
      )}
    </View>
  );
}
