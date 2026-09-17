'use client'

import {
  Alert,
  Button,
  Group,
  Modal,
  NumberInput,
  Stack,
  Switch,
  Text,
  TextInput,
  Textarea,
} from '@mantine/core'
import { IconInfoCircle } from '@tabler/icons-react'
import { useState } from 'react'

import { useMutationPost } from '@/helpers/mutations/useMutationPost'
import { messageFrom, notifyError, notifySuccess } from '@/helpers/notificationHelper'
import { QueryKeys } from '@/helpers/QueryKeys'
import type { AdminEncounter } from '@/interfaces/api/admin/AdminEncounter'
import type { SaveEncounterRequest } from '@/interfaces/api/admin/SaveEncounterRequest'

interface Props {
  opened: boolean
  onClose: () => void
  /** Null when creating. */
  encounter: AdminEncounter | null
}

const EMPTY: SaveEncounterRequest = {
  key: '',
  name: '',
  description: '',
  tier: 1,
  minCombatLevel: 1,
  isTrainingGround: false,
}

function initialForm(encounter: AdminEncounter | null): SaveEncounterRequest {
  if (encounter === null) return EMPTY

  return {
    id: encounter.id,
    key: encounter.key,
    name: encounter.name,
    description: encounter.description,
    tier: encounter.tier,
    minCombatLevel: encounter.minCombatLevel,
    isTrainingGround: encounter.isTrainingGround,
  }
}

/** Keyed on the encounter by {@link EditEncounterModal} — see EditItemModal for why. */
function EditEncounterForm({
  onClose,
  encounter,
}: {
  onClose: () => void
  encounter: AdminEncounter | null
}) {
  const [form, setForm] = useState<SaveEncounterRequest>(() => initialForm(encounter))

  const save = useMutationPost<SaveEncounterRequest, AdminEncounter>({
    url: '/api/Admin/SaveEncounter',
    queryKey: [QueryKeys.Encounters],
    invalidateQuery: true,
    alsoInvalidate: [[QueryKeys.AuditTrail]],
    onSuccess: (saved) => {
      if (saved !== undefined && saved.setWarnings.length > 0) {
        notifyError(saved.setWarnings.join(' '), 'Saved, but worth a look')
      } else {
        notifySuccess(encounter === null ? 'Encounter created.' : 'Encounter saved.')
      }

      onClose()
    },
    // The API names the stranded tier and why it matters — far more useful than a code.
    onError: (error) => notifyError(messageFrom(error)),
  })

  const set = <K extends keyof SaveEncounterRequest>(key: K, value: SaveEncounterRequest[K]) =>
    setForm((prev) => ({ ...prev, [key]: value }))

  return (
    <Stack>
      <Group grow>
        <TextInput
          label="Key"
          description="Referenced by player encounters."
          value={form.key}
          onChange={(e) => set('key', e.currentTarget.value)}
          required
        />

        <TextInput
          label="Name"
          value={form.name}
          onChange={(e) => set('name', e.currentTarget.value)}
          required
        />
      </Group>

      <Textarea
        label="Description"
        description="Shown to players before they commit to the walk."
        value={form.description}
        onChange={(e) => set('description', e.currentTarget.value)}
        autosize
        minRows={2}
      />

      <Group grow>
        <NumberInput
          label="Tier"
          description="Which Combat material rung this awards. 1–7."
          value={form.tier}
          onChange={(v) => set('tier', Number(v) || 1)}
          min={1}
          max={7}
        />

        <NumberInput
          label="Minimum Combat level"
          description="Gates which encounters appear, never whether any do."
          value={form.minCombatLevel}
          onChange={(v) => set('minCombatLevel', Number(v) || 1)}
          min={1}
        />
      </Group>

      {/* Tier 1 is the floor a new player meets. Flagged here rather than only on the
          server's refusal, so the reason arrives before the rejection does. */}
      {form.tier === 1 && form.minCombatLevel > 1 && (
        <Alert color="red" icon={<IconInfoCircle size={18} />} variant="light">
          <Text size="sm">
            Tier 1 must be reachable at Combat level 1, or a brand-new player meets nothing at
            all (§5C.1). This will be refused.
          </Text>
        </Alert>
      )}

      <Switch
        label="Training ground"
        description="Permanent and fixed to historic ground, rather than roaming and expiring."
        checked={form.isTrainingGround}
        onChange={(e) => set('isTrainingGround', e.currentTarget.checked)}
      />

      {/* The §5C.2 consequence, said plainly at the moment the switch is flipped. */}
      {form.isTrainingGround && (
        <Alert color="blue" icon={<IconInfoCircle size={18} />} variant="light">
          <Text size="sm">
            Training grounds do not count towards tier coverage. If this is the only encounter
            at tier {form.tier}, saving it as a training ground will be refused — a castle is
            a boost, never the only venue (§5C.2).
          </Text>
        </Alert>
      )}

      <Group justify="flex-end">
        <Button variant="default" onClick={onClose} disabled={save.isPending}>
          Cancel
        </Button>
        <Button onClick={() => save.mutate(form)} loading={save.isPending}>
          Save
        </Button>
      </Group>
    </Stack>
  )
}

export function EditEncounterModal({ opened, onClose, encounter }: Props) {
  return (
    <Modal
      opened={opened}
      onClose={onClose}
      title={encounter === null ? 'New encounter' : `Edit ${encounter.name}`}
      size="lg"
    >
      {opened && (
        <EditEncounterForm key={encounter?.id ?? 'new'} onClose={onClose} encounter={encounter} />
      )}
    </Modal>
  )
}
