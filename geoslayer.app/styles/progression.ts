import { StyleSheet } from 'react-native';

/**
 * Shared styling for the skills and upgrade screens, following the HUD's pixel-terminal
 * look: monospace, square borders, the same greens and golds.
 */

const PIXEL_BORDER = 2;
const BG = '#0a0a1e';
const PANEL = 'rgba(20, 20, 45, 0.95)';
const ACCENT = '#39ff14';
const GOLD = '#ffcc00';
const MUTED = '#6a6a8e';
const VIOLET = '#bb66ff';

export const progressionStyles = StyleSheet.create({
  /* ── Screen shell ────────────────────────────────────── */
  screen: {
    flex: 1,
    backgroundColor: BG,
  },
  header: {
    paddingTop: 50, // safe area
    paddingHorizontal: 14,
    paddingBottom: 12,
    borderBottomWidth: PIXEL_BORDER,
    borderBottomColor: ACCENT,
    backgroundColor: PANEL,
  },
  headerRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  title: {
    color: '#fff',
    fontSize: 18,
    fontWeight: 'bold',
    fontFamily: 'monospace',
  },
  backButton: {
    paddingVertical: 6,
    paddingHorizontal: 10,
    borderWidth: PIXEL_BORDER,
    borderColor: '#4a4a7e',
    backgroundColor: '#2a2a4e',
  },
  backText: {
    color: '#fff',
    fontSize: 12,
    fontWeight: 'bold',
    fontFamily: 'monospace',
  },
  subtitle: {
    color: ACCENT,
    fontSize: 12,
    fontFamily: 'monospace',
    marginTop: 6,
  },
  scroll: {
    padding: 14,
    gap: 10,
    paddingBottom: 40,
  },

  /* ── Adventurer summary ──────────────────────────────── */
  adventurerRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    marginTop: 8,
  },
  adventurerLabel: {
    color: '#fff',
    fontSize: 13,
    fontWeight: 'bold',
    fontFamily: 'monospace',
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
    backgroundColor: VIOLET,
  },
  xpText: {
    color: MUTED,
    fontSize: 10,
    fontFamily: 'monospace',
    marginTop: 4,
  },

  /* ── Section headings ────────────────────────────────── */
  sectionTitle: {
    color: GOLD,
    fontSize: 13,
    fontWeight: 'bold',
    fontFamily: 'monospace',
    marginTop: 14,
    marginBottom: 2,
  },

  /* ── Skill / upgrade cards ───────────────────────────── */
  card: {
    backgroundColor: PANEL,
    borderWidth: PIXEL_BORDER,
    borderColor: '#4a4a7e',
    padding: 12,
    gap: 6,
  },
  lockedCard: {
    backgroundColor: 'rgba(20, 20, 45, 0.6)',
    borderColor: '#2a2a4e',
  },
  cardRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  cardIcon: {
    fontSize: 20,
  },
  cardName: {
    color: '#fff',
    fontSize: 14,
    fontWeight: 'bold',
    fontFamily: 'monospace',
    flex: 1,
  },
  lockedName: {
    color: MUTED,
  },
  cardLevel: {
    color: ACCENT,
    fontSize: 13,
    fontWeight: 'bold',
    fontFamily: 'monospace',
  },
  cardMeta: {
    color: MUTED,
    fontSize: 11,
    fontFamily: 'monospace',
  },
  cardDescription: {
    color: '#c8c8e0',
    fontSize: 11,
    fontFamily: 'monospace',
  },
  unlockBadge: {
    color: GOLD,
    fontSize: 11,
    fontWeight: 'bold',
    fontFamily: 'monospace',
  },
  effectText: {
    color: ACCENT,
    fontSize: 11,
    fontWeight: 'bold',
    fontFamily: 'monospace',
  },

  /* ── Buttons ─────────────────────────────────────────── */
  buyButton: {
    marginTop: 4,
    paddingVertical: 8,
    alignItems: 'center',
    borderWidth: PIXEL_BORDER,
    borderColor: ACCENT,
    backgroundColor: '#14321a',
  },
  buyButtonDisabled: {
    borderColor: '#3a3a5e',
    backgroundColor: '#1a1a2e',
  },
  buyText: {
    color: ACCENT,
    fontSize: 12,
    fontWeight: 'bold',
    fontFamily: 'monospace',
  },
  buyTextDisabled: {
    color: MUTED,
  },
  respecButton: {
    marginTop: 18,
    paddingVertical: 10,
    alignItems: 'center',
    borderWidth: PIXEL_BORDER,
    borderColor: '#ff6666',
    backgroundColor: '#2e1414',
  },
  respecText: {
    color: '#ff6666',
    fontSize: 12,
    fontWeight: 'bold',
    fontFamily: 'monospace',
  },

  /* ── Points banner ───────────────────────────────────── */
  pointsBanner: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginTop: 8,
  },
  pointsValue: {
    color: GOLD,
    fontSize: 16,
    fontWeight: 'bold',
    fontFamily: 'monospace',
  },

  /* ── States ──────────────────────────────────────────── */
  centred: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    padding: 24,
    gap: 10,
  },
  message: {
    color: MUTED,
    fontSize: 13,
    fontFamily: 'monospace',
    textAlign: 'center',
  },
  errorText: {
    color: '#ff6666',
    fontSize: 13,
    fontFamily: 'monospace',
    textAlign: 'center',
  },
});

