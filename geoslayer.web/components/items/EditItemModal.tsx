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
import type { AdminItem } from '@/interfaces/api/admin/AdminItem'
import type { SaveItemRequest } from '@/interfaces/api/admin/SaveItemRequest'
import {
  asOptions,
  ItemKinds,
  ItemModifiers,
  ItemSlots,
} from '@/interfaces/api/admin/ItemEnums'

interface Props {
  opened: boolean
  onClose: () => void
  /** Null when creating. */
  item: AdminItem | null
}

/**
 * What each modifier actually does, shown while choosing one.
 *
 * Mirrors `ModifierReaders` on the server. Duplicated deliberately rather than fetched:
 * this is guidance for a dropdown, and a round trip to populate a tooltip would be worse
 * than a copy that a server-side test keeps honest.
 */
const MODIFIER_EFFECTS: Record<string, string> = {
  SkillXpPercent: 'Raises XP from every source, by this fraction. 0.2 = +20%.',
  RevealRadius: 'Widens the corridor each step reveals, in cells.',
  PoiRangeMetres: 'Extends how far away a POI can be visited, trained at, or traded with.',
  OfflineCapHours: 'Extends both offline worker accrual and banking interest.',
  SellPricePercent: 'Raises what materials fetch at a shop. 0.5 = +50%.',
  WorkerRatePercent: 'Raises what offline workers produce.',
  ToolTier: 'Caps the highest tier this tool can gather. A whole number, 1–7.',
  GatherSpeedPercent: 'More units gathered per cell within a tier.',
  CombatPowerLevels: 'Fights as though Combat level were this much higher. In levels.',
}

const EMPTY: SaveItemRequest = {
  key: '',
  name: '',
  description: '',
  kind: 0,
  slot: 0,
  modifier: 0,
  modifierValue: 0,
  secondaryModifier: null,
  secondaryModifierValue: 0,
  tier: 1,
}

function initialForm(item: AdminItem | null): SaveItemRequest {
  if (item === null) return EMPTY

  return {
    id: item.id,
    key: item.key,
    name: item.name,
    description: item.description,
    kind: item.kind,
    slot: item.slot,
    modifier: item.modifier,
    modifierValue: item.modifierValue,
    secondaryModifier: item.secondaryModifier,
    secondaryModifierValue: item.secondaryModifierValue,
    tier: item.tier,
  }
}

/**
 * The form is keyed on the item in {@link EditItemModal}, so React discards and rebuilds
 * this component whenever a different item is opened. That is what resets the draft —
 * syncing it with an effect instead would be the cascading-render anti-pattern React warns
 * about, and would briefly render the previous item's values under the new title.
 */
function EditItemForm({ onClose, item }: { onClose: () => void; item: AdminItem | null }) {
  const [form, setForm] = useState<SaveItemRequest>(() => initialForm(item))

  const save = useMutationPost<SaveItemRequest, AdminItem>({
    url: '/api/Admin/SaveItem',
    queryKey: [QueryKeys.Items],
    invalidateQuery: true,
    alsoInvalidate: [[QueryKeys.AuditTrail]],
    onSuccess: () => {
      notifySuccess(item === null ? 'Item created.' : 'Item saved.')
      onClose()
    },
    // The API explains refusals precisely — an unread modifier names §4.3, a duplicate key
    // names the collision. Surfacing that beats a generic failure.
    onError: (error) => notifyError(messageFrom(error)),
  })

  const set = <K extends keyof SaveItemRequest>(key: K, value: SaveItemRequest[K]) =>
    setForm((prev) => ({ ...prev, [key]: value }))

  const modifierName = ItemModifiers[form.modifier] ?? ''

  return (
    <Stack>
      <TextInput
        label="Key"
        description="Referenced by recipes and seed data. Changing it on an existing item will break those references."
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

      <Textarea
        label="Description"
        description="Shown to players."
        value={form.description}
        onChange={(e) => set('description', e.currentTarget.value)}
        autosize
        minRows={2}
      />

      <Group grow>
        <Select
          label="Kind"
          data={asOptions(ItemKinds)}
          value={String(form.kind)}
          onChange={(v) => set('kind', Number(v ?? 0))}
          allowDeselect={false}
        />

        <Select
          label="Slot"
          description="Slots make gear a choice rather than merely additive (§4.3)."
          data={asOptions(ItemSlots)}
          value={String(form.slot)}
          onChange={(v) => set('slot', Number(v ?? 0))}
          allowDeselect={false}
        />

        <NumberInput
          label="Tier"
          value={form.tier}
          onChange={(v) => set('tier', Number(v) || 1)}
          min={1}
          max={7}
        />
      </Group>

      <Group grow align="flex-start">
        <Select
          label="Modifier"
          data={asOptions(ItemModifiers)}
          value={String(form.modifier)}
          onChange={(v) => set('modifier', Number(v ?? 0))}
          allowDeselect={false}
        />

        <NumberInput
          label="Value"
          description="A fraction for percentages, a whole number for tiers and levels."
          value={form.modifierValue}
          onChange={(v) => set('modifierValue', Number(v) || 0)}
          step={0.05}
          decimalScale={4}
        />
      </Group>

      {/* What this will actually do, before saving rather than after. */}
      {MODIFIER_EFFECTS[modifierName] !== undefined && (
        <Alert color="blue" icon={<IconInfoCircle size={18} />} variant="light">
          <Text size="sm">{MODIFIER_EFFECTS[modifierName]}</Text>
        </Alert>
      )}

      <Group grow align="flex-start">
        <Select
          label="Secondary modifier"
          description="Optional. Tools use this for gather speed alongside tier."
          data={asOptions(ItemModifiers)}
          value={form.secondaryModifier === null ? null : String(form.secondaryModifier)}
          onChange={(v) => set('secondaryModifier', v === null ? null : Number(v))}
          clearable
        />

        <NumberInput
          label="Secondary value"
          value={form.secondaryModifierValue}
          onChange={(v) => set('secondaryModifierValue', Number(v) || 0)}
          step={0.05}
          decimalScale={4}
          disabled={form.secondaryModifier === null}
        />
      </Group>

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

export function EditItemModal({ opened, onClose, item }: Props) {
  return (
    <Modal
      opened={opened}
      onClose={onClose}
      title={item === null ? 'New item' : `Edit ${item.name}`}
      size="lg"
    >
      {/* Keyed so opening a different item remounts the form with fresh state. */}
      {opened && <EditItemForm key={item?.id ?? 'new'} onClose={onClose} item={item} />}
    </Modal>
  )
}
