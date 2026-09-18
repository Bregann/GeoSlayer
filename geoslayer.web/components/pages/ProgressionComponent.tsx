'use client'

import {
  ActionIcon,
  Alert,
  Badge,
  Button,
  Group,
  Loader,
  Stack,
  Table,
  Text,
  Title,
  Tooltip,
} from '@mantine/core'
import { useDisclosure } from '@mantine/hooks'
import { IconAlertTriangle, IconPencil, IconPlus, IconTrash } from '@tabler/icons-react'
import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'

import { DeleteConfirmationModal } from '@/components/common/DeleteConfirmationModal'
import { EditUnlockModal } from '@/components/progression/EditUnlockModal'
import { EditUpgradeModal } from '@/components/progression/EditUpgradeModal'
import { doQueryGet } from '@/helpers/apiClient'
import { useMutationDelete } from '@/helpers/mutations/useMutationDelete'
import { messageFrom, notifyError, notifySuccess } from '@/helpers/notificationHelper'
import { QueryKeys } from '@/helpers/QueryKeys'
import type {
  AdminProgression,
  AdminUnlock,
  AdminUpgrade,
} from '@/interfaces/api/admin/AdminProgression'
import { UnlockTypes } from '@/interfaces/api/admin/ItemEnums'

/**
 * The unlock ladder and upgrade tree (Stage 18 task 8).
 *
 * Visibility first, editing second — the ladder's failure modes are things you spot by
 * *looking* at it: a payload unlocking twice, a rung at the wrong level, a curve whose
 * total nobody had added up.
 *
 * Total cost is computed server-side and shown because it is the number a comma-separated
 * curve makes hard to eyeball, and the one that decides whether an upgrade is worth buying.
 */
