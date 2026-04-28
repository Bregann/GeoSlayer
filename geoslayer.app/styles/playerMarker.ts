import { StyleSheet } from 'react-native';

const PX = 6;

export const playerMarkerStyles = StyleSheet.create({
  outer: {
    alignItems: 'center',
    justifyContent: 'center',
  },
  inner: {
    alignItems: 'center',
    zIndex: 2,
  },
  row: {
    flexDirection: 'row',
  },
  pixel: {
    width: PX,
    height: PX,
  },
  pixelGap: {
    width: PX,
    height: PX,
  },
  skin: {
    backgroundColor: '#f5cfa0',
  },
  hair: {
    backgroundColor: '#5c3a1e',
  },
  armor: {
    backgroundColor: '#39ff14',
  },
  belt: {
    backgroundColor: '#8B7500',
  },
  boots: {
    backgroundColor: '#5c3a1e',
  },
  glow: {
    position: 'absolute',
    bottom: -4,
    width: 22,
    height: 10,
    borderRadius: 11,
    backgroundColor: 'rgba(57, 255, 20, 0.3)',
    zIndex: 1,
  },
});
