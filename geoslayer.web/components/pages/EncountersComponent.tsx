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
import { useMemo, useState } from 'react'

import { DeleteConfirmationModal } from '@/components/common/DeleteConfirmationModal'
import { EditEncounterModal } from '@/components/encounters/EditEncounterModal'
import { doQueryGet } from '@/helpers/apiClient'
import { useMutationDelete } from '@/helpers/mutations/useMutationDelete'
import { messageFrom, notifyError, notifySuccess } from '@/helpers/notificationHelper'
import { QueryKeys } from '@/helpers/QueryKeys'
import type { AdminEncounter } from '@/interfaces/api/admin/AdminEncounter'

const LADDER_TIERS = 7

/**
 * Encounter management (Stage 18 task 8).
 *
 * The coverage strip at the top is the point of this screen. §5C.2's rule — a player with
 * no castle must be able to train Combat to the top — is invisible in a flat table: you
 * would have to read every row and hold seven tiers in your head to notice a gap.
 *
 * The server refuses a save or delete that would open one. This makes the state visible so
 * those refusals are never a surprise.
 */
export default function EncountersComponent() {
  const [editing, setEditing] = useState<AdminEncounter | null>(null)
  const [deleting, setDeleting] = useState<AdminEncounter | null>(null)

  const [editOpen, { open: openEdit, close: closeEdit }] = useDisclosure(false)
  const [deleteOpen, { open: openDelete, close: closeDelete }] = useDisclosure(false)

  const encounters = useQuery<AdminEncounter[]>({
    queryKey: [QueryKeys.Encounters],
    queryFn: async () => await doQueryGet<AdminEncounter[]>('/api/Admin/GetEncounters'),
  })

  const remove = useMutationDelete<number>({
    url: (id) => `/api/Admin/DeleteEncounter?encounterId=${id}`,
    queryKey: [QueryKeys.Encounters],
    alsoInvalidate: [[QueryKeys.AuditTrail]],
    onSuccess: () => {
      notifySuccess('Encounter deleted.')
      closeDelete()
    },
    // The API explains which tier would be stranded, and why that matters.
    onError: (error) => notifyError(messageFrom(error)),
  })

  /** How many roaming encounters cover each tier — the §5C.2 view. */
  const coverage = useMemo(() => {
    const list = encounters.data ?? []

    return Array.from({ length: LADDER_TIERS }, (_, index) => {
      const tier = index + 1

      return {
        tier,
        roaming: list.filter((e) => !e.isTrainingGround && e.tier === tier).length,
        training: list.filter((e) => e.isTrainingGround && e.tier === tier).length,
      }
    })
  }, [encounters.data])

  const warnings = encounters.data?.[0]?.setWarnings ?? []

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={2}>Encounters</Title>

        <Button
          leftSection={<IconPlus size={16} />}
          onClick={() => {
            setEditing(null)
            openEdit()
          }}
        >
          New encounter
        </Button>
      </Group>

      <Text c="dimmed" size="sm">
        Every Combat tier needs at least one <strong>roaming</strong> encounter. Historic
        ground is a boost, never the only venue (§5C.2) — a player with no castle nearby must
        still reach the top of the skill.
      </Text>

      {/* The rule, made visible. A red cell here is a tier nobody without a castle can
          train past, and the server will refuse to let you create one. */}
      <Group gap="xs">
        {coverage.map(({ tier, roaming, training }) => (
          <Tooltip
            key={tier}
            label={
              roaming === 0
                ? `Tier ${tier}: no roaming encounter — castle-less players are stuck here`
                : `Tier ${tier}: ${roaming} roaming, ${training} training ground${training === 1 ? '' : 's'}`
            }
          >
            <Badge
              size="lg"
              color={roaming === 0 ? 'red' : training === 0 ? 'blue' : 'teal'}
              variant={roaming === 0 ? 'filled' : 'light'}
            >
              T{tier} · {roaming}
              {training > 0 ? ` (+${training})` : ''}
            </Badge>
          </Tooltip>
        ))}
      </Group>

      {warnings.length > 0 && (
        <Alert color="yellow" icon={<IconAlertTriangle size={18} />} title="Worth a look">
          <Stack gap={4}>
            {warnings.map((warning) => (
              <Text key={warning} size="sm">
                {warning}
              </Text>
            ))}
          </Stack>
        </Alert>
      )}

      {encounters.isLoading && <Loader />}

      {encounters.isError && (
        <Alert color="red" title="Could not load encounters">
          {messageFrom(encounters.error)}
        </Alert>
      )}

      {encounters.data && (
        <Table striped highlightOnHover withTableBorder>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Name</Table.Th>
              <Table.Th>Key</Table.Th>
              <Table.Th>Tier</Table.Th>
              <Table.Th>Combat level</Table.Th>
              <Table.Th>Kind</Table.Th>
              <Table.Th w={90}></Table.Th>
            </Table.Tr>
          </Table.Thead>

          <Table.Tbody>
            {encounters.data.map((encounter) => (
              <Table.Tr key={encounter.id}>
                <Table.Td>{encounter.name}</Table.Td>

                <Table.Td>
                  <Text ff="monospace" size="sm">
                    {encounter.key}
                  </Text>
                </Table.Td>

                <Table.Td>{encounter.tier}</Table.Td>
                <Table.Td>{encounter.minCombatLevel}</Table.Td>

                <Table.Td>
                  <Badge
                    size="sm"
                    color={encounter.isTrainingGround ? 'grape' : 'blue'}
                    variant="light"
                  >
                    {encounter.isTrainingGround ? 'training ground' : 'roaming'}
                  </Badge>
                </Table.Td>

                <Table.Td>
                  <Group gap={4} justify="flex-end" wrap="nowrap">
                    <Tooltip label="Edit">
                      <ActionIcon
                        variant="subtle"
                        onClick={() => {
                          setEditing(encounter)
                          openEdit()
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
                          setDeleting(encounter)
                          openDelete()
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
      )}

      <EditEncounterModal opened={editOpen} onClose={closeEdit} encounter={editing} />

      <DeleteConfirmationModal
        opened={deleteOpen}
        onClose={closeDelete}
        title="Delete encounter"
        body={
          deleting
            ? `Delete "${deleting.name}"? If it is the only roaming encounter at tier ${deleting.tier}, this will be refused.`
            : ''
        }
        loading={remove.isPending}
        onConfirm={() => deleting && remove.mutate(deleting.id)}
      />
    </Stack>
  )
}
