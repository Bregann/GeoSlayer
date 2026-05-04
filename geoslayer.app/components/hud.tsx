import { Text, TouchableOpacity, View } from 'react-native';

import { hudStyles as styles } from '@/styles/hud';

interface HudProps {
  hp: number;
  maxHp: number;
  xp: number;
  level: number;
  gold: number;
  cellsRevealed: number;
  onInventory?: () => void;
  onSkills?: () => void;
}

export function Hud({
  hp,
  maxHp,
  xp,
  level,
  gold,
  cellsRevealed,
  onInventory,
  onSkills,
}: HudProps) {
  const hpPercent = Math.max(0, Math.min(100, (hp / maxHp) * 100));
  const xpNeeded = level * 100;
  const xpPercent = Math.max(0, Math.min(100, (xp / xpNeeded) * 100));

  return (
    <>
      {/* TOP HUD */}
      <View style={styles.topContainer}>
        <View style={styles.topRow}>
          <View style={styles.statGroup}>
            <Text style={styles.iconText}>❤️</Text>
            <Text style={styles.statLabel}>HP:</Text>
            <Text style={styles.statValue}>
              {hp}/{maxHp}
            </Text>
          </View>
          <View style={styles.statGroup}>
            <Text style={styles.iconText}>💰</Text>
            <Text style={styles.goldValue}>{gold.toLocaleString()}g</Text>
          </View>
        </View>

        <View style={styles.topRow}>
          <View style={styles.statGroup}>
            <Text style={styles.iconText}>⭐</Text>
            <Text style={styles.statLabel}>LVL:</Text>
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
        </View>
      </View>
    </>
  );
}
