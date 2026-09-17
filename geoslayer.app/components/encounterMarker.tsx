import { Marker } from '@maplibre/maplibre-react-native';
import { Text, TouchableOpacity, View } from 'react-native';

import { mapColor, mapIcon, mapLabel } from '@/helpers/encounters';
import { encounterMarkerStyles as styles } from '@/styles/encounterMarker';
import type { Encounter } from '@/interfaces/api/combat/Encounter';

interface EncounterMarkerProps {
  encounter: Encounter;
  onPress?: () => void;
}

/**
 * An encounter drawn on the map (DESIGN.md §5C).
 *
 * Stage 16 shipped encounters with a screen but no map presence, which made the one
 * time-limited thing in the system invisible until the player went looking for it. A
 * roaming encounter expires; a list you have to open is the wrong home for that.
 *
 * Deliberately shaped unlike a PoiMarker — a diamond rather than a bubble — because an
 * encounter sits *at* a POI, and two similar markers on the same point would read as one
 * duplicated thing rather than two different ones.
 */
export function EncounterMarker({ encounter, onPress }: EncounterMarkerProps) {
  const color = mapColor(encounter);

  return (
    <Marker
      key={encounter.id}
      lngLat={[encounter.longitude, encounter.latitude]}
      anchor="center"
    >
      <TouchableOpacity onPress={onPress} activeOpacity={0.7}>
        <View style={styles.container}>
          {/* Only when the player can actually act on it — a glow on something out of
              reach is an invitation to walk into a road looking at a phone. */}
          {encounter.isInRange && (
            <View style={[styles.inRangeGlow, { backgroundColor: color }]} />
          )}

          <View style={[styles.diamond, { borderColor: color }]}>
            <Text style={styles.icon}>{mapIcon(encounter)}</Text>
          </View>

          <Text style={[styles.label, { color }]} numberOfLines={1}>
            {mapLabel(encounter)}
          </Text>
        </View>
      </TouchableOpacity>
    </Marker>
  );
}
