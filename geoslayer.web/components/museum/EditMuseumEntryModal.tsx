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
  Textarea,
} from '@mantine/core'
import { IconInfoCircle } from '@tabler/icons-react'
import { useState } from 'react'

import { useMutationPost } from '@/helpers/mutations/useMutationPost'
import { messageFrom, notifyError, notifySuccess } from '@/helpers/notificationHelper'
import { QueryKeys } from '@/helpers/QueryKeys'
import type { AdminMuseumEntry } from '@/interfaces/api/admin/AdminMuseumEntry'
import type { SaveMuseumEntryRequest } from '@/interfaces/api/admin/SaveMuseumEntryRequest'
import { asOptions, MuseumRarities, MuseumWings } from '@/interfaces/api/admin/ItemEnums'

interface Props {
  opened: boolean
  onClose: () => void
  /** Null when creating. */
  entry: AdminMuseumEntry | null
}

const EMPTY: SaveMuseumEntryRequest = {
  key: '',
  name: '',
  description: '',
  wing: 0,
  rarity: 0,
  unlockCondition: '',
  sortOrder: 0,
}

function initialForm(entry: AdminMuseumEntry | null): SaveMuseumEntryRequest {
  if (entry === null) return EMPTY

  return {
    id: entry.id,
    key: entry.key,
    name: entry.name,
    description: entry.description,
    wing: entry.wing,
    rarity: entry.rarity,
    unlockCondition: entry.unlockCondition,
    sortOrder: entry.sortOrder,
  }
}

/** Keyed on the entry by {@link EditMuseumEntryModal} — see EditItemModal for why. */
function EditMuseumEntryForm({
  onClose,
  entry,
}: {
  onClose: () => void
  entry: AdminMuseumEntry | null
}) {
  const [form, setForm] = useState<SaveMuseumEntryRequest>(() => initialForm(entry))

  const save = useMutationPost<SaveMuseumEntryRequest, AdminMuseumEntry>({
    url: '/api/Admin/SaveMuseumEntry',
    queryKey: [QueryKeys.MuseumEntries],
    invalidateQuery: true,
    alsoInvalidate: [[QueryKeys.AuditTrail]],
    onSuccess: (saved) => {
      if (saved !== undefined && saved.setWarnings.length > 0) {
        notifyError(saved.setWarnings.join(' '), 'Saved, but worth a look')
      } else {
        notifySuccess(entry === null ? 'Entry created.' : 'Entry saved.')
      }

      onClose()
    },
    onError: (error) => notifyError(messageFrom(error)),
  })

  const set = <K extends keyof SaveMuseumEntryRequest>(
    key: K,
    value: SaveMuseumEntryRequest[K]
  ) => setForm((prev) => ({ ...prev, [key]: value }))

  const isCartography = MuseumWings[form.wing] === 'Cartography'

  return (
    <Stack>
      <Group grow>
        <TextInput
          label="Key"
          description="What RecordFind matches on. Changing it on an existing entry orphans every find of it."
          value={form.key}
          onChange={(e) => set('key', e.currentTarget.value)}
          required
        />

        <TextInput
          label="Name"
          description="What the plinth is labelled."
          value={form.name}
          onChange={(e) => set('name', e.currentTarget.value)}
          required
        />
      </Group>

      <Textarea
        label="Description"
        description="The flavour text once it has been found."
        value={form.description}
        onChange={(e) => set('description', e.currentTarget.value)}
        autosize
        minRows={2}
      />

      <Group grow>
        <Select
          label="Wing"
          data={asOptions(MuseumWings)}
          value={String(form.wing)}
          onChange={(v) => set('wing', Number(v ?? 0))}
          allowDeselect={false}
        />

        <Select
          label="Rarity"
          description="Presentation only — it does not change how often it appears."
          data={asOptions(MuseumRarities)}
          value={String(form.rarity)}
          onChange={(v) => set('rarity', Number(v ?? 0))}
          allowDeselect={false}
        />

        <NumberInput
          label="Sort order"
          description="Position within the wing."
          value={form.sortOrder}
          onChange={(v) => set('sortOrder', Number(v) || 0)}
        />
      </Group>

      <Textarea
        label="How it is found"
        description="What the empty plinth says. §5A.1 stakes the whole system on this reading as a pull rather than as noise."
        placeholder="e.g. Visit a lighthouse."
        value={form.unlockCondition}
        onChange={(e) => set('unlockCondition', e.currentTarget.value)}
        autosize
        minRows={2}
        required
      />

      {/* Worth saying at the moment the wing is chosen rather than leaving someone to
          wonder why this one behaves differently. */}
      {isCartography && (
        <Alert color="blue" icon={<IconInfoCircle size={18} />} variant="light">
          <Text size="sm">
            Cartography entries are normally created as players discover regions, so the wing
            has no fixed size and can never be &quot;complete&quot; — which is why it has no
            completion bonus. Adding one by hand is fine, but it will not make the wing
            finishable.
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
          disabled={form.unlockCondition.trim() === ''}
        >
          Save
        </Button>
      </Group>
    </Stack>
  )
}

export function EditMuseumEntryModal({ opened, onClose, entry }: Props) {
  return (
    <Modal
      opened={opened}
      onClose={onClose}
      title={entry === null ? 'New Museum entry' : `Edit ${entry.name}`}
      size="lg"
    >
      {opened && (
        <EditMuseumEntryForm key={entry?.id ?? 'new'} onClose={onClose} entry={entry} />
      )}
    </Modal>
  )
}
