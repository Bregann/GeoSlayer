'use client'

import {
  Alert,
  Button,
  Checkbox,
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
import type { AdminMaterial } from '@/interfaces/api/admin/AdminMaterial'
import type { SaveMaterialRequest } from '@/interfaces/api/admin/SaveMaterialRequest'
import { asOptions, MaterialCategories, SkillTypes } from '@/interfaces/api/admin/ItemEnums'

interface Props {
  opened: boolean
  onClose: () => void
  /** Null when creating. */
  material: AdminMaterial | null
}

const EMPTY: SaveMaterialRequest = {
  key: '',
  name: '',
  category: 0,
  skillType: null,
  tier: 1,
  levelRequired: 1,
  baseGatherSeconds: 3,
  xpPerUnit: 5,
  isUnique: false,
}

/**
 * The prescribed ladder from SKILL-TEMPLATE.md §1a.
 *
 * Shown as guidance rather than enforced: the server refuses a ladder that *inverts*, but
 * these exact numbers are a template, not a law. An admin deviating deliberately should be
 * able to, and an admin deviating accidentally should be able to see it.
 */
const TEMPLATE: Record<number, { level: number; seconds: number; xp: number }> = {
  1: { level: 1, seconds: 3, xp: 5 },
  2: { level: 10, seconds: 5, xp: 12 },
  3: { level: 20, seconds: 9, xp: 25 },
  4: { level: 35, seconds: 15, xp: 48 },
  5: { level: 50, seconds: 24, xp: 85 },
  6: { level: 70, seconds: 40, xp: 150 },
  7: { level: 90, seconds: 60, xp: 240 },
}

function initialForm(material: AdminMaterial | null): SaveMaterialRequest {
  if (material === null) return EMPTY

  return {
    id: material.id,
    key: material.key,
    name: material.name,
    category: material.category,
    skillType: material.skillType,
    tier: material.tier,
    levelRequired: material.levelRequired,
    baseGatherSeconds: material.baseGatherSeconds,
    xpPerUnit: material.xpPerUnit,
    isUnique: material.isUnique,
  }
}

/**
 * Keyed on the material by {@link EditMaterialModal}, so React rebuilds it when a different
 * one is opened — see EditItemModal for why that beats syncing state in an effect.
 */
function EditMaterialForm({
  onClose,
  material,
}: {
  onClose: () => void
  material: AdminMaterial | null
}) {
  const [form, setForm] = useState<SaveMaterialRequest>(() => initialForm(material))

  const save = useMutationPost<SaveMaterialRequest, AdminMaterial>({
    url: '/api/Admin/SaveMaterial',
    queryKey: [QueryKeys.Materials],
    invalidateQuery: true,
    alsoInvalidate: [[QueryKeys.AuditTrail]],
    onSuccess: () => {
      notifySuccess(material === null ? 'Material created.' : 'Material saved.')
      onClose()
    },
    // The server names the invariant that was broken — which rung collides, which skill
    // owns the category. That sentence is the whole value of the refusal.
    onError: (error) => notifyError(messageFrom(error)),
  })

  const set = <K extends keyof SaveMaterialRequest>(key: K, value: SaveMaterialRequest[K]) =>
    setForm((prev) => ({ ...prev, [key]: value }))

  const template = TEMPLATE[form.tier]

  const offTemplate =
    template !== undefined &&
    (form.levelRequired !== template.level ||
      form.baseGatherSeconds !== template.seconds ||
      form.xpPerUnit !== template.xp)

  const xpPerSecond =
    form.baseGatherSeconds > 0
      ? Math.round((form.xpPerUnit / form.baseGatherSeconds) * 1000) / 1000
      : 0

  return (
    <Stack>
      <TextInput
        label="Key"
        description="Referenced by recipes and drop tables. Changing it on an existing material breaks those references."
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

      <Group grow>
        <Select
          label="Category"
          description="Each skill ladder needs its own — two ladders in one category compete for the same tier band."
          data={asOptions(MaterialCategories)}
          value={String(form.category)}
          onChange={(v) => set('category', Number(v ?? 0))}
          allowDeselect={false}
        />

        <Select
          label="Skill"
          description="Leave empty for a universal material like Dust."
          data={asOptions(SkillTypes)}
          value={form.skillType === null ? null : String(form.skillType)}
          onChange={(v) => set('skillType', v === null ? null : Number(v))}
          clearable
        />
      </Group>

      <Group grow>
        <NumberInput
          label="Tier"
          description="1–7. One material per rung."
          value={form.tier}
          onChange={(v) => set('tier', Number(v) || 1)}
          min={1}
          max={7}
        />

        <NumberInput
          label="Level required"
          description="Absolute — below it the material is unobtainable, not merely rare (§4.1a)."
          value={form.levelRequired}
          onChange={(v) => set('levelRequired', Number(v) || 1)}
          min={1}
        />
      </Group>

      <Group grow>
        <NumberInput
          label="Gather seconds"
          description="Must rise with tier."
          value={form.baseGatherSeconds}
          onChange={(v) => set('baseGatherSeconds', Number(v) || 1)}
          min={0.1}
          step={1}
          decimalScale={2}
        />

        <NumberInput
          label="XP per unit"
          description={`Currently ${xpPerSecond} XP/second.`}
          value={form.xpPerUnit}
          onChange={(v) => set('xpPerUnit', Number(v) || 0)}
          min={0}
          step={1}
          decimalScale={4}
        />
      </Group>

      {/* Guidance, not a rule. The server enforces that the ladder does not invert; the
          template is what the seeded ladders follow. */}
      {offTemplate && template !== undefined && (
        <Alert color="yellow" icon={<IconInfoCircle size={18} />} variant="light">
          <Text size="sm">
            The standard tier {form.tier} rung is level {template.level}, {template.seconds}s,{' '}
            {template.xp} XP. Deviating is allowed — the server only refuses a ladder that
            goes backwards — but the seeded ladders all follow this shape.
          </Text>
        </Alert>
      )}

      <Checkbox
        label="Unique"
        description="From the rarest POIs only. Exclusivity means nothing if everything is exclusive (§7.4)."
        checked={form.isUnique}
        onChange={(e) => set('isUnique', e.currentTarget.checked)}
      />

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

export function EditMaterialModal({ opened, onClose, material }: Props) {
  return (
    <Modal
      opened={opened}
      onClose={onClose}
      title={material === null ? 'New material' : `Edit ${material.name}`}
      size="lg"
    >
      {opened && (
        <EditMaterialForm key={material?.id ?? 'new'} onClose={onClose} material={material} />
      )}
    </Modal>
  )
}
