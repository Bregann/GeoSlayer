import { Text, TouchableOpacity, View } from 'react-native';

import { hudStyles as styles } from '@/styles/hud';

interface HudProps {
  hp: number;
  maxHp: number;
  xp: number;
  level: number;
  gold: number;
  streetName: string | null;
  streetProgress: number;
  onInventory?: () => void;
  onSkills?: () => void;
  onStreetLog?: () => void;
}

export function Hud({
  hp,
  maxHp,
  xp,
  level,
  gold,
  streetName,
  streetProgress,
  onInventory,
  onSkills,
  onStreetLog,
}: HudProps) {
  const hpPercent = Math.max(0, Math.min(100, (hp / maxHp) * 100));
  const xpNeeded = level * 100;
  const xpPercent = Math.max(0, Math.min(100, (xp / xpNeeded) * 100));

  return (
    <>
      {/* ── TOP HUD ─────────────────────────────────────────── */}
      <View style={styles.topContainer}>
        {/* Row 1: HP + Gold */}
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

        {/* Row 2: Level + XP bar */}
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

        {/* Row 3: Street name + progress (only when on a street) */}
        {streetName ? (
          <View style={styles.streetRow}>
            <Text style={styles.streetName} numberOfLines={1}>
              {streetName}
            </Text>
            <View style={styles.streetTrack}>
              <View
                style={[
                  styles.streetFill,
                  { width: `${Math.min(100, streetProgress)}%` },
                ]}
              />
            </View>
            <Text style={styles.streetPercent}>
              {Math.round(streetProgress)}%
            </Text>
          </View>
        ) : null}
      </View>

      {/* ── BOTTOM PANEL ────────────────────────────────────── */}
      <View style={styles.bottomContainer}>
        <View style={styles.menuRow}>
          <TouchableOpacity style={styles.menuButton} onPress={onInventory}>
            <Text style={styles.menuIcon}>🎒</Text>
          </TouchableOpacity>

          <TouchableOpacity style={styles.menuButton} onPress={onSkills}>
            <Text style={styles.menuIcon}>✨</Text>
          </TouchableOpacity>

          <TouchableOpacity style={styles.menuButton} onPress={onStreetLog}>
            <Text style={styles.menuIcon}>📜</Text>
          </TouchableOpacity>
        </View>
      </View>
    </>
  );
}
