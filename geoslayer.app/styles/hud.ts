import { StyleSheet } from 'react-native';

const PIXEL_BORDER = 2;
const BG = 'rgba(10, 10, 30, 0.92)';
const ACCENT = '#39ff14';
const GOLD = '#ffcc00';

export const hudStyles = StyleSheet.create({
  /* ── TOP HUD ─────────────────────────────────────────── */
  topContainer: {
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    backgroundColor: BG,
    borderBottomWidth: PIXEL_BORDER,
    borderBottomColor: ACCENT,
    paddingTop: 50, // safe area
    paddingHorizontal: 12,
    paddingBottom: 8,
    gap: 4,
  },
  topRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  statGroup: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 3,
  },
  iconText: {
    fontSize: 13,
  },
  statLabel: {
    color: '#fff',
    fontSize: 12,
    fontWeight: 'bold',
    fontFamily: 'monospace',
  },
  statValue: {
    color: '#fff',
    fontSize: 13,
    fontWeight: 'bold',
    fontFamily: 'monospace',
  },
  goldValue: {
    color: GOLD,
    fontSize: 13,
    fontWeight: 'bold',
    fontFamily: 'monospace',
  },
  xpLabel: {
    color: '#aaa',
    fontSize: 11,
    fontWeight: 'bold',
    fontFamily: 'monospace',
    marginLeft: 8,
  },
  xpTrack: {
    flex: 1,
    height: 12,
    backgroundColor: '#1a1a2e',
    borderWidth: 1,
    borderColor: '#555',
    overflow: 'hidden',
  },
  xpFill: {
    height: '100%',
    backgroundColor: '#bb66ff',
  },

  /* ── Street progress (inside top bar) ────────────────── */
  streetRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    marginTop: 2,
  },
  streetName: {
    color: ACCENT,
    fontSize: 10,
    fontWeight: 'bold',
    fontFamily: 'monospace',
    maxWidth: 90,
  },
  streetTrack: {
    flex: 1,
    height: 10,
    backgroundColor: '#1a1a2e',
    borderWidth: 1,
    borderColor: '#555',
    overflow: 'hidden',
  },
  streetFill: {
    height: '100%',
    backgroundColor: ACCENT,
  },
  streetPercent: {
    color: '#ccc',
    fontSize: 10,
    fontFamily: 'monospace',
    width: 32,
    textAlign: 'right',
  },

  /* ── BOTTOM PANEL ────────────────────────────────────── */
  bottomContainer: {
    position: 'absolute',
    bottom: 0,
    left: 0,
    right: 0,
    backgroundColor: BG,
    borderTopWidth: PIXEL_BORDER,
    borderTopColor: ACCENT,
    paddingBottom: 30, // safe area
    paddingHorizontal: 10,
    paddingTop: 10,
  },
  menuRow: {
    flexDirection: 'row',
    justifyContent: 'space-evenly',
    gap: 12,
  },
  menuButton: {
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: '#2a2a4e',
    borderWidth: PIXEL_BORDER,
    borderColor: '#4a4a7e',
    width: 56,
    height: 56,
  },
  menuIcon: {
    fontSize: 26,
  },
});
