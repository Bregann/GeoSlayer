'use client'

import { Alert, Badge, Loader, Stack, Table, Text, Title, Tooltip } from '@mantine/core'
import { IconAlertTriangle } from '@tabler/icons-react'
import { useQuery } from '@tanstack/react-query'

import { doQueryGet } from '@/helpers/apiClient'
import { messageFrom } from '@/helpers/notificationHelper'
import { QueryKeys } from '@/helpers/QueryKeys'
import type { AdminProgression } from '@/interfaces/api/admin/AdminProgression'
import { UnlockTypes } from '@/interfaces/api/admin/ItemEnums'

/**
 * The unlock ladder and upgrade tree (Stage 18 task 8).
 *
 * Read-only for now. Both halves have save endpoints, but the editing UI is worth less than
 * the visibility: the ladder's failure modes are things you spot by *looking* at it — a
 * payload unlocking twice, a rung at the wrong level, a curve whose total nobody had added
 * up.
 *
 * Total cost is computed server-side and shown because it is the number a comma-separated
 * curve makes hard to eyeball, and the one that decides whether an upgrade is worth buying.
 */
export default function ProgressionComponent() {
  const progression = useQuery<AdminProgression>({
    queryKey: [QueryKeys.Progression],
    queryFn: async () => await doQueryGet<AdminProgression>('/api/Admin/GetProgression'),
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
          <Title order={4}>Unlock ladder</Title>

          <Table striped withTableBorder>
            <Table.Thead>
              <Table.Tr>
                <Table.Th w={110}>Level</Table.Th>
                <Table.Th w={110}>Type</Table.Th>
                <Table.Th>Payload</Table.Th>
                <Table.Th>Shown as</Table.Th>
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
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>

          <Title order={4}>Bonus Point upgrades</Title>

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
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </>
      )}
    </Stack>
  )
}
