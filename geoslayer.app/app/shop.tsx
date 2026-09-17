import { useQuery, useQueryClient } from '@tanstack/react-query';
import { router, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { ActivityIndicator, ScrollView, Text, TouchableOpacity, View } from 'react-native';

import { authApiClient } from '@/helpers/apiClient';
import { coinLabel, junkCount, junkValue, saleSummary, sellableItems } from '@/helpers/economy';
import { useMutationPost } from '@/helpers/mutations/useMutationPost';
import { QueryKeys } from '@/helpers/QueryKeys';
import { tierLabel } from '@/helpers/inventory';
import type { BankAccount } from '@/interfaces/api/economy/BankAccount';
import type { SellResult } from '@/interfaces/api/economy/SellResult';
import type { Inventory } from '@/interfaces/api/materials/Inventory';
import { progressionStyles as styles } from '@/styles/progression';

/**
 * Selling at a shop (DESIGN.md §5.4).
 *
 * Reached by tapping a Trading POI on the map, so the player is already standing at the
 * shop when they arrive here — the walk is the cost, and this screen is the payoff.
 *
 * Per-material choice with a junk shortcut, deliberately: one button that empties the bag
 * would eventually sell the ore someone was saving for a recipe, and that is the kind of
 * loss a player does not forgive.
 */
export default function ShopScreen() {
  const params = useLocalSearchParams<{ poiId?: string; poiName?: string }>();
  const poiId = Number(params.poiId ?? 0);

  const queryClient = useQueryClient();
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<SellResult | null>(null);

  const inventory = useQuery<Inventory>({
    queryKey: [QueryKeys.Inventory],
    queryFn: async () =>
      (await authApiClient.get<Inventory>('/api/Player/GetInventory')).data,
  });

  const account = useQuery<BankAccount>({
    queryKey: [QueryKeys.BankAccount],
    queryFn: async () =>
      (await authApiClient.get<BankAccount>('/api/Economy/GetAccount')).data,
  });

  const failureMessage = (err: unknown): string => {
    const data = (err as { response?: { data?: unknown } })?.response?.data;
    if (typeof data === 'string' && data) return data;
    return err instanceof Error ? err.message : 'Something went wrong';
  };

  const afterSale = (sale: SellResult | undefined) => {
    setError(null);
    if (sale) setResult(sale);
    queryClient.invalidateQueries({ queryKey: [QueryKeys.Inventory] });
    queryClient.invalidateQueries({ queryKey: [QueryKeys.BankAccount] });
  };

  const sell = useMutationPost<{ poiId: number; quantities: Record<number, number> }, SellResult>({
    url: '/api/Economy/Sell',
    queryKey: [QueryKeys.Inventory],
    invalidateQuery: true,
    onSuccess: afterSale,
    onError: (err) => setError(failureMessage(err)),
  });

  const sellJunk = useMutationPost<null, SellResult>({
    url: `/api/Economy/SellJunk?poiId=${poiId}`,
    queryKey: [QueryKeys.Inventory],
    invalidateQuery: true,
    onSuccess: afterSale,
    onError: (err) => setError(failureMessage(err)),
  });

  const items = sellableItems(
    (inventory.data?.categories ?? []).flatMap((category) => category.items),
  );

  const junkWorth = junkValue(items);
  const junkStacks = junkCount(items);
  const busy = sell.isPending || sellJunk.isPending;

  return (
    <View style={styles.screen}>
      <View style={styles.header}>
        <View style={styles.headerRow}>
          <Text style={styles.title}>🏪 {(params.poiName ?? 'SHOP').toUpperCase()}</Text>
          <TouchableOpacity style={styles.backButton} onPress={() => router.back()}>
            <Text style={styles.backText}>← BACK</Text>
          </TouchableOpacity>
        </View>

        <Text style={styles.xpText}>
          {coinLabel(account.data?.coin ?? 0)} in hand
        </Text>
      </View>

      {error && <Text style={styles.effectText}>{error}</Text>}

      {result && (
        <Text style={styles.subtitle}>
          {saleSummary(result.coinEarned, result.poiName)}
        </Text>
      )}

      {inventory.isLoading && (
        <View style={styles.centred}>
          <ActivityIndicator />
        </View>
      )}

      {!inventory.isLoading && items.length === 0 && (
        <View style={styles.centred}>
          <Text style={styles.message}>
            Nothing to sell.{'\n'}Go and find something first.
          </Text>
        </View>
      )}

      {items.length > 0 && (
        <ScrollView contentContainerStyle={styles.scroll}>
          {/* The shortcut says what it will take and what it pays, so it is never a
              surprise. It deliberately reaches only low tiers (§5.4). */}
          {junkStacks > 0 && (
            <TouchableOpacity
              style={styles.card}
              disabled={busy}
              onPress={() => sellJunk.mutate(null)}
            >
              <Text style={styles.cardName}>
                Sell all junk — {coinLabel(junkWorth)}
              </Text>
              <Text style={styles.cardMeta}>
                {junkStacks} low-tier stack{junkStacks === 1 ? '' : 's'}. Nothing rare, no relics.
              </Text>
            </TouchableOpacity>
          )}

          {items.map((item) => {
            const tier = tierLabel(item);

            return (
              <TouchableOpacity
                key={item.materialId}
                style={styles.card}
                disabled={busy}
                onPress={() =>
                  sell.mutate({
                    poiId,
                    quantities: { [item.materialId]: item.quantity },
                  })
                }
              >
                <View style={styles.cardRow}>
                  <Text style={styles.cardName}>{item.name}</Text>
                  {tier && <Text style={styles.cardLevel}>{tier}</Text>}
                </View>

                <View style={styles.cardRow}>
                  <Text style={styles.cardMeta}>
                    {item.quantity.toLocaleString()} × {coinLabel(item.unitPrice)}
                  </Text>
                  <Text style={styles.cardLevel}>{coinLabel(item.stackPrice)}</Text>
                </View>
              </TouchableOpacity>
            );
          })}
        </ScrollView>
      )}
    </View>
  );
}
