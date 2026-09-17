import { useQuery } from '@tanstack/react-query';
import { router, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { ActivityIndicator, Text, TextInput, TouchableOpacity, View } from 'react-native';

import { authApiClient } from '@/helpers/apiClient';
import { coinLabel, interestSummary } from '@/helpers/economy';
import { useMutationPost } from '@/helpers/mutations/useMutationPost';
import { QueryKeys } from '@/helpers/QueryKeys';
import type { BankAccount } from '@/interfaces/api/economy/BankAccount';
import { progressionStyles as styles } from '@/styles/progression';

/**
 * Banking (DESIGN.md §5.4a).
 *
 * Deposited coin earns interest but cannot be spent until withdrawn — that illiquidity is
 * the entire trade, and the screen says so rather than letting a player discover it when
 * they try to buy something.
 *
 * The rate is deliberately small. Nothing here dresses it up: overselling a nudge is the
 * fastest way to make someone feel cheated when they return to a modest number.
 */
export default function BankScreen() {
  const params = useLocalSearchParams<{ poiId?: string; poiName?: string }>();
  const poiId = Number(params.poiId ?? 0);

  const [amount, setAmount] = useState('');
  const [error, setError] = useState<string | null>(null);

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

  const parsed = Math.max(0, Math.floor(Number(amount) || 0));

  const deposit = useMutationPost<null, BankAccount>({
    url: `/api/Economy/Deposit?poiId=${poiId}&amount=${parsed}`,
    queryKey: [QueryKeys.BankAccount],
    invalidateQuery: true,
    onSuccess: () => {
      setError(null);
      setAmount('');
    },
    onError: (err) => setError(failureMessage(err)),
  });

  const withdraw = useMutationPost<null, BankAccount>({
    url: `/api/Economy/Withdraw?poiId=${poiId}&amount=${parsed}`,
    queryKey: [QueryKeys.BankAccount],
    invalidateQuery: true,
    onSuccess: () => {
      setError(null);
      setAmount('');
    },
    onError: (err) => setError(failureMessage(err)),
  });

  const data = account.data;
  const busy = deposit.isPending || withdraw.isPending;

  return (
    <View style={styles.screen}>
      <View style={styles.header}>
        <View style={styles.headerRow}>
          <Text style={styles.title}>🏦 {(params.poiName ?? 'BANK').toUpperCase()}</Text>
          <TouchableOpacity style={styles.backButton} onPress={() => router.back()}>
            <Text style={styles.backText}>← BACK</Text>
          </TouchableOpacity>
        </View>
      </View>

      {account.isLoading && (
        <View style={styles.centred}>
          <ActivityIndicator />
        </View>
      )}

      {data && (
        <View style={styles.scroll}>
          {/* Interest already earned leads, when there is any — it is the reason to have
              come back, so it should not be buried under the controls. */}
          {data.interestJustPaid > 0 && (
            <Text style={styles.subtitle}>
              +{coinLabel(data.interestJustPaid)} interest while you were away
            </Text>
          )}

          <View style={styles.card}>
            <View style={styles.cardRow}>
              <Text style={styles.cardName}>In hand</Text>
              <Text style={styles.cardLevel}>{coinLabel(data.coin)}</Text>
            </View>
            <Text style={styles.cardMeta}>Spendable.</Text>
          </View>

          <View style={styles.card}>
            <View style={styles.cardRow}>
              <Text style={styles.cardName}>On deposit</Text>
              <Text style={styles.cardLevel}>{coinLabel(data.deposited)}</Text>
            </View>
            <Text style={styles.cardMeta}>{interestSummary(data)}</Text>
          </View>

          {error && <Text style={styles.effectText}>{error}</Text>}

          <TextInput
            style={styles.input}
            value={amount}
            onChangeText={setAmount}
            keyboardType="number-pad"
            placeholder="Amount"
            placeholderTextColor="#666"
          />

          <View style={styles.cardRow}>
            <TouchableOpacity
              style={styles.card}
              disabled={busy || parsed <= 0}
              onPress={() => deposit.mutate(null)}
            >
              <Text style={styles.cardName}>Deposit</Text>
            </TouchableOpacity>

            <TouchableOpacity
              style={styles.card}
              disabled={busy || parsed <= 0}
              onPress={() => withdraw.mutate(null)}
            >
              <Text style={styles.cardName}>Withdraw</Text>
            </TouchableOpacity>
          </View>
        </View>
      )}
    </View>
  );
}
