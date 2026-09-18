'use client'

import {
  Alert,
  Button,
  Group,
  Modal,
  NumberInput,
  Stack,
  Text,
  TextInput,
  Textarea,
} from '@mantine/core'
import { IconAlertTriangle, IconInfoCircle } from '@tabler/icons-react'
import { useState } from 'react'

import { useMutationPost } from '@/helpers/mutations/useMutationPost'
import { messageFrom, notifyError, notifySuccess } from '@/helpers/notificationHelper'
import { QueryKeys } from '@/helpers/QueryKeys'
import type { AdminUpgrade } from '@/interfaces/api/admin/AdminProgression'
import type { SaveUpgradeRequest } from '@/interfaces/api/admin/SaveUpgradeRequest'

interface Props {
  opened: boolean
  onClose: () => void
  /** Null when creating. */
  upgrade: AdminUpgrade | null
}

const EMPTY: SaveUpgradeRequest = {
  key: '',
  name: '',
  category: '',
  description: '',
  maxRank: 1,
  costCurve: '1',
  effectPerRank: 1,
  minAdventurerLevel: 1,
}

function initialForm(upgrade: AdminUpgrade | null): SaveUpgradeRequest {
  if (upgrade === null) return EMPTY

  return {
    id: upgrade.id,
    key: upgrade.key,
    name: upgrade.name,
    category: upgrade.category,
    description: upgrade.description,
    maxRank: upgrade.maxRank,
    costCurve: upgrade.costCurve,
    effectPerRank: upgrade.effectPerRank,
    minAdventurerLevel: upgrade.minAdventurerLevel,
  }
}

/**
 * Reads a cost curve the way the server does, without throwing.
 *
 * The server refuses a malformed curve outright, but an admin typing one needs to see what
 * is wrong *while* typing rather than on a rejected save. This mirrors
 * `ProgressionValidation.RejectCostCurve` closely enough to be useful and is deliberately
 * not authoritative — the save is still validated server-side.
 */
function readCurve(curve: string, maxRank: number): { costs: number[]; problem: string | null } {
  const parts = curve
    .split(',')
    .map((p) => p.trim())
    .filter((p) => p !== '')

  if (parts.length === 0) {
    return { costs: [], problem: 'A cost curve needs at least one value.' }
  }

  const costs: number[] = []

  for (const part of parts) {
    // The server parses with int.Parse, which throws — and Costs is read on every
    // upgrades-screen load, so a stored typo takes that screen down for every player.
    if (!/^-?\d+$/.test(part)) {
      return { costs: [], problem: `'${part}' is not a whole number.` }
    }

    const cost = Number(part)

    if (cost < 1) {
      return { costs: [], problem: `Rank costs must be at least 1; '${part}' is not.` }
    }

    costs.push(cost)
  }

  if (costs.length !== maxRank) {
    return {
      costs,
      problem: `The curve has ${costs.length} value${costs.length === 1 ? '' : 's'} but max rank is ${maxRank}.`,
    }
  }

  for (let i = 1; i < costs.length; i++) {
    if (costs[i] < costs[i - 1]) {
      return {
        costs,
        problem: `Rank ${i + 1} costs ${costs[i]} but rank ${i} costs ${costs[i - 1]} — §3.0a makes costs escalate.`,
      }
    }
  }

  return { costs, problem: null }
}

/** Keyed on the upgrade by {@link EditUpgradeModal} — see EditItemModal for why. */
function EditUpgradeForm({
  onClose,
  upgrade,
}: {
  onClose: () => void
  upgrade: AdminUpgrade | null
}) {
  const [form, setForm] = useState<SaveUpgradeRequest>(() => initialForm(upgrade))

  const save = useMutationPost<SaveUpgradeRequest, AdminUpgrade>({
    url: '/api/Admin/SaveUpgrade',
    queryKey: [QueryKeys.Progression],
    invalidateQuery: true,
    alsoInvalidate: [[QueryKeys.AuditTrail]],
    onSuccess: () => {
      notifySuccess(upgrade === null ? 'Upgrade created.' : 'Upgrade saved.')
      onClose()
    },
    onError: (error) => notifyError(messageFrom(error)),
  })

  const set = <K extends keyof SaveUpgradeRequest>(key: K, value: SaveUpgradeRequest[K]) =>
    setForm((prev) => ({ ...prev, [key]: value }))

  const { costs, problem } = readCurve(form.costCurve, form.maxRank)
  const total = costs.reduce((sum, cost) => sum + cost, 0)

  return (
    <Stack>
      <Group grow>
        <TextInput
          label="Key"
          description="Referenced by PlayerUpgrade rows. Changing it on an existing upgrade orphans everyone's ranks."
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

      <Group grow>
        <TextInput
          label="Category"
          description="Groups the tree — Workers, Idle, Exploration, Territory, Yield."
          value={form.category}
          onChange={(e) => set('category', e.currentTarget.value)}
        />

        <NumberInput
          label="Minimum Adventurer level"
          description="Gates when it appears, so the tree reveals itself gradually (§3.0a)."
          value={form.minAdventurerLevel}
          onChange={(v) => set('minAdventurerLevel', Number(v) || 1)}
          min={1}
        />
      </Group>

      <Textarea
        label="Description"
        description="Shown to players in the upgrade menu."
        value={form.description}
        onChange={(e) => set('description', e.currentTarget.value)}
        autosize
        minRows={2}
      />

      <Group grow align="flex-start">
        <NumberInput
          label="Max rank"
          value={form.maxRank}
          onChange={(v) => set('maxRank', Number(v) || 1)}
          min={1}
        />

        <NumberInput
          label="Effect per rank"
          description="Units depend on the upgrade — cells, hours, a fraction."
          value={form.effectPerRank}
          onChange={(v) => set('effectPerRank', Number(v) || 0)}
          step={0.05}
          decimalScale={4}
        />
      </Group>

      <TextInput
        label="Cost curve"
        description="Bonus Points per rank, comma-separated. One value per rank, never decreasing."
        placeholder="1,2,4,7,11"
        value={form.costCurve}
        onChange={(e) => set('costCurve', e.currentTarget.value)}
        error={problem}
        required
      />

      {/* The number the curve hides, shown while typing. The server computes the same thing
          on save; this is so an admin does not have to add it up in their head. */}
      {problem === null && (
        <Alert color="blue" icon={<IconInfoCircle size={18} />} variant="light">
          <Text size="sm">
            {total} Bonus Points to max — {costs.join(' + ')}. That is {total} Adventurer
            levels&apos; worth.
          </Text>
        </Alert>
      )}

      {problem !== null && (
        <Alert color="orange" icon={<IconAlertTriangle size={18} />} variant="light">
          <Text size="sm">
            {problem} The server will refuse this — a malformed curve is parsed on every
            upgrades-screen load, so it would crash that screen for every player.
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
          disabled={problem !== null}
        >
          Save
        </Button>
      </Group>
    </Stack>
  )
}

export function EditUpgradeModal({ opened, onClose, upgrade }: Props) {
  return (
    <Modal
      opened={opened}
      onClose={onClose}
      title={upgrade === null ? 'New upgrade' : `Edit ${upgrade.name}`}
      size="lg"
    >
      {opened && <EditUpgradeForm key={upgrade?.id ?? 'new'} onClose={onClose} upgrade={upgrade} />}
    </Modal>
  )
}