/**
 * The unlock celebration. §3.1c asks for a full screen rather than a toast — an unlock
 * is the payoff for several sessions of walking and should interrupt.
 */
export const celebrationStyles = StyleSheet.create({
  overlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.92)',
    alignItems: 'center',
    justifyContent: 'center',
    padding: 28,
    gap: 14,
  },
  banner: {
    color: GOLD,
    fontSize: 14,
    fontWeight: 'bold',
    fontFamily: 'monospace',
    letterSpacing: 2,
  },
  icon: {
    fontSize: 72,
  },
  name: {
    color: '#fff',
    fontSize: 26,
    fontWeight: 'bold',
    fontFamily: 'monospace',
    textAlign: 'center',
  },
  kind: {
    color: ACCENT,
    fontSize: 13,
    fontFamily: 'monospace',
  },
  level: {
    color: MUTED,
    fontSize: 12,
    fontFamily: 'monospace',
  },
  pageDots: {
    color: MUTED,
    fontSize: 12,
    fontFamily: 'monospace',
    marginTop: 4,
  },
  button: {
    marginTop: 18,
    paddingVertical: 12,
    paddingHorizontal: 34,
    borderWidth: 2,
    borderColor: ACCENT,
    backgroundColor: '#14321a',
  },
  buttonText: {
    color: ACCENT,
    fontSize: 14,
    fontWeight: 'bold',
    fontFamily: 'monospace',
  },
});

/**
 * The material pickup toast on the map screen (Stage 03 task 5).
 *
 * Sits above the bottom HUD panel rather than centre-screen: a pickup is ambient
 * feedback, unlike an unlock, and must not cover the map the player is walking with.
 */
export const pickupStyles = StyleSheet.create({
  container: {
    position: 'absolute',
    left: 12,
    right: 12,
    bottom: 110,
    backgroundColor: 'rgba(10, 10, 30, 0.94)',
    borderWidth: 2,
    borderColor: ACCENT,
    paddingVertical: 8,
    paddingHorizontal: 12,
    gap: 2,
  },
  text: {
    color: ACCENT,
    fontSize: 12,
    fontWeight: 'bold',
    fontFamily: 'monospace',
  },
  overflow: {
    color: GOLD,
    fontSize: 10,
    fontFamily: 'monospace',
  },
});

/**
 * The POI visit action and its outcome (Stage 04 task 4).
 */
export const visitStyles = StyleSheet.create({
  decay: {
    color: GOLD,
    fontSize: 11,
    fontFamily: 'monospace',
    marginBottom: 4,
    textAlign: 'center',
  },
  visitButton: {
    marginTop: 10,
    paddingVertical: 10,
    paddingHorizontal: 24,
    alignItems: 'center',
    borderWidth: 2,
    borderColor: ACCENT,
    backgroundColor: '#14321a',
    minWidth: 180,
  },
  visitButtonDisabled: {
    borderColor: '#3a3a5e',
    backgroundColor: '#1a1a2e',
  },
  visitText: {
    color: ACCENT,
    fontSize: 13,
    fontWeight: 'bold',
    fontFamily: 'monospace',
  },
  visitTextDisabled: {
    color: MUTED,
  },
  resultBox: {
    marginTop: 10,
    padding: 10,
    borderWidth: 1,
    borderColor: ACCENT,
    backgroundColor: 'rgba(20, 50, 26, 0.6)',
    gap: 4,
    alignItems: 'center',
  },
  resultText: {
    color: ACCENT,
    fontSize: 12,
    fontWeight: 'bold',
    fontFamily: 'monospace',
    textAlign: 'center',
  },
  resultMaterials: {
    color: '#c8c8e0',
    fontSize: 11,
    fontFamily: 'monospace',
    textAlign: 'center',
  },
  error: {
    color: '#ff6666',
    fontSize: 11,
    fontFamily: 'monospace',
    marginTop: 8,
    textAlign: 'center',
  },
});
