import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { router } from 'expo-router';
import { useState } from 'react';
import { ActivityIndicator, ScrollView, Text, TouchableOpacity, View } from 'react-native';

import { authApiClient } from '@/helpers/apiClient';
import { groupItemsByKind, needsClaim, type PlayerItem } from '@/helpers/crafting';
import { progressionStyles as styles } from '@/styles/progression';

interface Claim {
  id: number;
  name: string;
}

/**
 * Equipment and buildings (Stage 06 task 4).
 *
 * Gear equips to a slot; buildings are placed on a Claim. Both show their effect as text,
 * because §4.3 treats an item whose modifier changes nothing as a bug — the player should
 * be able to see what a thing does before and after equipping it.
 */
export default function EquipmentScreen() {
  const queryClient = useQueryClient();
  const [error, setError] = useState<string | null>(null);
  const [placing, setPlacing] = useState<number | null>(null);

  const items = useQuery<PlayerItem[]>({
    queryKey: ['player', 'items'],
    queryFn: async () => (await authApiClient.get<PlayerItem[]>('/api/crafting/items')).data,
  });

  const claims = useQuery<Claim[]>({
    queryKey: ['player', 'claims'],
    queryFn: async () => (await authApiClient.get<Claim[]>('/api/idle/claims')).data,
  });

  const failureMessage = (err: unknown): string => {
    const data = (err as { response?: { data?: unknown } })?.response?.data;
    if (typeof data === 'string' && data) return data;
    return err instanceof Error ? err.message : 'Something went wrong';
  };

  const equip = useMutation<PlayerItem[], unknown, { id: number; equipped: boolean; claimId?: number }>({
    mutationFn: async (input) =>
      (await authApiClient.post<PlayerItem[]>(`/api/crafting/items/${input.id}/equip`, {
        equipped: input.equipped,
        claimId: input.claimId ?? null,
      })).data,
    onSuccess: () => {
      setError(null);
      setPlacing(null);
      queryClient.invalidateQueries({ queryKey: ['player', 'items'] });
      // Gear changes reveal radius, XP, caps and offline time, so anything reading
      // those must not keep a stale copy.
      queryClient.invalidateQueries({ queryKey: ['player', 'inventory'] });
      queryClient.invalidateQueries({ queryKey: ['player', 'workers'] });
    },
    onError: (err: unknown) => setError(failureMessage(err)),
  });

  return (
    <View style={styles.screen}>
      <View style={styles.header}>
        <View style={styles.headerRow}>
          <Text style={styles.title}>🎽 EQUIPMENT</Text>
          <TouchableOpacity style={styles.backButton} onPress={() => router.back()}>
            <Text style={styles.backText}>← BACK</Text>
          </TouchableOpacity>
        </View>
      </View>

      {items.isLoading && (
        <View style={styles.centred}>
          <ActivityIndicator color="#39ff14" />
          <Text style={styles.message}>Loading…</Text>
        </View>
      )}

      {!items.isLoading && (items.data ?? []).length === 0 && (
        <View style={styles.centred}>
          <Text style={styles.message}>
            Nothing yet.{'\n'}Craft gear, tools and buildings to see them here.
          </Text>
        </View>
      )}

      {!items.isLoading && (items.data ?? []).length > 0 && (
        <ScrollView contentContainerStyle={styles.scroll}>
          {error && <Text style={styles.errorText}>{error}</Text>}

          {groupItemsByKind(items.data ?? []).map((group) => (
            <View key={group.kind}>
              <Text style={styles.sectionTitle}>{group.kind.toUpperCase()}</Text>

              {group.items.map((item) => (
                <View key={item.id} style={styles.card}>
                  <View style={styles.cardRow}>
                    <Text style={styles.cardName}>{item.name}</Text>
                    {item.isEquipped && <Text style={styles.cardLevel}>ACTIVE</Text>}
                  </View>

                  <Text style={styles.cardDescription}>{item.description}</Text>
                  <Text style={styles.effectText}>{item.modifierText}</Text>

                  {item.claimName && (
                    <Text style={styles.cardMeta}>Placed on {item.claimName}</Text>
                  )}

                  {needsClaim(item) && !item.isEquipped && placing === item.id && (
                    <View style={{ gap: 6, marginTop: 6 }}>
                      {(claims.data ?? []).length === 0 && (
                        <Text style={styles.cardMeta}>No Claims yet — claim territory first.</Text>
                      )}

                      {(claims.data ?? []).map((claim) => (
                        <TouchableOpacity
                          key={claim.id}
                          style={styles.buyButton}
                          onPress={() =>
                            equip.mutate({ id: item.id, equipped: true, claimId: claim.id })
                          }
                        >
                          <Text style={styles.buyText}>Place on {claim.name}</Text>
                        </TouchableOpacity>
                      ))}
                    </View>
                  )}

                  <TouchableOpacity
                    style={styles.buyButton}
                    disabled={equip.isPending}
                    onPress={() => {
                      if (item.isEquipped) {
                        equip.mutate({ id: item.id, equipped: false });
                      } else if (needsClaim(item)) {
                        setPlacing(placing === item.id ? null : item.id);
                      } else {
                        equip.mutate({ id: item.id, equipped: true });
                      }
                    }}
                  >
                    <Text style={styles.buyText}>
                      {item.isEquipped
                        ? 'REMOVE'
                        : needsClaim(item)
                          ? placing === item.id ? 'CANCEL' : 'PLACE'
                          : 'EQUIP'}
                    </Text>
                  </TouchableOpacity>
                </View>
              ))}
            </View>
          ))}
        </ScrollView>
      )}
    </View>
  );
}
