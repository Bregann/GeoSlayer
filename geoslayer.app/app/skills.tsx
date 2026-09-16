import { useQuery } from '@tanstack/react-query';
import { router } from 'expo-router';
import { ActivityIndicator, ScrollView, Text, TouchableOpacity, View } from 'react-native';

import { authApiClient } from '@/helpers/apiClient';
import {
  groupLockedByLevel,
  levelProgressPercent,
  nextUnlockSummary,
  skillIcon,
  sortSkillsByLevel,
  xpRemaining,
} from '@/helpers/progression';
import { progressionStyles as styles } from '@/styles/progression';
import type { PlayerSkills } from '@/types/progression';

/**
 * Skills screen (Stage 02 task 5, DESIGN.md §3.1c).
 *
 * Shows unlocked skills with progress, and the locked ladder below them named and greyed
 * with its unlock level — the ladder is a roadmap and the player should always be able to
 * see what they are walking toward.
 */
export default function SkillsScreen() {
  const { data, isLoading, isError, error } = useQuery<PlayerSkills>({
    queryKey: ['player', 'skills'],
    queryFn: async () => {
      const response = await authApiClient.get<PlayerSkills>('/api/player/skills');
      return response.data;
    },
  });

  return (
    <View style={styles.screen}>
      <View style={styles.header}>
        <View style={styles.headerRow}>
          <Text style={styles.title}>✨ SKILLS</Text>
          <TouchableOpacity style={styles.backButton} onPress={() => router.back()}>
            <Text style={styles.backText}>← BACK</Text>
          </TouchableOpacity>
        </View>

        {data && (
          <>
            <View style={styles.adventurerRow}>
              <Text style={styles.adventurerLabel}>ADVENTURER {data.adventurerLevel}</Text>
              <View style={styles.xpTrack}>
                <View
                  style={[
                    styles.xpFill,
                    {
                      width: `${levelProgressPercent(
                        data.adventurerXp,
                        data.adventurerXpForCurrentLevel,
                        data.adventurerXpForNextLevel,
                      )}%`,
                    },
                  ]}
                />
              </View>
            </View>

            <Text style={styles.xpText}>
              {data.adventurerXp.toLocaleString()} XP ·{' '}
              {xpRemaining(data.adventurerXp, data.adventurerXpForNextLevel).toLocaleString()} to
              next level
            </Text>

            {nextUnlockSummary(data) && (
              <Text style={styles.subtitle}>NEXT: {nextUnlockSummary(data)}</Text>
            )}
          </>
        )}
      </View>

      {isLoading && (
        <View style={styles.centred}>
          <ActivityIndicator color="#39ff14" />
          <Text style={styles.message}>Loading skills…</Text>
        </View>
      )}

      {isError && (
        <View style={styles.centred}>
          <Text style={styles.errorText}>
            Could not load skills.{'\n'}
            {error instanceof Error ? error.message : 'Unknown error'}
          </Text>
        </View>
      )}

      {data && (
        <ScrollView contentContainerStyle={styles.scroll}>
          <Text style={styles.sectionTitle}>UNLOCKED</Text>

          {sortSkillsByLevel(data.unlocked).map((skill) => (
            <View key={skill.name} style={styles.card}>
              <View style={styles.cardRow}>
                <Text style={styles.cardIcon}>{skillIcon(skill.name)}</Text>
                <Text style={styles.cardName}>{skill.name}</Text>
                <Text style={styles.cardLevel}>LVL {skill.level}</Text>
              </View>

              <View style={styles.xpTrack}>
                <View
                  style={[
                    styles.xpFill,
                    {
                      width: `${levelProgressPercent(
                        skill.xp,
                        skill.xpForCurrentLevel,
                        skill.xpForNextLevel,
                      )}%`,
                    },
                  ]}
                />
              </View>

              <Text style={styles.cardMeta}>
                {skill.xp.toLocaleString()} XP ·{' '}
                {xpRemaining(skill.xp, skill.xpForNextLevel).toLocaleString()} to next
              </Text>
            </View>
          ))}

          {data.locked.length > 0 && (
            <>
              <Text style={styles.sectionTitle}>THE ROAD AHEAD</Text>

              {groupLockedByLevel(data.locked).map((group) => (
                <View key={group.level} style={[styles.card, styles.lockedCard]}>
                  <Text style={styles.unlockBadge}>ADVENTURER LEVEL {group.level}</Text>

                  {group.entries.map((entry) => {
                    const isSkill = entry.unlockType === 'Skill' || entry.unlockType === 0;

                    return (
                      <View key={entry.payload} style={styles.cardRow}>
                        <Text style={styles.cardIcon}>
                          {isSkill ? skillIcon(entry.payload) : '🏛️'}
                        </Text>
                        <Text style={[styles.cardName, styles.lockedName]}>
                          {entry.displayName}
                        </Text>
                        <Text style={styles.cardMeta}>{isSkill ? 'SKILL' : 'SYSTEM'}</Text>
                      </View>
                    );
                  })}
                </View>
              ))}
            </>
          )}
        </ScrollView>
      )}
    </View>
  );
}
