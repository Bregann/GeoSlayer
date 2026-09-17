import { StyleSheet } from 'react-native';

/**
 * Encounter markers (DESIGN.md §5C).
 *
 * Shaped to be distinguishable from a PoiMarker at a glance, since an encounter sits at
 * a POI and the two are drawn on the same coordinate. Colour comes from
 * `helpers/encounters.mapColor` rather than living here, because it carries urgency and
 * urgency is presentation logic worth testing.
 */
export const encounterMarkerStyles = StyleSheet.create({
  container: {
    alignItems: 'center',
  },

  /** A rotated square: reads as a diamond, which no other marker in the app uses. */
  diamond: {
    width: 30,
    height: 30,
    borderWidth: 2,
    transform: [{ rotate: '45deg' }],
    backgroundColor: 'rgba(10,10,30,0.85)',
    alignItems: 'center',
    justifyContent: 'center',
  },

  /** Counter-rotated so the glyph sits upright inside the diamond. */
  icon: {
    fontSize: 15,
    textAlign: 'center',
    transform: [{ rotate: '-45deg' }],
  },

  label: {
    fontSize: 9,
    fontFamily: 'monospace',
    fontWeight: 'bold',
    textAlign: 'center',
    marginTop: 4,
    maxWidth: 70,
  },

  inRangeGlow: {
    position: 'absolute',
    width: 44,
    height: 44,
    borderRadius: 22,
    opacity: 0.35,
  },
});
