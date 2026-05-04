import { Marker } from '@maplibre/maplibre-react-native';
import { Text, TouchableOpacity, View } from 'react-native';

import {
  poiMarkerStyles as styles,
  SKILL_COLORS,
  SKILL_ICONS,
} from '@/styles/poiMarker';

interface PoiMarkerProps {
  id: number;
  name: string;
  skill: string;
  coordinate: [number, number];
  inRange: boolean;
  clusterCount?: number;
  onPress?: () => void;
}

export function PoiMarker({ id, name, skill, coordinate, inRange, clusterCount, onPress }: PoiMarkerProps) {
  const color = SKILL_COLORS[skill] ?? '#aaa';
  const icon = SKILL_ICONS[skill] ?? '❓';

  return (
    <Marker key={id} lngLat={coordinate} anchor="center">
      <TouchableOpacity onPress={onPress} activeOpacity={0.7}>
        <View style={styles.container}>
          {/* Glow ring when in interaction range */}
          {inRange && (
            <View style={[styles.inRangeGlow, { backgroundColor: color }]} />
          )}
          <View style={[styles.bubble, { borderColor: color, backgroundColor: 'rgba(10,10,30,0.85)' }]}>
            {clusterCount ? (
              <Text style={[styles.icon, { fontSize: 12, color }]}>{clusterCount}x</Text>
            ) : (
              <Text style={styles.icon}>{icon}</Text>
            )}
          </View>
          {name ? (
            <Text style={[styles.label, { color }]} numberOfLines={1}>
              {name}
            </Text>
          ) : null}
        </View>
      </TouchableOpacity>
    </Marker>
  );
}
