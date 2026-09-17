import { useMutation } from '@tanstack/react-query';
import { router } from 'expo-router';
import { useEffect, useState } from 'react';
import { ActivityIndicator, Modal, Text, TouchableOpacity, View } from 'react-native';

import { authApiClient } from '@/helpers/apiClient';
import { formatPickups } from '@/helpers/inventory';
import { decayLabel, previewXp, summariseVisit, visitBlockedReason } from '@/helpers/poiVisit';
import { poiModalStyles as styles } from '@/styles/mapScreen';
import { visitStyles } from '@/styles/progression';
import { SKILL_ICONS } from '@/styles/poiMarker';
import type { NearbyPoi } from '@/interfaces/api/journey/NearbyPoi';
import type { PoiVisitResult } from '@/interfaces/api/journey/PoiVisitResult';

interface Props {
  poi: NearbyPoi | null;
  /** Visits this player has already made here, for the decay preview (§3.4). */
  visitCount?: number;
  onClose: () => void;
  onVisited?: (_result: PoiVisitResult) => void;
}

/**
 * POI detail, with the visit action (Stage 04 task 4).
 *
 * The decay state is shown *before* visiting, so a reduced reward is never a surprise —
 * §3.4 makes revisits deliberately worth less, and the player should be able to see that
 * rather than infer it from a shrinking number.
 */
export function PoiDetailModal({ poi, visitCount = 0, onClose, onVisited }: Props) {
  const [result, setResult] = useState<PoiVisitResult | null>(null);
  const [error, setError] = useState<string | null>(null);

  // A new POI clears the previous result, or the old outcome would appear attached to it.
  useEffect(() => {
    setResult(null);
    setError(null);
  }, [poi?.id]);

  // Deliberately not one of the useMutation* wrappers: a visit invalidates no cached
  // query (the result is handed straight to the caller via onVisited), and the wrappers
  // require a queryKey. Inventing one to satisfy the shape would be worse than this.
  const visit = useMutation<PoiVisitResult, unknown, number>({
    mutationFn: async (poiId: number) => {
      const response = await authApiClient.post<PoiVisitResult>(
        `/api/Journey/VisitPoi?id=${poiId}`,
      );

      if (response.status >= 400) {
        const data = response.data as unknown;
        throw new Error(
          typeof data === 'string' && data ? data : 'Could not visit this place',
        );
      }

      return response.data;
    },
    onSuccess: (visited: PoiVisitResult) => {
      setResult(visited);
      setError(null);
      onVisited?.(visited);
    },
    onError: (err: unknown) => {
      // The server rejects out-of-range and too-soon visits with a reason; show it.
      const response = (err as { response?: { data?: unknown } })?.response?.data;
      setError(
        typeof response === 'string' && response
          ? response
          : err instanceof Error
            ? err.message
            : 'Could not visit this place',
      );
    },
  });

  const blocked = poi ? visitBlockedReason(poi) : null;
  const decay = decayLabel(visitCount);
  const expected = poi ? previewXp(poi.xpReward, visitCount) : 0;

  return (
    <Modal
      visible={poi !== null}
      transparent
      animationType="fade"
      onRequestClose={onClose}
    >
      <TouchableOpacity style={styles.overlay} activeOpacity={1} onPress={onClose}>
        <View style={styles.card}>
          <Text style={styles.icon}>{SKILL_ICONS[poi?.skill ?? ''] ?? '❓'}</Text>
          <Text style={styles.name}>{poi?.name || 'Unknown Place'}</Text>
          <Text style={styles.skill}>{poi?.skill}</Text>

          <Text style={styles.xp}>+{expected} XP</Text>

          {decay && <Text style={visitStyles.decay}>{decay}</Text>}

          <Text style={styles.distance}>
            {Math.round(poi?.distanceMetres ?? 0)}m away
          </Text>

          {result && (
            <View style={visitStyles.resultBox}>
              <Text style={visitStyles.resultText}>{summariseVisit(result)}</Text>

              {formatPickups(result.materials) && (
                <Text style={visitStyles.resultMaterials}>
                  {formatPickups(result.materials)}
                </Text>
              )}
            </View>
          )}

          {error && <Text style={visitStyles.error}>{error}</Text>}

          {!result && (
            <TouchableOpacity
              style={[visitStyles.visitButton, blocked && visitStyles.visitButtonDisabled]}
              disabled={blocked !== null || visit.isPending || !poi}
              onPress={() => poi && visit.mutate(poi.id)}
            >
              {visit.isPending ? (
                <ActivityIndicator color="#39ff14" />
              ) : (
                <Text
                  style={[visitStyles.visitText, blocked && visitStyles.visitTextDisabled]}
                >
                  {blocked ?? 'VISIT'}
                </Text>
              )}
            </TouchableOpacity>
          )}

          {/* Trading and Banking POIs do something beyond training (§5.4, §5.4a), so
              they get a second action. Only offered in range — a button that opens a
              screen the server will then refuse is worse than no button. */}
          {poi?.skill === 'Trading' && poi.inRange && (
            <TouchableOpacity
              style={visitStyles.visitButton}
              onPress={() => {
                onClose();
                router.push({
                  pathname: '/shop',
                  params: { poiId: String(poi.id), poiName: poi.name },
                });
              }}
            >
              <Text style={visitStyles.visitText}>SELL HERE</Text>
            </TouchableOpacity>
          )}

          {poi?.skill === 'Banking' && poi.inRange && (
            <TouchableOpacity
              style={visitStyles.visitButton}
              onPress={() => {
                onClose();
                router.push({
                  pathname: '/bank',
                  params: { poiId: String(poi.id), poiName: poi.name },
                });
              }}
            >
              <Text style={visitStyles.visitText}>BANK</Text>
            </TouchableOpacity>
          )}

          <TouchableOpacity style={styles.closeButton} onPress={onClose}>
            <Text style={styles.closeText}>CLOSE</Text>
          </TouchableOpacity>
        </View>
      </TouchableOpacity>
    </Modal>
  );
}
