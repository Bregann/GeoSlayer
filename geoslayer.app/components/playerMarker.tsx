import { Marker } from '@maplibre/maplibre-react-native';
import { View } from 'react-native';

import { playerMarkerStyles as styles } from '@/styles/playerMarker';

interface PlayerMarkerProps {
  /** [longitude, latitude] */
  coordinate: [number, number];
}

/**
 * Custom pixel-art style player marker using MapLibre's Marker.
 * Replace the inner Views with an <Image source={require('...')} /> to use
 * a real sprite sheet asset.
 */
export function PlayerMarker({ coordinate }: PlayerMarkerProps) {
  return (
    <Marker lngLat={coordinate} anchor="center">
      <View style={styles.outer}>
        <View style={styles.inner}>
          {/* Pixel body rows */}
          <View style={styles.row}>
            <View style={[styles.pixel, styles.skin]} />
          </View>
          <View style={styles.row}>
            <View style={[styles.pixel, styles.skin]} />
            <View style={[styles.pixel, styles.hair]} />
            <View style={[styles.pixel, styles.skin]} />
          </View>
          <View style={styles.row}>
            <View style={[styles.pixel, styles.armor]} />
            <View style={[styles.pixel, styles.armor]} />
            <View style={[styles.pixel, styles.armor]} />
          </View>
          <View style={styles.row}>
            <View style={[styles.pixel, styles.armor]} />
            <View style={[styles.pixel, styles.belt]} />
            <View style={[styles.pixel, styles.armor]} />
          </View>
          <View style={styles.row}>
            <View style={[styles.pixel, styles.boots]} />
            <View style={styles.pixelGap} />
            <View style={[styles.pixel, styles.boots]} />
          </View>
        </View>
        {/* Glow ring */}
        <View style={styles.glow} />
      </View>
    </Marker>
  );
}
