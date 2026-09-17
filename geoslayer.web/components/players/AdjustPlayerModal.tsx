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
import { IconAlertTriangle } from '@tabler/icons-react'
import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'

import { doQueryGet } from '@/helpers/apiClient'
import { useMutationPost } from '@/helpers/mutations/useMutationPost'
import { messageFrom, notifyError, notifySuccess } from '@/helpers/notificationHelper'
import { QueryKeys } from '@/helpers/QueryKeys'
import type { AdminItem } from '@/interfaces/api/admin/AdminItem'
import type { AdminMaterial } from '@/interfaces/api/admin/AdminMaterial'
import type { AdminPlayer } from '@/interfaces/api/admin/AdminPlayer'

interface Props {
  opened: boolean
  onClose: () => void
  player: AdminPlayer
}

type Target = 'coin' | 'material' | 'item'

/**
 * Grant or remove a player's coin, materials and items (Stage 18 task 9).
 *
 * **This is the only screen in the admin interface that edits player state**, and it is
 * shaped accordingly. Everywhere else a mistake is a retunable config value; here it is in
 * someone's balance.
 *
 * Uncapped, because an admin is trusted and the audit trail is the control. But the reason
 * field is required rather than optional — an audit entry that says "coin +5000" and
 * nothing else cannot answer the question it exists for.
 */
export function AdjustPlayerModal({ opened, onClose, player }: Props) {
  const [target, setTarget] = useState<Target>('coin')
  const [delta, setDelta] = useState<number>(0)
  const [reason, setReason] = useState('')
  const [materialId, setMaterialId] = useState<string | null>(null)
  const [itemId, setItemId] = useState<string | null>(null)

  const materials = useQuery<AdminMaterial[]>({
    queryKey: [QueryKeys.Materials],
    queryFn: async () => await doQueryGet<AdminMaterial[]>('/api/Admin/GetMaterials'),
    enabled: target === 'material',
  })

  const items = useQuery<AdminItem[]>({
    queryKey: [QueryKeys.Items],
    queryFn: async () => await doQueryGet<AdminItem[]>('/api/Admin/GetItems'),
    enabled: target === 'item',
  })

  const reset = () => {
    setDelta(0)
    setReason('')
    setMaterialId(null)
    setItemId(null)
  }

  const onSaved = () => {
    notifySuccess('Adjusted. Recorded in the audit trail.')
    reset()
    onClose()
  }

  const onFailed = (error: Error) => notifyError(messageFrom(error))

  const invalidate: string[][] = [[QueryKeys.Player], [QueryKeys.AuditTrail]]

  const adjustCoin = useMutationPost<
    { playerId: number; delta: number; reason: string },
    AdminPlayer
  >({
    url: '/api/Admin/AdjustPlayerCoin',
    queryKey: [QueryKeys.Player],
    invalidateQuery: true,
    alsoInvalidate: invalidate,
    onSuccess: onSaved,
    onError: onFailed,
  })

  const adjustMaterial = useMutationPost<
    { playerId: number; materialId: number; delta: number; reason: string },
    AdminPlayer
  >({
    url: '/api/Admin/AdjustPlayerMaterial',
    queryKey: [QueryKeys.Player],
    invalidateQuery: true,
    alsoInvalidate: invalidate,
    onSuccess: onSaved,
    onError: onFailed,
  })

  const adjustItem = useMutationPost<
    { playerId: number; itemId: number; delta: number; reason: string },
    AdminPlayer
  >({
    url: '/api/Admin/AdjustPlayerItem',
    queryKey: [QueryKeys.Player],
    invalidateQuery: true,
    alsoInvalidate: invalidate,
    onSuccess: onSaved,
    onError: onFailed,
  })

  const busy = adjustCoin.isPending || adjustMaterial.isPending || adjustItem.isPending

  const canSubmit =
    delta !== 0 &&
    reason.trim().length > 0 &&
    (target === 'coin' ||
      (target === 'material' && materialId !== null) ||
      (target === 'item' && itemId !== null))

  const submit = () => {
    const base = { playerId: player.playerId, delta, reason }

    if (target === 'coin') {
      adjustCoin.mutate(base)
    } else if (target === 'material' && materialId !== null) {
      adjustMaterial.mutate({ ...base, materialId: Number(materialId) })
    } else if (itemId !== null) {
      adjustItem.mutate({ ...base, itemId: Number(itemId) })
    }
  }

  return (
    <Modal opened={opened} onClose={onClose} title={`Adjust ${player.username}`} size="lg">
      <Stack>
        <Alert color="orange" icon={<IconAlertTriangle size={18} />} variant="light">
          <Text size="sm">
            This changes a real player&apos;s account. Every adjustment is recorded with who,
            what, when and why — the reason is what makes that record useful later.
          </Text>
        </Alert>

        <Select
          label="What to change"
          data={[
            { value: 'coin', label: 'Coin' },
            { value: 'material', label: 'A material' },
            { value: 'item', label: 'An item' },
          ]}
          value={target}
          onChange={(v) => {
            setTarget((v as Target) ?? 'coin')
            reset()
          }}
          allowDeselect={false}
        />

        {target === 'material' && (
          <Select
            label="Material"
            placeholder="Choose one"
            data={(materials.data ?? []).map((m) => ({
              value: String(m.id),
              label: `${m.name} (T${m.tier})`,
            }))}
            value={materialId}
            onChange={setMaterialId}
            searchable
          />
        )}

        {target === 'item' && (
          <Select
            label="Item"
            placeholder="Choose one"
            data={(items.data ?? []).map((i) => ({
              value: String(i.id),
              label: `${i.name} (T${i.tier})`,
            }))}
            value={itemId}
            onChange={setItemId}
            searchable
          />
        )}

        <NumberInput
          label="Amount"
          description="Negative removes. Removals stop at zero rather than going negative."
          value={delta}
          onChange={(v) => setDelta(Number(v) || 0)}
          allowNegative
        />

        {target === 'coin' && delta !== 0 && (
          <Text size="sm" c="dimmed">
            {player.coin.toLocaleString()}c → {Math.max(0, player.coin + delta).toLocaleString()}c
          </Text>
        )}

        <TextInput
          label="Reason"
          description="Recorded verbatim. A ticket number or a sentence — anything that answers 'why' in six months."
          placeholder="e.g. Support ticket 42 — coin lost to a failed sale"
          value={reason}
          onChange={(e) => setReason(e.currentTarget.value)}
          required
        />

        <Group justify="flex-end">
          <Button variant="default" onClick={onClose} disabled={busy}>
            Cancel
          </Button>
          <Button
            color={delta < 0 ? 'red' : undefined}
            onClick={submit}
            loading={busy}
            disabled={!canSubmit}
          >
            {delta < 0 ? 'Remove' : 'Grant'}
          </Button>
        </Group>
      </Stack>
    </Modal>
  )
}
