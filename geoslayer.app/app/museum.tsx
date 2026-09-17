import { useQuery, useQueryClient } from '@tanstack/react-query';
import { router } from 'expo-router';
import { useState } from 'react';
import { ActivityIndicator, ScrollView, Text, TouchableOpacity, View } from 'react-native';

import { authApiClient } from '@/helpers/apiClient';
import { useMutationPost } from '@/helpers/mutations/useMutationPost';
import {
  foundLabel,
  overallPercent,
  rarityColour,
  rarityName,
  sortEntries,
  sortWings,
  wingIcon,
  wingPercent,
  wingProgress,
  type Museum,
} from '@/helpers/museum';
import { museumStyles as styles, progressionStyles } from '@/styles/progression';
import { QueryKeys } from '@/helpers/QueryKeys';

/**
 * The Museum (Stage 12 task 4, DESIGN.md §5A).
 *
 * §5A.1: "a collection log is a completionist's spreadsheet; a Museum is a place you
 * built". So gaps are shown rather than hidden — an empty plinth carries its unlock
 * condition, which turns a blank into a direction to walk.
 */
export default function MuseumScreen() {
  const queryClient = useQueryClient();
  const [openWing, setOpenWing] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const { data, isLoading, isError } = useQuery<Museum>({
    queryKey: [QueryKeys.Museum],
    queryFn: async () => (await authApiClient.get<Museum>('/api/Museum/GetMuseum')).data,
  });

  const donate = useMutationPost<{ key: string; quantity: number }, unknown>({
    url: (input) => `/api/Museum/Donate?key=${input.key}`,
    queryKey: [QueryKeys.Museum],
    invalidateQuery: true,
    onSuccess: () => {
      setError(null);
      queryClient.invalidateQueries({ queryKey: [QueryKeys.Museum] });
    },
    onError: (err: unknown) => {
      const response = (err as { response?: { data?: unknown } })?.response?.data;
      setError(
        typeof response === 'string' && response
          ? response
          : err instanceof Error ? err.message : 'Could not donate',
      );
    },
  });

  return (
    <View style={progressionStyles.screen}>
      <View style={progressionStyles.header}>
        <View style={progressionStyles.headerRow}>
          <Text style={progressionStyles.title}>🏛️ MUSEUM</Text>
          <TouchableOpacity style={progressionStyles.backButton} onPress={() => router.back()}>
            <Text style={progressionStyles.backText}>← BACK</Text>
          </TouchableOpacity>
        </View>

        <TouchableOpacity
          style={progressionStyles.backButton}
          onPress={() => router.push('/clues')}
        >
          <Text style={progressionStyles.backText}>📜 CLUE SCROLLS →</Text>
        </TouchableOpacity>

        {data && (
          <>
            <Text style={progressionStyles.xpText}>
              {data.totalFound} of {data.totalEntries} · {overallPercent(data)}% complete
            </Text>
            <Text style={progressionStyles.subtitle}>
              ✦ {data.curation.toLocaleString()} curation
            </Text>
          </>
        )}
      </View>

      {isLoading && (
        <View style={progressionStyles.centred}>
          <ActivityIndicator color="#39ff14" />
          <Text style={progressionStyles.message}>Opening the doors…</Text>
        </View>
      )}

      {isError && (
        <View style={progressionStyles.centred}>
          <Text style={progressionStyles.errorText}>Could not load the Museum.</Text>
        </View>
      )}

      {data && (
        <ScrollView contentContainerStyle={progressionStyles.scroll}>
          {error && <Text style={progressionStyles.errorText}>{error}</Text>}

          {sortWings(data.wings).map((wing) => {
            const isOpen = openWing === wing.name;

            return (
              <View key={wing.name} style={progressionStyles.card}>
                <TouchableOpacity
                  onPress={() => setOpenWing(isOpen ? null : wing.name)}
                  activeOpacity={0.7}
                >
                  <View style={progressionStyles.cardRow}>
                    <Text style={progressionStyles.cardIcon}>{wingIcon(wing.name)}</Text>
                    <Text style={progressionStyles.cardName}>{wing.name}</Text>
                    <Text style={progressionStyles.cardLevel}>
                      {wing.total === 0 ? 'SOON' : wingProgress(wing)}
                    </Text>
                  </View>

                  {wing.total > 0 && (
                    <View style={progressionStyles.xpTrack}>
                      <View
                        style={[progressionStyles.xpFill, { width: `${wingPercent(wing)}%` }]}
                      />
                    </View>
                  )}

                  {wing.isComplete && (
                    <Text style={progressionStyles.unlockBadge}>WING COMPLETE</Text>
                  )}

                  {wing.total === 0 && (
                    <Text style={progressionStyles.cardMeta}>
                      This wing is still being built.
                    </Text>
                  )}
                </TouchableOpacity>

                {isOpen && wing.total > 0 && (
                  <View style={{ gap: 8, marginTop: 8 }}>
                    {sortEntries(wing.entries).map((entry) => (
                      <View
                        key={entry.key}
                        style={[styles.plinth, !entry.isFound && styles.plinthEmpty]}
                      >
                        <View style={progressionStyles.cardRow}>
                          <Text
                            style={[
                              styles.plinthName,
                              { color: entry.isFound ? rarityColour(entry.rarity) : '#6a6a8e' },
                            ]}
                          >
                            {entry.isFound ? entry.name : '???'}
                          </Text>

                          {entry.isFound && entry.quantity > 1 && (
                            <Text style={progressionStyles.cardMeta}>×{entry.quantity}</Text>
                          )}
                        </View>

                        {/* An empty plinth carries its unlock condition: a gap should be a
                            direction to walk, not a mystery. */}
                        <Text style={progressionStyles.cardMeta}>
                          {entry.isFound
                            ? (foundLabel(entry) ?? entry.description)
                            : entry.unlockCondition}
                        </Text>

                        {entry.isFound && (
                          <Text style={progressionStyles.cardMeta}>
                            {rarityName(entry.rarity)}
                          </Text>
                        )}

                        {entry.donatableQuantity > 0 && (
                          <TouchableOpacity
                            style={progressionStyles.buyButton}
                            disabled={donate.isPending}
                            onPress={() =>
                              donate.mutate({ key: entry.key, quantity: entry.donatableQuantity })
                            }
                          >
                            <Text style={progressionStyles.buyText}>
                              DONATE {entry.donatableQuantity} SPARE
                              {entry.donatableQuantity === 1 ? '' : 'S'}
                            </Text>
                          </TouchableOpacity>
                        )}
                      </View>
                    ))}
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
