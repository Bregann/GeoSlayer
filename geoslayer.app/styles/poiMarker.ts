import { StyleSheet } from 'react-native';

/** Colour palette per skill type – retro/pixel themed. */
export const SKILL_COLORS: Record<string, string> = {
  Prayer: '#f0e68c', // gold
  Knowledge: '#87ceeb', // sky blue
  Woodcutting: '#228b22', // forest green
  Fishing: '#1e90ff', // dodger blue
  Healing: '#ff6b6b', // soft red
  Athletics: '#ff8c00', // dark orange
  Tavern: '#daa520', // goldenrod
  Trading: '#ffd700', // gold
  Banking: '#c0c0c0', // silver
  Combat: '#dc143c', // crimson
  Mining: '#8b7355', // tan/brown
  Farming: '#9acd32', // yellow-green
  Smithing: '#b0b0b0', // grey steel
  Cooking: '#ff7f50', // coral
  Exploration: '#da70d6', // orchid
};

/** Skill icon emoji for the map markers. */
export const SKILL_ICONS: Record<string, string> = {
  Prayer: '⛪',
  Knowledge: '📚',
  Woodcutting: '🪓',
  Fishing: '🎣',
  Healing: '🏥',
  Athletics: '🏋️',
  Tavern: '🍺',
  Trading: '🏪',
  Banking: '🏦',
  Combat: '⚔️',
  Mining: '⛏️',
  Farming: '🌾',
  Smithing: '🔨',
  Cooking: '🍳',
  Exploration: '🧭',
};

export const poiMarkerStyles = StyleSheet.create({
  container: {
    alignItems: 'center',
  },
  bubble: {
    paddingHorizontal: 6,
    paddingVertical: 4,
    borderRadius: 4,
    borderWidth: 2,
    alignItems: 'center',
    justifyContent: 'center',
    minWidth: 32,
    minHeight: 32,
  },
  icon: {
    fontSize: 18,
    textAlign: 'center',
  },
  label: {
    fontSize: 9,
    fontFamily: 'monospace',
    fontWeight: 'bold',
    textAlign: 'center',
    marginTop: 2,
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
