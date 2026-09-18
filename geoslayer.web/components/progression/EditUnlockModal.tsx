'use client'

import {
  Alert,
  Button,
  Group,
  Modal,
  NumberInput,
  Select,
  Stack,
  Text,
  TextInput,
} from '@mantine/core'
import { IconInfoCircle } from '@tabler/icons-react'
import { useState } from 'react'

import { useMutationPost } from '@/helpers/mutations/useMutationPost'
import { messageFrom, notifyError, notifySuccess } from '@/helpers/notificationHelper'
import { QueryKeys } from '@/helpers/QueryKeys'
import type { AdminUnlock } from '@/interfaces/api/admin/AdminProgression'
import type { SaveUnlockRequest } from '@/interfaces/api/admin/SaveUnlockRequest'
import { asOptions, SkillTypes, UnlockTypes } from '@/interfaces/api/admin/ItemEnums'

interface Props {
  opened: boolean
  onClose: () => void
  /** Null when creating. */
  unlock: AdminUnlock | null
  /** Every existing rung, for warning about a payload that already unlocks elsewhere. */
  existing: AdminUnlock[]
}

const EMPTY: SaveUnlockRequest = {
  adventurerLevel: 1,
  unlockType: 0,
  payload: '',
  displayName: '',
}

function initialForm(unlock: AdminUnlock | null): SaveUnlockRequest {
  if (unlock === null) return EMPTY

  return {
    id: unlock.id,
    adventurerLevel: unlock.adventurerLevel,
    unlockType: unlock.unlockType,
    payload: unlock.payload,
    displayName: unlock.displayName,
  }
}

/** Keyed on the unlock by {@link EditUnlockModal} — see EditItemModal for why. */
function EditUnlockForm({
  onClose,
  unlock,
  existing,
}: {
  onClose: () => void
  unlock: AdminUnlock | null
  existing: AdminUnlock[]
}) {
  const [form, setForm] = useState<SaveUnlockRequest>(() => initialForm(unlock))

  const save = useMutationPost<SaveUnlockRequest, AdminUnlock>({
    url: '/api/Admin/SaveUnlock',
    queryKey: [QueryKeys.Progression],
    invalidateQuery: true,
    alsoInvalidate: [[QueryKeys.AuditTrail]],
    onSuccess: () => {
      notifySuccess(unlock === null ? 'Rung created.' : 'Rung saved.')
      onClose()
    },
    onError: (error) => notifyError(messageFrom(error)),
  })

  const set = <K extends keyof SaveUnlockRequest>(key: K, value: SaveUnlockRequest[K]) =>
    setForm((prev) => ({ ...prev, [key]: value }))

  // The server refuses this outright; showing it here means the reason arrives before the
  // rejection does. ApplyUnlocks skips what is already owned, so a second rung for the same
  // payload silently never fires — dead configuration that is very hard to notice.
  const clash = existing.find(
    (u) =>
      u.id !== form.id &&
      u.payload.toLowerCase() === form.payload.trim().toLowerCase() &&
      form.payload.trim() !== ''
  )

  // A Skill unlock's payload must be a SkillType name, because that is what the ladder
  // matches on when granting it.
  const isSkill = UnlockTypes[form.unlockType] === 'Skill'

  return (
    <Stack>
      <Group grow>
        <NumberInput
          label="Adventurer level"
          description="The rung this sits on."
          value={form.adventurerLevel}
          onChange={(v) => set('adventurerLevel', Number(v) || 1)}
          min={1}
        />

        <Select
          label="Type"
          data={asOptions(UnlockTypes)}
          value={String(form.unlockType)}
          onChange={(v) => {
            // Clearing the payload on a type change is deliberate: a SkillType name is not
            // a valid system key and vice versa, so carrying it over would guarantee a bad
            // save.
            setForm((prev) => ({ ...prev, unlockType: Number(v ?? 0), payload: '' }))
          }}
          allowDeselect={false}
        />
      </Group>

      {isSkill ? (
        <Select
          label="Skill"
          description="Matched by name when the ladder grants it."
          data={SkillTypes.map((name) => ({ value: name, label: name }))}
          value={form.payload === '' ? null : form.payload}
          onChange={(v) => set('payload', v ?? '')}
          searchable
        />
      ) : (
        <TextInput
          label="System key"
          description="The system this rung unlocks, e.g. Claims or Crafting."
          value={form.payload}
          onChange={(e) => set('payload', e.currentTarget.value)}
        />
      )}

      <TextInput
        label="Shown as"
        description="What the unlock celebration says. Written for a player mid-walk."
        placeholder="e.g. Mining unlocked"
        value={form.displayName}
        onChange={(e) => set('displayName', e.currentTarget.value)}
        required
      />

      {clash !== undefined && (
        <Alert color="orange" icon={<IconInfoCircle size={18} />} variant="light">
          <Text size="sm">
            &apos;{clash.payload}&apos; already unlocks at level {clash.adventurerLevel}. A
            second rung for the same payload never fires — whichever comes first grants it,
            and the other is dead configuration. The server will refuse this.
          </Text>
        </Alert>
      )}

      <Group justify="flex-end">
        <Button variant="default" onClick={onClose} disabled={save.isPending}>
          Cancel
        </Button>
        <Button
          onClick={() => save.mutate(form)}
          loading={save.isPending}
          disabled={clash !== undefined || form.payload.trim() === ''}
        >
          Save
        </Button>
      </Group>
    </Stack>
  )
}

export function EditUnlockModal({ opened, onClose, unlock, existing }: Props) {
  return (
    <Modal
      opened={opened}
      onClose={onClose}
      title={unlock === null ? 'New rung' : `Edit ${unlock.displayName}`}
      size="lg"
    >
      {opened && (
        <EditUnlockForm
          key={unlock?.id ?? 'new'}
          onClose={onClose}
          unlock={unlock}
          existing={existing}
        />
      )}
    </Modal>
  )
}
