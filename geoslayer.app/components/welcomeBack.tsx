import { Modal, ScrollView, Text, TouchableOpacity, View } from 'react-native';

import { capWarning, formatDuration, type OfflineAccrual } from '@/helpers/idle';
import { celebrationStyles, progressionStyles } from '@/styles/progression';

interface Props {
  accrual: OfflineAccrual | null;
  onDismiss: () => void;
}

/**
 * The welcome-back screen (Stage 05 task 4).
 *
 * The highest-value UX in the idle half: it is what makes closing the app feel good
 * rather than like quitting. Deliberately specific — named materials and levels, not a
 * generic "you have rewards" — and shown only when something meaningful accrued.
 */
export function WelcomeBack({ accrual, onDismiss }: Props) {
  if (!accrual) return null;

  const warning = capWarning(accrual);
  const materials = accrual.materials.filter((m) => m.quantity > 0);

  return (
    <Modal visible transparent animationType="fade" onRequestClose={onDismiss}>
      <View style={celebrationStyles.overlay}>
        <Text style={celebrationStyles.banner}>★ WELCOME BACK ★</Text>
        <Text style={celebrationStyles.icon}>⛏️</Text>

        <Text style={celebrationStyles.kind}>
          Your workers ran for {formatDuration(accrual.hoursAccrued)}
        </Text>

        <ScrollView
          style={{ maxHeight: 280, alignSelf: 'stretch' }}
          contentContainerStyle={{ gap: 8, paddingVertical: 12 }}
        >
          {accrual.skills.map((skill) => (
            <View key={skill.skillType} style={progressionStyles.card}>
              <View style={progressionStyles.cardRow}>
                <Text style={progressionStyles.cardName}>{skill.name}</Text>
                <Text style={progressionStyles.cardLevel}>
                  {skill.levelledUp ? `LVL ${skill.level}!` : `LVL ${skill.level}`}
                </Text>
              </View>
              <Text style={progressionStyles.cardMeta}>
                +{skill.xpEarned.toLocaleString()} XP
              </Text>
            </View>
          ))}

          {materials.length > 0 && (
            <View style={progressionStyles.card}>
              <Text style={progressionStyles.unlockBadge}>GATHERED</Text>
              {materials.map((material) => (
                <Text key={material.materialId} style={progressionStyles.cardMeta}>
                  +{material.quantity.toLocaleString()} {material.name}
                  {material.overflowConvertedToDust > 0 && ' (stack full — converted to Dust)'}
                </Text>
              ))}
            </View>
          )}

          {accrual.unlocks.map((unlock) => (
            <View key={unlock.payload} style={progressionStyles.card}>
              <Text style={progressionStyles.unlockBadge}>UNLOCKED</Text>
              <Text style={progressionStyles.cardName}>{unlock.displayName}</Text>
            </View>
          ))}
        </ScrollView>

        {warning && <Text style={progressionStyles.subtitle}>{warning}</Text>}

        <TouchableOpacity style={celebrationStyles.button} onPress={onDismiss}>
          <Text style={celebrationStyles.buttonText}>COLLECT</Text>
        </TouchableOpacity>
      </View>
    </Modal>
  );
}
