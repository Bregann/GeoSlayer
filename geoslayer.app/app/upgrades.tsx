import { useQuery } from '@tanstack/react-query';
import { router } from 'expo-router';
import { useState } from 'react';
import { ActivityIndicator, Alert, ScrollView, Text, TouchableOpacity, View } from 'react-native';

import { authApiClient } from '@/helpers/apiClient';
import { useMutationPost } from '@/helpers/mutations/useMutationPost';
import { currentEffectLabel, formatEffect, groupUpgradesByCategory, purchaseBlockedReason, rankLabel } from '@/helpers/progression';
import { progressionStyles as styles } from '@/styles/progression';
import { QueryKeys } from '@/helpers/QueryKeys';
import type { PlayerUpgrades } from '@/interfaces/api/progression/PlayerUpgrades';

/**
 * Bonus Point upgrade screen (Stage 02 task 5, DESIGN.md §3.0a).
 *
 * Purchases and respec both return the full refreshed tree, so the screen writes that
 * straight back into the cache rather than refetching — the server has already told us
 * the new state, and a second round trip would let the points counter flicker.
 */
export default function UpgradesScreen() {
  const [actionError, setActionError] = useState<string | null>(null);

  const queryKey = [QueryKeys.Upgrades];

  const { data, isLoading, isError, error } = useQuery<PlayerUpgrades>({
    queryKey,
    queryFn: async () => {
      const response = await authApiClient.get<PlayerUpgrades>('/api/Player/GetUpgrades');
      return response.data;
    },
  });

  /** The server rejects an invalid purchase with 400 and a reason — surface it verbatim. */
  const failureMessage = (err: unknown): string => {
    const response = (err as { response?: { data?: unknown } })?.response?.data;

    if (typeof response === 'string' && response.length > 0) return response;

    if (response && typeof response === 'object') {
      const message = (response as { message?: string; title?: string }).message
        ?? (response as { title?: string }).title;
      if (message) return message;
    }

    return err instanceof Error ? err.message : 'Something went wrong';
  };

  // Both endpoints return the whole upgrades payload, so invalidateQuery is false and
  // the wrapper writes the response straight into the cache instead of refetching.
  // Reveal Radius and Scholar change what a sync returns, so the skills view and
  // anything reading player state must not keep a stale copy.
  const purchase = useMutationPost<string, PlayerUpgrades>({
    url: (key) => `/api/Player/PurchaseUpgrade?key=${key}`,
    queryKey,
    invalidateQuery: false,
    alsoInvalidate: [[QueryKeys.Skills]],
    onSuccess: () => setActionError(null),
    onError: (err) => setActionError(failureMessage(err)),
  });

  const respec = useMutationPost<void, PlayerUpgrades>({
    url: '/api/Player/Respec',
    queryKey,
    invalidateQuery: false,
    alsoInvalidate: [[QueryKeys.Skills]],
    onSuccess: () => setActionError(null),
    onError: (err) => setActionError(failureMessage(err)),
  });

  const confirmRespec = () => {
    if (!data) return;

    // Respec is destructive and costs points, so it asks first (§3.0a says players will
    // mis-invest; it should be recoverable, not accidental).
    Alert.alert(
      'Respec upgrades?',
      `This refunds every spent point and clears all ranks, for ${data.respecCost} point${
        data.respecCost === 1 ? '' : 's'
      }.`,
      [
        { text: 'Cancel', style: 'cancel' },
        { text: 'Respec', style: 'destructive', onPress: () => respec.mutate() },
      ],
    );
  };

  const isBusy = purchase.isPending || respec.isPending;

  return (
    <View style={styles.screen}>
      <View style={styles.header}>
        <View style={styles.headerRow}>
          <Text style={styles.title}>⚡ UPGRADES</Text>
          <TouchableOpacity style={styles.backButton} onPress={() => router.back()}>
            <Text style={styles.backText}>← BACK</Text>
          </TouchableOpacity>
        </View>

        <TouchableOpacity
          style={styles.backButton}
          onPress={() => router.push('/workers')}
        >
          <Text style={styles.backText}>⛏️ WORKERS →</Text>
        </TouchableOpacity>

        {data && (
          <View style={styles.pointsBanner}>
            <Text style={styles.adventurerLabel}>BONUS POINTS</Text>
            <Text style={styles.pointsValue}>{data.bonusPointsAvailable}</Text>
          </View>
        )}

        {data && (
          <Text style={styles.xpText}>
            {data.bonusPointsEarned} earned · {data.bonusPointsSpent} spent
          </Text>
        )}
      </View>

      {isLoading && (
        <View style={styles.centred}>
          <ActivityIndicator color="#39ff14" />
          <Text style={styles.message}>Loading upgrades…</Text>
        </View>
      )}

      {isError && (
        <View style={styles.centred}>
          <Text style={styles.errorText}>
            Could not load upgrades.{'\n'}
            {error instanceof Error ? error.message : 'Unknown error'}
          </Text>
        </View>
      )}

      {data && (
        <ScrollView contentContainerStyle={styles.scroll}>
          {actionError && <Text style={styles.errorText}>{actionError}</Text>}

          {groupUpgradesByCategory(data.upgrades).map((group) => (
            <View key={group.category}>
              <Text style={styles.sectionTitle}>{group.category.toUpperCase()}</Text>

              {group.upgrades.map((upgrade) => {
                const blocked = purchaseBlockedReason(upgrade);
                const effect = currentEffectLabel(upgrade);
                const disabled = blocked !== null || isBusy;

                return (
                  <View
                    key={upgrade.key}
                    style={[styles.card, !upgrade.isAvailable && styles.lockedCard]}
                  >
                    <View style={styles.cardRow}>
                      <Text
                        style={[styles.cardName, !upgrade.isAvailable && styles.lockedName]}
                      >
                        {upgrade.name}
                      </Text>
                      <Text style={styles.cardLevel}>{rankLabel(upgrade)}</Text>
                    </View>

                    <Text style={styles.cardDescription}>{upgrade.description}</Text>

                    {effect && <Text style={styles.effectText}>Current: {effect}</Text>}

                    {upgrade.rank < upgrade.maxRank && (
                      <Text style={styles.cardMeta}>
                        Next rank: {formatEffect(upgrade, upgrade.effectPerRank)}
                      </Text>
                    )}

                    <TouchableOpacity
                      style={[styles.buyButton, disabled && styles.buyButtonDisabled]}
                      disabled={disabled}
                      onPress={() => purchase.mutate(upgrade.key)}
                    >
                      <Text style={[styles.buyText, disabled && styles.buyTextDisabled]}>
                        {blocked ?? `BUY · ${upgrade.nextRankCost} pt${
                          upgrade.nextRankCost === 1 ? '' : 's'
                        }`}
                      </Text>
                    </TouchableOpacity>
                  </View>
                );
              })}
            </View>
          ))}

          <TouchableOpacity
            style={styles.respecButton}
            disabled={isBusy}
            onPress={confirmRespec}
          >
            <Text style={styles.respecText}>
              RESPEC · {data.respecCost} pt{data.respecCost === 1 ? '' : 's'}
            </Text>
          </TouchableOpacity>
        </ScrollView>
      )}
    </View>
  );
}
