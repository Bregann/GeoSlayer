import { useQuery } from '@tanstack/react-query';
import { router } from 'expo-router';
import { ActivityIndicator, ScrollView, Text, TouchableOpacity, View } from 'react-native';

import { authApiClient } from '@/helpers/apiClient';
import { categoryIcon, sortByUrgency, stackLabel, stackPercent, stackWarning, tierLabel, totalUnits } from '@/helpers/inventory';
import { progressionStyles as styles } from '@/styles/progression';
import { QueryKeys } from '@/helpers/QueryKeys';
import type { Inventory } from '@/interfaces/api/materials/Inventory';

/**
 * Inventory screen (Stage 03 task 5).
 *
 * Grouped by category, each stack shown against its cap. Near-cap materials are flagged
 * because overflow is a real cost (§7.4) — it converts to Dust at a poor rate, so the
 * player should see the ceiling coming rather than discover it after the fact.
 */
export default function InventoryScreen() {
  const { data, isLoading, isError, error } = useQuery<Inventory>({
    queryKey: [QueryKeys.Inventory],
    queryFn: async () => {
      const response = await authApiClient.get<Inventory>('/api/Player/GetInventory');
      return response.data;
    },
  });

  return (
    <View style={styles.screen}>
      <View style={styles.header}>
        <View style={styles.headerRow}>
          <Text style={styles.title}>🎒 INVENTORY</Text>
          <TouchableOpacity style={styles.backButton} onPress={() => router.back()}>
            <Text style={styles.backText}>← BACK</Text>
          </TouchableOpacity>
        </View>

        {data && (
          <Text style={styles.xpText}>
            {data.distinctMaterials} material{data.distinctMaterials === 1 ? '' : 's'} ·{' '}
            {totalUnits(data).toLocaleString()} units
          </Text>
        )}

        {data && data.nearCapCount > 0 && (
          <Text style={styles.subtitle}>
            ⚠ {data.nearCapCount} stack{data.nearCapCount === 1 ? '' : 's'} near cap
          </Text>
        )}
      </View>

      {isLoading && (
        <View style={styles.centred}>
          <ActivityIndicator color="#39ff14" />
          <Text style={styles.message}>Loading inventory…</Text>
        </View>
      )}

      {isError && (
        <View style={styles.centred}>
          <Text style={styles.errorText}>
            Could not load inventory.{'\n'}
            {error instanceof Error ? error.message : 'Unknown error'}
          </Text>
        </View>
      )}

      {data && data.categories.length === 0 && (
        <View style={styles.centred}>
          <Text style={styles.message}>
            Nothing yet.{'\n'}Walk somewhere and the ground will start giving things up.
          </Text>
        </View>
      )}

      {data && data.categories.length > 0 && (
        <ScrollView contentContainerStyle={styles.scroll}>
          {data.categories.map((category) => (
            <View key={category.category}>
              <Text style={styles.sectionTitle}>
                {categoryIcon(category.name)} {category.name.toUpperCase()}
              </Text>

              {sortByUrgency(category.items).map((item) => {
                const warning = stackWarning(item);
                const tier = tierLabel(item);

                return (
                  <View key={item.materialId} style={styles.card}>
                    <View style={styles.cardRow}>
                      <Text style={styles.cardName}>{item.name}</Text>
                      {tier && <Text style={styles.cardLevel}>{tier}</Text>}
                    </View>

                    <View style={styles.xpTrack}>
                      <View style={[styles.xpFill, { width: `${stackPercent(item)}%` }]} />
                    </View>

                    <View style={styles.cardRow}>
                      <Text style={styles.cardMeta}>{stackLabel(item)}</Text>
                      {item.isUnique && <Text style={styles.unlockBadge}>UNIQUE</Text>}
                    </View>

                    {warning && <Text style={styles.effectText}>{warning}</Text>}
                  </View>
                );
              })}
            </View>
          ))}
        </ScrollView>
      )}
    </View>
  );
}
