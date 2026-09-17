import { useQuery } from '@tanstack/react-query';
import { router } from 'expo-router';
import { useState } from 'react';
import { ActivityIndicator, ScrollView, Text, TouchableOpacity, View } from 'react-native';

import { authApiClient } from '@/helpers/apiClient';
import { useMutationPost } from '@/helpers/mutations/useMutationPost';
import { availableTiers, currentStep, hasSearchArea, skipBlockedReason, sortScrolls, stepProgress, stepSummary, tierIcon } from '@/helpers/clues';
import { progressionStyles as styles } from '@/styles/progression';
import { QueryKeys } from '@/helpers/QueryKeys';
import type { ClueScroll } from '@/interfaces/api/clues/ClueScroll';
import type { Museum } from '@/interfaces/api/museum/Museum';

/**
 * Clue scrolls (Stage 13 task 5, DESIGN.md §5B).
 *
 * Deliberately shows no countdown: §5B.3 says clues are "for savouring, not stressing",
 * and they pair with weekend walks. Only the current step's riddle is shown, because the
 * chain is meant to unfold one leg at a time.
 */
export default function CluesScreen() {
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const scrolls = useQuery<ClueScroll[]>({
    queryKey: [QueryKeys.Clues],
    queryFn: async () => (await authApiClient.get<ClueScroll[]>('/api/Clues/GetScrolls')).data,
  });

  // Curation funds skips, and lives on the Museum payload.
  const museum = useQuery<Museum>({
    queryKey: [QueryKeys.Museum],
    queryFn: async () => (await authApiClient.get<Museum>('/api/Museum/GetMuseum')).data,
  });

  const failureMessage = (err: unknown): string => {
    const data = (err as { response?: { data?: unknown } })?.response?.data;
    if (typeof data === 'string' && data) return data;
    return err instanceof Error ? err.message : 'Something went wrong';
  };

  // Solving a step can fill a Museum plinth, so both views refresh together.
  const alsoInvalidate = [[QueryKeys.Museum]];

  const generate = useMutationPost<string, ClueScroll>({
    url: (tier) => `/api/Clues/Generate?tier=${tier}`,
    queryKey: [QueryKeys.Clues],
    invalidateQuery: true,
    alsoInvalidate,
    onSuccess: () => { setError(null); setMessage(null); },
    onError: (err) => setError(failureMessage(err)),
  });

  const attempt = useMutationPost<number, { stepSolved: boolean; scrollComplete: boolean }>({
    url: (id) => `/api/Clues/Attempt?id=${id}`,
    queryKey: [QueryKeys.Clues],
    invalidateQuery: true,
    alsoInvalidate,
    onSuccess: (result) => {
      setError(null);
      setMessage(result?.scrollComplete ? 'Scroll complete!' : 'Found it. On to the next.');
    },
    onError: (err) => { setMessage(null); setError(failureMessage(err)); },
  });

  const skip = useMutationPost<number, unknown>({
    url: (id) => `/api/Clues/Skip?id=${id}`,
    queryKey: [QueryKeys.Clues],
    invalidateQuery: true,
    alsoInvalidate,
    onSuccess: () => { setError(null); setMessage('Step skipped.'); },
    onError: (err) => setError(failureMessage(err)),
  });

  const curation = museum.data?.curation ?? 0;
  const offered = availableTiers(scrolls.data ?? []);

  return (
    <View style={styles.screen}>
      <View style={styles.header}>
        <View style={styles.headerRow}>
          <Text style={styles.title}>📜 CLUES</Text>
          <TouchableOpacity style={styles.backButton} onPress={() => router.back()}>
            <Text style={styles.backText}>← BACK</Text>
          </TouchableOpacity>
        </View>

        <Text style={styles.xpText}>
          No time limits — take as long as you like. ✦ {curation.toLocaleString()} curation
        </Text>
      </View>

      {scrolls.isLoading && (
        <View style={styles.centred}>
          <ActivityIndicator color="#39ff14" />
          <Text style={styles.message}>Unrolling…</Text>
        </View>
      )}

      {!scrolls.isLoading && (
        <ScrollView contentContainerStyle={styles.scroll}>
          {message && <Text style={styles.effectText}>{message}</Text>}
          {error && <Text style={styles.errorText}>{error}</Text>}

          {sortScrolls(scrolls.data ?? []).map((scroll) => {
            const step = currentStep(scroll);
            const blocked = skipBlockedReason(scroll, curation);

            return (
              <View
                key={scroll.id}
                style={[styles.card, scroll.isComplete && styles.lockedCard]}
              >
                <View style={styles.cardRow}>
                  <Text style={styles.cardIcon}>{tierIcon(scroll.tier)}</Text>
                  <Text style={[styles.cardName, scroll.isComplete && styles.lockedName]}>
                    {scroll.tierName}
                  </Text>
                  <Text style={styles.cardLevel}>
                    {scroll.isComplete ? 'DONE' : stepProgress(scroll)}
                  </Text>
                </View>

                {step && (
                  <>
                    <Text style={styles.cardDescription}>{step.riddleText}</Text>

                    {hasSearchArea(step) && (
                      <Text style={styles.cardMeta}>
                        Search within {Math.round(step.searchRadius ?? 0)}m of the marked place
                      </Text>
                    )}

                    <TouchableOpacity
                      style={styles.buyButton}
                      disabled={attempt.isPending}
                      onPress={() => attempt.mutate(scroll.id)}
                    >
                      <Text style={styles.buyText}>I AM HERE</Text>
                    </TouchableOpacity>

                    <TouchableOpacity
                      style={[styles.buyButton, blocked && styles.buyButtonDisabled]}
                      disabled={blocked !== null || skip.isPending}
                      onPress={() => skip.mutate(scroll.id)}
                    >
                      <Text style={[styles.buyText, blocked && styles.buyTextDisabled]}>
                        {blocked ?? `SKIP · ${scroll.skipCost} curation`}
                      </Text>
                    </TouchableOpacity>
                  </>
                )}

                {/* The trail behind: solved steps, so the walk reads as a journey. */}
                {scroll.steps.filter((s) => s.isSolved).map((s) => (
                  <Text key={s.stepIndex} style={styles.cardMeta}>
                    {s.stepIndex + 1}. {stepSummary(s)} — {s.riddleText}
                  </Text>
                ))}
              </View>
            );
          })}

          {offered.length > 0 && (
            <>
              <Text style={styles.sectionTitle}>TAKE A NEW SCROLL</Text>

              {offered.map((tier) => (
                <TouchableOpacity
                  key={tier}
                  style={styles.buyButton}
                  disabled={generate.isPending}
                  onPress={() => generate.mutate(tier)}
                >
                  <Text style={styles.buyText}>
                    {tierIcon(tier)} {tier.toUpperCase()}
                  </Text>
                </TouchableOpacity>
              ))}
            </>
          )}
        </ScrollView>
      )}
    </View>
  );
}
