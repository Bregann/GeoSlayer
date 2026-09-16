import { Text, TouchableOpacity, View } from 'react-native';

import { hudStyles as styles } from '@/styles/hud';
import { levelProgressPercent } from '@/helpers/progression';
import { xpProgressPercent } from '@/helpers/xpCurve';

interface HudProps {
  /** Cumulative lifetime Adventurer XP. */
  xp: number;
  /** Adventurer level (DESIGN.md §3.0). */
  level: number;
  cellsRevealed: number;
  /**
   * Level bounds from the server, when a skills fetch has supplied them. The HUD falls
   * back to its own copy of the curve when it has not — the map screen renders long
   * before any progression request completes.
   */
  xpForCurrentLevel?: number;
  xpForNextLevel?: number;
  /** Unspent Bonus Points, shown as a nudge when the player has some to spend. */
  bonusPoints?: number;
  onInventory?: () => void;
  onSkills?: () => void;
  onUpgrades?: () => void;
}

export function Hud({
  xp,
  level,
  cellsRevealed,
  xpForCurrentLevel,
  xpForNextLevel,
  bonusPoints,
  onInventory,
  onSkills,
  onUpgrades,
}: HudProps) {
  // Prefer the server's own level bounds; fall back to the client curve copy. `xp` is
  // cumulative lifetime XP either way, so progress comes from the curve rather than a
  // flat `level * 100`.
  const xpPercent =
    xpForCurrentLevel !== undefined && xpForNextLevel !== undefined
      ? levelProgressPercent(xp, xpForCurrentLevel, xpForNextLevel)
      : xpProgressPercent(xp, level);

  const hasPoints = (bonusPoints ?? 0) > 0;

  return (
    <>
      {/* TOP HUD */}
      <View style={styles.topContainer}>
        {/* HP and gold are deliberately absent until those systems exist (Stage 02
            task 5). They previously rendered hardcoded `85/100` and `0g`, which is
            worse than showing nothing — it reads as real state that never changes. */}
        <View style={styles.topRow}>
          <View style={styles.statGroup}>
            <Text style={styles.iconText}>⭐</Text>
            <Text style={styles.statLabel}>ADVENTURER:</Text>
            <Text style={styles.statValue}>{level}</Text>
          </View>
          <Text style={styles.xpLabel}>XP</Text>
          <View style={styles.xpTrack}>
            <View style={[styles.xpFill, { width: `${xpPercent}%` }]} />
          </View>
        </View>

        <View style={styles.topRow}>
          <View style={styles.statGroup}>
            <Text style={styles.iconText}>🗺️</Text>
            <Text style={styles.statLabel}>EXPLORED:</Text>
            <Text style={styles.statValue}>{cellsRevealed} cells</Text>
          </View>

          {hasPoints && (
            <View style={styles.statGroup}>
              <Text style={styles.iconText}>⚡</Text>
              <Text style={styles.goldValue}>{bonusPoints} pts</Text>
            </View>
          )}
        </View>
      </View>

      {/* BOTTOM PANEL */}
      <View style={styles.bottomContainer}>
        <View style={styles.menuRow}>
          <TouchableOpacity style={styles.menuButton} onPress={onInventory}>
            <Text style={styles.menuIcon}>🎒</Text>
          </TouchableOpacity>

          <TouchableOpacity style={styles.menuButton} onPress={onSkills}>
            <Text style={styles.menuIcon}>✨</Text>
          </TouchableOpacity>

          <TouchableOpacity style={styles.menuButton} onPress={onUpgrades}>
            <Text style={styles.menuIcon}>⚡</Text>
          </TouchableOpacity>
        </View>
      </View>
    </>
  );
}
