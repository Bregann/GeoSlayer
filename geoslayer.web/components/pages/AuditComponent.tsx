'use client'

import { Alert, Badge, Loader, Stack, Table, Text, Title } from '@mantine/core'
import { useQuery } from '@tanstack/react-query'

import { doQueryGet } from '@/helpers/apiClient'
import { messageFrom } from '@/helpers/notificationHelper'
import { QueryKeys } from '@/helpers/QueryKeys'
import type { AdminAudit } from '@/interfaces/api/admin/AdminAudit'

/** Destructive actions are coloured so they can be found by scanning. */
const ACTION_COLOURS: Record<string, string> = {
  Created: 'teal',
  Updated: 'blue',
  Deleted: 'red',
  ImageUploaded: 'grape',
  ImageReplaced: 'grape',
  ImageDeleted: 'orange',
}

/**
 * The audit trail (Stage 18 task 9).
 *
 * Read-only by design. An audit trail that can be edited is a log, not a trail — nothing
 * here offers a way to change or remove an entry.
 */
export default function AuditComponent() {
  const trail = useQuery<AdminAudit[]>({
    queryKey: [QueryKeys.AuditTrail],
    queryFn: async () => await doQueryGet<AdminAudit[]>('/api/Admin/GetAuditTrail?limit=200'),
  })

  return (
    <Stack>
      <Title order={2}>Audit trail</Title>

      <Text c="dimmed" size="sm">
        Every change an admin has made. Newest first, most recent 200.
      </Text>

      {trail.isLoading && <Loader />}

      {trail.isError && (
        <Alert color="red" title="Could not load the audit trail">
          {messageFrom(trail.error)}
        </Alert>
      )}

      {trail.data && trail.data.length === 0 && (
        <Text c="dimmed">Nothing recorded yet.</Text>
      )}

      {trail.data && trail.data.length > 0 && (
        <Table striped highlightOnHover withTableBorder>
          <Table.Thead>
            <Table.Tr>
              <Table.Th w={180}>When</Table.Th>
              <Table.Th w={140}>Who</Table.Th>
              <Table.Th w={140}>Action</Table.Th>
              <Table.Th w={120}>Entity</Table.Th>
              <Table.Th>Detail</Table.Th>
            </Table.Tr>
          </Table.Thead>

          <Table.Tbody>
            {trail.data.map((entry) => (
              <Table.Tr key={entry.id}>
                <Table.Td>
                  <Text size="sm">{new Date(entry.occurredUtc).toLocaleString()}</Text>
                </Table.Td>

                <Table.Td>{entry.adminUsername}</Table.Td>

                <Table.Td>
                  <Badge color={ACTION_COLOURS[entry.action] ?? 'gray'} size="sm">
                    {entry.action}
                  </Badge>
                </Table.Td>

                <Table.Td>
                  <Text size="sm" ff="monospace">
                    {entry.entityType}
                    {entry.entityId !== null ? ` #${entry.entityId}` : ''}
                  </Text>
                </Table.Td>

                <Table.Td>
                  <Text size="sm">{entry.detail ?? '—'}</Text>
                </Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      )}
    </Stack>
  )
}
