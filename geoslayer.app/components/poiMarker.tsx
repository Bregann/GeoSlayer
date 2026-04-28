import { Marker } from '@maplibre/maplibre-react-native';
import { Text, View } from 'react-native';

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
}

export function PoiMarker({ id, name, skill, coordinate, inRange }: PoiMarkerProps) {
  const color = SKILL_COLORS[skill] ?? '#aaa';
  const icon = SKILL_ICONS[skill] ?? '❓';

  return (
    <Marker key={id} lngLat={coordinate} anchor="center">
      <View style={styles.container}>
        {/* Glow ring when in interaction range */}
        {inRange && (
          <View style={[styles.inRangeGlow, { backgroundColor: color }]} />
        )}
        <View style={[styles.bubble, { borderColor: color, backgroundColor: 'rgba(10,10,30,0.85)' }]}>
          <Text style={styles.icon}>{icon}</Text>
        </View>
        {name ? (
          <Text style={[styles.label, { color }]} numberOfLines={1}>
            {name}
          </Text>
        ) : null}
      </View>
    </Marker>
  );
}