export default function ProgressionComponent() {
  const [editingUpgrade, setEditingUpgrade] = useState<AdminUpgrade | null>(null)
  const [editingUnlock, setEditingUnlock] = useState<AdminUnlock | null>(null)
  const [deletingUpgrade, setDeletingUpgrade] = useState<AdminUpgrade | null>(null)
  const [deletingUnlock, setDeletingUnlock] = useState<AdminUnlock | null>(null)

  const [upgradeOpen, { open: openUpgrade, close: closeUpgrade }] = useDisclosure(false)
  const [unlockOpen, { open: openUnlock, close: closeUnlock }] = useDisclosure(false)
  const [deleteUpgradeOpen, { open: openDeleteUpgrade, close: closeDeleteUpgrade }] =
    useDisclosure(false)
  const [deleteUnlockOpen, { open: openDeleteUnlock, close: closeDeleteUnlock }] =
    useDisclosure(false)

  const progression = useQuery<AdminProgression>({
    queryKey: [QueryKeys.Progression],
    queryFn: async () => await doQueryGet<AdminProgression>('/api/Admin/GetProgression'),
  })

  const removeUpgrade = useMutationDelete<number>({
    url: (id) => `/api/Admin/DeleteUpgrade?upgradeId=${id}`,
    queryKey: [QueryKeys.Progression],
    alsoInvalidate: [[QueryKeys.AuditTrail]],
    onSuccess: () => {
      notifySuccess('Upgrade deleted.')
      closeDeleteUpgrade()
    },
    // The API refuses when players have bought ranks, and says how many.
    onError: (error) => notifyError(messageFrom(error)),
  })

  const removeUnlock = useMutationDelete<number>({
    url: (id) => `/api/Admin/DeleteUnlock?unlockId=${id}`,
    queryKey: [QueryKeys.Progression],
    alsoInvalidate: [[QueryKeys.AuditTrail]],
    onSuccess: () => {
      notifySuccess('Rung deleted.')
      closeDeleteUnlock()
    },
    onError: (error) => notifyError(messageFrom(error)),
  })

  const data = progression.data

  return (
    <Stack>
      <Title order={2}>Progression</Title>

      <Text c="dimmed" size="sm">
        The Adventurer unlock ladder (§3.1) and the Bonus Point tree (§3.0a).
      </Text>

      {progression.isLoading && <Loader />}

      {progression.isError && (
        <Alert color="red" title="Could not load progression">
          {messageFrom(progression.error)}
        </Alert>
      )}

      {data && data.ladderWarnings.length > 0 && (
        <Alert color="yellow" icon={<IconAlertTriangle size={18} />} title="Worth a look">
          <Stack gap={4}>
            {data.ladderWarnings.map((warning) => (
              <Text key={warning} size="sm">
                {warning}
              </Text>
            ))}
          </Stack>
        </Alert>
      )}

      {data && (
        <>
          <Group justify="space-between">
            <Title order={4}>Unlock ladder</Title>

            <Button
              size="compact-sm"
              variant="light"
              leftSection={<IconPlus size={14} />}
              onClick={() => {
                setEditingUnlock(null)
                openUnlock()
              }}
            >
              New rung
            </Button>
          </Group>

          <Table striped withTableBorder>
            <Table.Thead>
              <Table.Tr>
                <Table.Th w={110}>Level</Table.Th>
                <Table.Th w={110}>Type</Table.Th>
                <Table.Th>Payload</Table.Th>
                <Table.Th>Shown as</Table.Th>
                <Table.Th w={90}></Table.Th>
              </Table.Tr>
            </Table.Thead>

            <Table.Tbody>
              {data.unlocks.map((unlock) => (
                <Table.Tr key={unlock.id}>
                  <Table.Td>{unlock.adventurerLevel}</Table.Td>
                  <Table.Td>
                    <Badge size="sm" variant="light">
                      {UnlockTypes[unlock.unlockType] ?? unlock.unlockType}
                    </Badge>
                  </Table.Td>
                  <Table.Td>
                    <Text ff="monospace" size="sm">
                      {unlock.payload}
                    </Text>
                  </Table.Td>
                  <Table.Td>{unlock.displayName}</Table.Td>
                  <Table.Td>
                    <Group gap={4} justify="flex-end" wrap="nowrap">
                      <Tooltip label="Edit">
                        <ActionIcon
                          variant="subtle"
                          onClick={() => {
                            setEditingUnlock(unlock)
                            openUnlock()
                          }}
                        >
                          <IconPencil size={16} />
                        </ActionIcon>
                      </Tooltip>

                      <Tooltip label="Delete">
                        <ActionIcon
                          variant="subtle"
                          color="red"
                          onClick={() => {
                            setDeletingUnlock(unlock)
                            openDeleteUnlock()
                          }}
                        >
                          <IconTrash size={16} />
                        </ActionIcon>
                      </Tooltip>
                    </Group>
                  </Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>

          <Group justify="space-between">
            <Title order={4}>Bonus Point upgrades</Title>

            <Button
              size="compact-sm"
              variant="light"
              leftSection={<IconPlus size={14} />}
              onClick={() => {
                setEditingUpgrade(null)
                openUpgrade()
              }}
            >
              New upgrade
            </Button>
          </Group>

          <Table striped withTableBorder>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Name</Table.Th>
                <Table.Th>Category</Table.Th>
                <Table.Th w={90}>Ranks</Table.Th>
                <Table.Th w={150}>Cost curve</Table.Th>
                <Table.Th w={90}>Total</Table.Th>
                <Table.Th w={110}>Per rank</Table.Th>
                <Table.Th w={110}>From level</Table.Th>
                <Table.Th w={90}></Table.Th>
              </Table.Tr>
            </Table.Thead>

            <Table.Tbody>
              {data.upgrades.map((upgrade) => (
                <Table.Tr key={upgrade.id}>
                  <Table.Td>
                    <Text size="sm">{upgrade.name}</Text>
                    <Text ff="monospace" size="xs" c="dimmed">
                      {upgrade.key}
                    </Text>
                  </Table.Td>
                  <Table.Td>{upgrade.category}</Table.Td>
                  <Table.Td>{upgrade.maxRank}</Table.Td>
                  <Table.Td>
                    <Text ff="monospace" size="sm">
                      {upgrade.costCurve}
                    </Text>
                  </Table.Td>
                  <Table.Td>
                    {/* The number the curve hides. */}
                    <Tooltip label="Bonus Points to max it">
                      <Text size="sm">{upgrade.totalCost}</Text>
                    </Tooltip>
                  </Table.Td>
                  <Table.Td>{upgrade.effectPerRank}</Table.Td>
                  <Table.Td>{upgrade.minAdventurerLevel}</Table.Td>
                  <Table.Td>
                    <Group gap={4} justify="flex-end" wrap="nowrap">
                      <Tooltip label="Edit">
                        <ActionIcon
                          variant="subtle"
                          onClick={() => {
                            setEditingUpgrade(upgrade)
                            openUpgrade()
                          }}
                        >
                          <IconPencil size={16} />
                        </ActionIcon>
                      </Tooltip>

                      <Tooltip label="Delete">
                        <ActionIcon
                          variant="subtle"
                          color="red"
                          onClick={() => {
                            setDeletingUpgrade(upgrade)
                            openDeleteUpgrade()
                          }}
                        >
                          <IconTrash size={16} />
                        </ActionIcon>
                      </Tooltip>
                    </Group>
                  </Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </>
      )}

      <EditUpgradeModal opened={upgradeOpen} onClose={closeUpgrade} upgrade={editingUpgrade} />

      <EditUnlockModal
        opened={unlockOpen}
        onClose={closeUnlock}
        unlock={editingUnlock}
        existing={data?.unlocks ?? []}
      />

      <DeleteConfirmationModal
        opened={deleteUpgradeOpen}
        onClose={closeDeleteUpgrade}
        title="Delete upgrade"
        body={
          deletingUpgrade
            ? `Delete "${deletingUpgrade.name}"? This is refused if any player has bought ranks in it.`
            : ''
        }
        loading={removeUpgrade.isPending}
        onConfirm={() => deletingUpgrade && removeUpgrade.mutate(deletingUpgrade.id)}
      />

      <DeleteConfirmationModal
        opened={deleteUnlockOpen}
        onClose={closeDeleteUnlock}
        title="Delete rung"
        body={
          deletingUnlock
            ? `Delete the level ${deletingUnlock.adventurerLevel} unlock for "${deletingUnlock.payload}"? Players who already have it keep it; only future players are affected.`
            : ''
        }
        loading={removeUnlock.isPending}
        onConfirm={() => deletingUnlock && removeUnlock.mutate(deletingUnlock.id)}
      />
    </Stack>
  )
}
