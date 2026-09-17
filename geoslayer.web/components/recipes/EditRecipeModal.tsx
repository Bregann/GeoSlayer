'use client'

import {
  ActionIcon,
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
import { IconPlus, IconTrash } from '@tabler/icons-react'
import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'

import { doQueryGet } from '@/helpers/apiClient'
import { useMutationPost } from '@/helpers/mutations/useMutationPost'
import { messageFrom, notifyError, notifySuccess } from '@/helpers/notificationHelper'
import { QueryKeys } from '@/helpers/QueryKeys'
import type { AdminItem } from '@/interfaces/api/admin/AdminItem'
import type { AdminMaterial } from '@/interfaces/api/admin/AdminMaterial'
import type { AdminRecipe } from '@/interfaces/api/admin/AdminRecipe'
import type {
  SaveRecipeInput,
  SaveRecipeRequest,
} from '@/interfaces/api/admin/SaveRecipeRequest'
import { asOptions, SkillTypes } from '@/interfaces/api/admin/ItemEnums'

interface Props {
  opened: boolean
  onClose: () => void
  /** Null when creating. */
  recipe: AdminRecipe | null
}

const EMPTY: SaveRecipeRequest = {
  key: '',
  name: '',
  description: '',
  skillType: 0,
  levelRequired: 1,
  durationSeconds: 600,
  xpReward: 10,
  outputMaterialId: null,
  outputItemId: null,
  outputQuantity: 1,
  inputs: [],
}

function initialForm(recipe: AdminRecipe | null): SaveRecipeRequest {
  if (recipe === null) return EMPTY

  return {
    id: recipe.id,
    key: recipe.key,
    name: recipe.name,
    description: recipe.description,
    skillType: recipe.skillType,
    levelRequired: recipe.levelRequired,
    durationSeconds: recipe.durationSeconds,
    xpReward: recipe.xpReward,
    outputMaterialId: recipe.outputMaterialId,
    outputItemId: recipe.outputItemId,
    outputQuantity: recipe.outputQuantity,
    inputs: recipe.inputs.map((i) => ({ materialId: i.materialId, quantity: i.quantity })),
  }
}

/** Keyed on the recipe by {@link EditRecipeModal} — see EditItemModal for why. */
function EditRecipeForm({
  onClose,
  recipe,
}: {
  onClose: () => void
  recipe: AdminRecipe | null
}) {
  const [form, setForm] = useState<SaveRecipeRequest>(() => initialForm(recipe))

  // Prefetched by the page, so these resolve from cache rather than flashing empty.
  const materials = useQuery<AdminMaterial[]>({
    queryKey: [QueryKeys.Materials],
    queryFn: async () => await doQueryGet<AdminMaterial[]>('/api/Admin/GetMaterials'),
  })

  const items = useQuery<AdminItem[]>({
    queryKey: [QueryKeys.Items],
    queryFn: async () => await doQueryGet<AdminItem[]>('/api/Admin/GetItems'),
  })

  const save = useMutationPost<SaveRecipeRequest, AdminRecipe>({
    url: '/api/Admin/SaveRecipe',
    queryKey: [QueryKeys.Recipes],
    invalidateQuery: true,
    alsoInvalidate: [[QueryKeys.AuditTrail]],
    onSuccess: (saved) => {
      // A warning is not a failure — the recipe saved. But surfacing it here is the only
      // moment the admin is definitely looking.
      if (saved !== undefined && saved.warnings.length > 0) {
        notifyError(saved.warnings.join(' '), 'Saved, but worth a look')
      } else {
        notifySuccess(recipe === null ? 'Recipe created.' : 'Recipe saved.')
      }

      onClose()
    },
    onError: (error) => notifyError(messageFrom(error)),
  })

  const set = <K extends keyof SaveRecipeRequest>(key: K, value: SaveRecipeRequest[K]) =>
    setForm((prev) => ({ ...prev, [key]: value }))

  const setInput = (index: number, patch: Partial<SaveRecipeInput>) =>
    setForm((prev) => ({
      ...prev,
      inputs: prev.inputs.map((input, i) => (i === index ? { ...input, ...patch } : input)),
    }))

  const materialOptions = (materials.data ?? []).map((m) => ({
    value: String(m.id),
    label: `${m.name} (T${m.tier}, ${m.unitPrice}c)`,
  }))

  const itemOptions = (items.data ?? []).map((i) => ({
    value: String(i.id),
    label: `${i.name} (T${i.tier})`,
  }))

  // Running total, so a loss-making recipe is visible while it is being built rather than
  // only after saving.
  const inputCost = form.inputs.reduce((sum, input) => {
    const material = (materials.data ?? []).find((m) => m.id === input.materialId)
    return sum + (material?.unitPrice ?? 0) * input.quantity
  }, 0)

  const outputMaterial = (materials.data ?? []).find((m) => m.id === form.outputMaterialId)
  const outputValue = (outputMaterial?.unitPrice ?? 0) * form.outputQuantity

  const losesMoney = outputValue > 0 && inputCost > 0 && outputValue <= inputCost

  return (
    <Stack>
      <Group grow>
        <TextInput
          label="Key"
          description="Referenced by seed data."
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
        value={form.description}
        onChange={(e) => set('description', e.currentTarget.value)}
        autosize
        minRows={2}
      />

      <Group grow>
        <Select
          label="Skill"
          description="Unlocked on the ladder, then at level — a double gate (§4.2)."
          data={asOptions(SkillTypes)}
          value={String(form.skillType)}
          onChange={(v) => set('skillType', Number(v ?? 0))}
          allowDeselect={false}
        />

        <NumberInput
          label="Level required"
          value={form.levelRequired}
          onChange={(v) => set('levelRequired', Number(v) || 1)}
          min={1}
        />

        <NumberInput
          label="Duration (seconds)"
          description="Time-gated, not tap-gated — you queue it and walk away."
          value={form.durationSeconds}
          onChange={(v) => set('durationSeconds', Number(v) || 1)}
          min={1}
        />

        <NumberInput
          label="XP reward"
          value={form.xpReward}
          onChange={(v) => set('xpReward', Number(v) || 0)}
          min={0}
        />
      </Group>

      {/* A recipe makes one thing. Choosing a material clears the item and vice versa, so
          the "one or the other" rule cannot be broken from here. */}
      <Group grow align="flex-start">
        <Select
          label="Output material"
          description="Or choose an item instead."
          data={materialOptions}
          value={form.outputMaterialId === null ? null : String(form.outputMaterialId)}
          onChange={(v) =>
            setForm((prev) => ({
              ...prev,
              outputMaterialId: v === null ? null : Number(v),
              outputItemId: v === null ? prev.outputItemId : null,
            }))
          }
          searchable
          clearable
        />

        <Select
          label="Output item"
          data={itemOptions}
          value={form.outputItemId === null ? null : String(form.outputItemId)}
          onChange={(v) =>
            setForm((prev) => ({
              ...prev,
              outputItemId: v === null ? null : Number(v),
              outputMaterialId: v === null ? prev.outputMaterialId : null,
            }))
          }
          searchable
          clearable
        />

        <NumberInput
          label="Output quantity"
          value={form.outputQuantity}
          onChange={(v) => set('outputQuantity', Number(v) || 1)}
          min={1}
        />
      </Group>

      <Group justify="space-between">
        <Text fw={600} size="sm">
          Inputs
        </Text>

        <Button
          size="xs"
          variant="light"
          leftSection={<IconPlus size={14} />}
          onClick={() =>
            set('inputs', [...form.inputs, { materialId: 0, quantity: 1 }])
          }
        >
          Add input
        </Button>
      </Group>

      {form.inputs.length === 0 && (
        <Text c="dimmed" size="sm">
          A recipe needs at least one input, or it produces something from nothing.
        </Text>
      )}

      {form.inputs.map((input, index) => (
        <Group key={index} align="flex-end" gap="xs">
          <Select
            flex={1}
            label={index === 0 ? 'Material' : undefined}
            data={materialOptions}
            value={input.materialId === 0 ? null : String(input.materialId)}
            onChange={(v) => setInput(index, { materialId: Number(v ?? 0) })}
            searchable
          />

          <NumberInput
            w={110}
            label={index === 0 ? 'Quantity' : undefined}
            value={input.quantity}
            onChange={(v) => setInput(index, { quantity: Number(v) || 1 })}
            min={1}
          />

          <ActionIcon
            color="red"
            variant="subtle"
            mb={4}
            onClick={() =>
              set('inputs', form.inputs.filter((_, i) => i !== index))
            }
          >
            <IconTrash size={16} />
          </ActionIcon>
        </Group>
      ))}

      {/* The economy, live. Shown while building rather than only on save, because the
          fix is usually to change a quantity here and now. */}
      <Alert color={losesMoney ? 'yellow' : 'gray'} variant="light">
        <Text size="sm">
          Inputs are worth {inputCost}c
          {outputValue > 0 ? `, output ${outputValue}c` : '. The output is an item, whose worth is its modifier rather than a price'}
          {losesMoney
            ? ' — this loses money, and §5D.1 expects produced goods to beat their parts.'
            : '.'}
        </Text>
      </Alert>

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

export function EditRecipeModal({ opened, onClose, recipe }: Props) {
  return (
    <Modal
      opened={opened}
      onClose={onClose}
      title={recipe === null ? 'New recipe' : `Edit ${recipe.name}`}
      size="xl"
    >
      {opened && <EditRecipeForm key={recipe?.id ?? 'new'} onClose={onClose} recipe={recipe} />}
    </Modal>
  )
}
