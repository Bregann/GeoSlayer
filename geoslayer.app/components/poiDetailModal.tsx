import { Modal, Text, TouchableOpacity, View } from 'react-native';

import { poiModalStyles as styles } from '@/styles/mapScreen';
import { SKILL_ICONS } from '@/styles/poiMarker';
import type { NearbyPoi } from '@/types/map';

interface Props {
  poi: NearbyPoi | null;
  onClose: () => void;
}

export function PoiDetailModal({ poi, onClose }: Props) {
  return (
    <Modal
      visible={poi !== null}
      transparent
      animationType="fade"
      onRequestClose={onClose}
    >
      <TouchableOpacity
        style={styles.overlay}
        activeOpacity={1}
        onPress={onClose}
      >
        <View style={styles.card}>
          <Text style={styles.icon}>
            {SKILL_ICONS[poi?.skill ?? ''] ?? '❓'}
          </Text>
          <Text style={styles.name}>
            {poi?.name || 'Unknown Place'}
          </Text>
          <Text style={styles.skill}>{poi?.skill}</Text>
          <Text style={styles.xp}>+{poi?.xpReward} XP</Text>
          <Text style={styles.distance}>
            {Math.round(poi?.distanceMetres ?? 0)}m away
          </Text>
          <TouchableOpacity style={styles.closeButton} onPress={onClose}>
            <Text style={styles.closeText}>CLOSE</Text>
          </TouchableOpacity>
        </View>
      </TouchableOpacity>
    </Modal>
  );
}
