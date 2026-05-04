import { Modal, Text, TouchableOpacity, View } from 'react-native';

import { poiModalStyles as styles } from '@/styles/mapScreen';
import { SKILL_ICONS } from '@/styles/poiMarker';
import type { NearbyPoi } from '@/types/map';

interface Props {
  pois: NearbyPoi[] | null;
  onSelect: (poi: NearbyPoi) => void;
  onClose: () => void;
}

export function PoiClusterModal({ pois, onSelect, onClose }: Props) {
  return (
    <Modal
      visible={pois !== null}
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
          <Text style={styles.name}>Nearby Places</Text>
          {(pois ?? []).map((poi) => (
            <TouchableOpacity
              key={poi.id}
              style={styles.clusterItem}
              onPress={() => {
                onClose();
                onSelect(poi);
              }}
            >
              <Text style={styles.clusterIcon}>
                {SKILL_ICONS[poi.skill] ?? '❓'}
              </Text>
              <View style={{ flex: 1 }}>
                <Text style={styles.clusterName}>
                  {poi.name || 'Unknown Place'}
                </Text>
                <Text style={styles.clusterSkill}>
                  {poi.skill} · +{poi.xpReward} XP
                </Text>
              </View>
            </TouchableOpacity>
          ))}
          <TouchableOpacity style={styles.closeButton} onPress={onClose}>
            <Text style={styles.closeText}>CLOSE</Text>
          </TouchableOpacity>
        </View>
      </TouchableOpacity>
    </Modal>
  );
}
