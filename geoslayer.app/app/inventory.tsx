import { useQuery } from '@tanstack/react-query';
import { router } from 'expo-router';
import { ActivityIndicator, ScrollView, Text, TouchableOpacity, View } from 'react-native';

import { authApiClient } from '@/helpers/apiClient';
import { categoryIcon, priceLabel, sortByValue, stackLabel, tierLabel, totalUnits, unitPriceLabel } from '@/helpers/inventory';
import { progressionStyles as styles } from '@/styles/progression';
import { QueryKeys } from '@/helpers/QueryKeys';
import type { Inventory } from '@/interfaces/api/materials/Inventory';

/**
 * Inventory screen (Stage 03 task 5).
 *
 * Grouped by category, sorted by what each stack is worth. Caps were removed with the coin
 * economy (§5.4), so there is no ceiling to warn about — the question this screen now
 * answers is "is this haul worth walking to a shop for", which is why price leads.
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

        {/* Replaces the old near-cap warning. Caps are gone (§5.4), so the header's job
            is to answer "is it worth a trip to a shop" rather than "am I about to lose
            something". */}
        {data && data.totalSellValue > 0 && (
          <Text style={styles.subtitle}>
            Worth {data.totalSellValue.toLocaleString()}c at a shop
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

              {sortByValue(category.items).map((item) => {
                const tier = tierLabel(item);

                return (
                  <View key={item.materialId} style={styles.card}>
                    <View style={styles.cardRow}>
                      <Text style={styles.cardName}>{item.name}</Text>
                      {tier && <Text style={styles.cardLevel}>{tier}</Text>}
                    </View>

                    <View style={styles.cardRow}>
                      <Text style={styles.cardMeta}>{stackLabel(item)}</Text>
                      {item.isUnique && <Text style={styles.unlockBadge}>UNIQUE</Text>}
                    </View>

                    {/* What it is worth leads, because the decision this screen supports
                        is now "is this haul worth a trip to a shop" (§5.4). */}
                    <View style={styles.cardRow}>
                      <Text style={styles.cardLevel}>{priceLabel(item)}</Text>
                      <Text style={styles.cardMeta}>{unitPriceLabel(item)}</Text>
                    </View>
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
