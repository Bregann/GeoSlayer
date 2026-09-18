import { useQuery } from '@tanstack/react-query';
import { router } from 'expo-router';
import { useState } from 'react';
import { ActivityIndicator, Alert, ScrollView, Text, TouchableOpacity, View } from 'react-native';

import { authApiClient } from '@/helpers/apiClient';
import { useMutationDelete } from '@/helpers/mutations/useMutationDelete';
import { useMutationPost } from '@/helpers/mutations/useMutationPost';
import { craftTimeRemaining, formatDuration, inputLabel, isTravelGated, lockBadge, sortRecipes } from '@/helpers/crafting';
import { progressionStyles as styles } from '@/styles/progression';
import { QueryKeys } from '@/helpers/QueryKeys';
import type { Craft } from '@/interfaces/api/crafting/Craft';
import type { Recipe } from '@/interfaces/api/crafting/Recipe';

interface RecipeList {
  recipes: Recipe[];
  queuedCount: number;
  queueLimit: number;
  /** Rented slots in hand, each good for one craft beyond the limit (§5D.4). */
  rentedCraftSlots: number;
  /** Coin to rent another. */
  slotRentalCost: number;
}

/**
 * Crafting (Stage 06 task 4).
 *
 * Locked recipes are shown greyed with their requirement rather than hidden (§4.2) — the
 * list is a roadmap. Travel-gated recipes are badged distinctly from level-gated ones,
 * because "go somewhere new" and "keep playing" are different answers.
 */
