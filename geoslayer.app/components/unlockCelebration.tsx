import { useEffect, useState } from 'react';
import { Modal, Text, TouchableOpacity, View } from 'react-native';

import { skillIcon } from '@/helpers/progression';
import { celebrationStyles as styles } from '@/styles/progression';
import type { UnlockEvent } from '@/types/progression';

interface Props {
  unlocks: UnlockEvent[];
  onDismiss: () => void;
}

/**
 * The unlock celebration (DESIGN.md §3.1c) — full screen, not a toast.
 *
 * An unlock is the payoff for several sessions of walking, and §3.1c is explicit that it
 * should interrupt rather than slide past in a corner. A sync can cross more than one
 * rung at once, so these are shown one at a time and advanced by the player.
 */
export function UnlockCelebration({ unlocks, onDismiss }: Props) {
  const [index, setIndex] = useState(0);

  // A fresh batch starts from the beginning, otherwise a second unlock arriving while
  // the first is still open would show the wrong card or an out-of-range one.
  useEffect(() => {
    setIndex(0);
  }, [unlocks]);

  if (unlocks.length === 0) return null;

  const current = unlocks[Math.min(index, unlocks.length - 1)];
  const isLast = index >= unlocks.length - 1;

  // UnlockType serialises as a string by name, but a numeric enum is also possible
  // depending on the serializer — treat both as the same thing.
  const isSkill = current.unlockType === 'Skill' || current.unlockType === 0;

  return (
    <Modal visible transparent animationType="fade" onRequestClose={onDismiss}>
      <View style={styles.overlay}>
        <Text style={styles.banner}>★ UNLOCKED ★</Text>

        <Text style={styles.icon}>
          {isSkill ? skillIcon(current.payload) : '🏛️'}
        </Text>

        <Text style={styles.name}>{current.displayName}</Text>
        <Text style={styles.kind}>{isSkill ? 'NEW SKILL' : 'NEW SYSTEM'}</Text>
        <Text style={styles.level}>Adventurer level {current.adventurerLevel}</Text>

        {unlocks.length > 1 && (
          <Text style={styles.pageDots}>
            {index + 1} / {unlocks.length}
          </Text>
        )}

        <TouchableOpacity
          style={styles.button}
          onPress={() => (isLast ? onDismiss() : setIndex(index + 1))}
        >
          <Text style={styles.buttonText}>{isLast ? 'CONTINUE' : 'NEXT'}</Text>
        </TouchableOpacity>
      </View>
    </Modal>
  );
}