export default function CraftingScreen() {
  const [error, setError] = useState<string | null>(null);

  const recipes = useQuery<RecipeList>({
    queryKey: [QueryKeys.Recipes],
    queryFn: async () => (await authApiClient.get<RecipeList>('/api/Crafting/GetRecipes')).data,
  });

  const queue = useQuery<Craft[]>({
    queryKey: [QueryKeys.CraftQueue],
    queryFn: async () => (await authApiClient.get<Craft[]>('/api/Crafting/GetQueue')).data,
  });

  const failureMessage = (err: unknown): string => {
    const data = (err as { response?: { data?: unknown } })?.response?.data;
    if (typeof data === 'string' && data) return data;
    return err instanceof Error ? err.message : 'Something went wrong';
  };

  // A craft moves materials out of the inventory and onto the queue, so all three
  // views change together.
  const alsoInvalidate = [[QueryKeys.CraftQueue], [QueryKeys.Inventory]];

  const start = useMutationPost<string, Craft>({
    url: (key) => `/api/Crafting/QueueCraft?key=${key}`,
    queryKey: [QueryKeys.Recipes],
    invalidateQuery: true,
    alsoInvalidate,
    onSuccess: () => setError(null),
    onError: (err) => setError(failureMessage(err)),
  });

  // Renting returns the whole recipe list, so the slot counter and the button both update
  // from one response.
  const rentSlot = useMutationPost<null, RecipeList>({
    url: '/api/Crafting/RentCraftSlot',
    queryKey: [QueryKeys.Recipes],
    invalidateQuery: true,
    alsoInvalidate,
    onSuccess: () => setError(null),
    onError: (err) => setError(failureMessage(err)),
  });

  const cancel = useMutationDelete<number, void>({
    url: (id) => `/api/Crafting/CancelCraft?id=${id}`,
    queryKey: [QueryKeys.Recipes],
    invalidateQuery: true,
    alsoInvalidate,
    onSuccess: () => setError(null),
    onError: (err) => setError(failureMessage(err)),
  });

  const isLoading = recipes.isLoading || queue.isLoading;

  return (
    <View style={styles.screen}>
      <View style={styles.header}>
        <View style={styles.headerRow}>
          <Text style={styles.title}>🔨 CRAFTING</Text>
          <TouchableOpacity style={styles.backButton} onPress={() => router.back()}>
            <Text style={styles.backText}>← BACK</Text>
          </TouchableOpacity>
        </View>

        <TouchableOpacity
          style={styles.backButton}
          onPress={() => router.push('/equipment')}
        >
          <Text style={styles.backText}>🎽 EQUIPMENT →</Text>
        </TouchableOpacity>

        {recipes.data && (
          <Text style={styles.xpText}>
            {recipes.data.queuedCount} / {recipes.data.queueLimit} craft slots busy · crafts
            finish while you are away
            {recipes.data.rentedCraftSlots > 0 &&
              ` · ${recipes.data.rentedCraftSlots} rented`}
          </Text>
        )}

        {/* Offered only when the permanent slots are full — a rent button on an idle queue
            is an invitation to waste coin on nothing (§5D.4). */}
        {recipes.data && recipes.data.queuedCount >= recipes.data.queueLimit && (
          <TouchableOpacity
            style={styles.backButton}
            disabled={rentSlot.isPending}
            onPress={() =>
              Alert.alert(
                'Rent a craft slot?',
                `${recipes.data.slotRentalCost.toLocaleString()}c for one extra craft. ` +
                  'It is spent by the next thing you queue, and does not expire.\n\n' +
                  'The Craft Slot upgrade is permanent and much better value.',
                [
                  { text: 'Cancel', style: 'cancel' },
                  { text: 'Rent', onPress: () => rentSlot.mutate(null) },
                ],
              )
            }
          >
            <Text style={styles.backText}>
              RENT A SLOT · {recipes.data.slotRentalCost.toLocaleString()}c
            </Text>
          </TouchableOpacity>
        )}
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

          {(queue.data ?? []).length > 0 && (
            <>
              <Text style={styles.sectionTitle}>IN PROGRESS</Text>

              {(queue.data ?? []).map((craft) => (
                <View key={craft.id} style={styles.card}>
                  <View style={styles.cardRow}>
                    <Text style={styles.cardName}>{craft.recipeName}</Text>
                    <Text style={styles.cardLevel}>{craftTimeRemaining(craft)}</Text>
                  </View>

                  <TouchableOpacity
                    style={styles.respecButton}
                    disabled={cancel.isPending}
                    onPress={() => cancel.mutate(craft.id)}
                  >
                    <Text style={styles.respecText}>CANCEL · full refund</Text>
                  </TouchableOpacity>
                </View>
              ))}
            </>
          )}

          <Text style={styles.sectionTitle}>RECIPES</Text>

          {sortRecipes(recipes.data?.recipes ?? []).map((recipe) => {
            const badge = lockBadge(recipe);
            const travel = isTravelGated(recipe);

            return (
              <View
                key={recipe.key}
                style={[styles.card, !recipe.canCraft && styles.lockedCard]}
              >
                <View style={styles.cardRow}>
                  <Text style={[styles.cardName, !recipe.canCraft && styles.lockedName]}>
                    {recipe.name}
                  </Text>
                  <Text style={styles.cardMeta}>{formatDuration(recipe.durationSeconds)}</Text>
                </View>

                <Text style={styles.cardDescription}>{recipe.description}</Text>

                {recipe.outputName && (
                  <Text style={styles.effectText}>
                    Makes {recipe.outputQuantity}× {recipe.outputName}
                  </Text>
                )}

                {recipe.inputs.map((input) => (
                  <Text
                    key={input.materialId}
                    style={input.hasEnough ? styles.cardMeta : styles.errorText}
                  >
                    {input.name} {inputLabel(input)}
                    {input.isTravelGated && ' · from rare places only'}
                  </Text>
                ))}

                {badge && (
                  <Text style={travel ? styles.unlockBadge : styles.cardMeta}>
                    {badge}
                    {recipe.lockText ? ` — ${recipe.lockText}` : ''}
                  </Text>
                )}

                {recipe.canCraft && (
                  <TouchableOpacity
                    style={styles.buyButton}
                    disabled={start.isPending}
                    onPress={() => start.mutate(recipe.key)}
                  >
                    <Text style={styles.buyText}>CRAFT</Text>
                  </TouchableOpacity>
                )}
              </View>
            );
          })}
        </ScrollView>
      )}
    </View>
  );
}
