'use client'

import {
  Alert,
  Badge,
  Button,
  Group,
  Loader,
  Stack,
  Table,
  Text,
  TextInput,
  Title,
  Tooltip,
} from '@mantine/core'
import { IconRotate } from '@tabler/icons-react'
import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'

import { doQueryGet } from '@/helpers/apiClient'
import { useMutationPost } from '@/helpers/mutations/useMutationPost'
import { messageFrom, notifyError, notifySuccess } from '@/helpers/notificationHelper'
import { QueryKeys } from '@/helpers/QueryKeys'
import type { AdminGameSetting } from '@/interfaces/api/admin/AdminGameSetting'

interface SaveGameSettingRequest {
  id: number
  value: string
}

/**
 * Tuning (Stage 18).
 *
 * The numbers DESIGN.md always promised were retunable without a deploy, finally editable.
 * Each row carries its shipped default and its bounds, because the two questions someone
 * asks before changing a balance number are "what was it" and "how far can I go".
 *
 * Values are edited in place and saved per row rather than as a form: these are independent
 * numbers, and a bulk save would make one typo block five good changes.
 */
export default function SettingsComponent() {
  const [drafts, setDrafts] = useState<Record<number, string>>({})

  const settings = useQuery<AdminGameSetting[]>({
    queryKey: [QueryKeys.GameSettings],
    queryFn: async () => await doQueryGet<AdminGameSetting[]>('/api/Admin/GetGameSettings'),
  })

  const save = useMutationPost<SaveGameSettingRequest, AdminGameSetting>({
    url: '/api/Admin/SaveGameSetting',
    queryKey: [QueryKeys.GameSettings],
    invalidateQuery: true,
    alsoInvalidate: [[QueryKeys.AuditTrail]],
    onSuccess: (saved) => {
      notifySuccess('Saved. The change is live immediately.')

      // Clear the draft so the row falls back to the server's value — otherwise a stale
      // draft would keep showing even after a successful save.
      if (saved !== undefined) {
        setDrafts((prev) => {
          const next = { ...prev }
          delete next[saved.id]
          return next
        })
      }
    },
    // The API names the bound that was exceeded.
    onError: (error) => notifyError(messageFrom(error)),
  })

  const grouped = (settings.data ?? []).reduce<Record<string, AdminGameSetting[]>>(
    (acc, setting) => {
      acc[setting.category] = [...(acc[setting.category] ?? []), setting]
      return acc
    },
    {}
  )

  const changedCount = (settings.data ?? []).filter((s) => s.isChanged).length

  return (
    <Stack>
      <Title order={2}>Tuning</Title>

      <Text c="dimmed" size="sm">
        Balance numbers, editable without a deploy. Changes take effect immediately — no
        restart. Every edit is recorded in the audit trail with its previous value.
      </Text>

      {changedCount > 0 && (
        <Alert color="blue" variant="light">
          {changedCount} setting{changedCount === 1 ? ' differs' : 's differ'} from the shipped
          balance.
        </Alert>
      )}

      {settings.isLoading && <Loader />}

      {settings.isError && (
        <Alert color="red" title="Could not load settings">
          {messageFrom(settings.error)}
        </Alert>
      )}

      {Object.entries(grouped).map(([category, rows]) => (
        <Stack key={category} gap="xs">
          <Title order={4}>{category}</Title>

          <Table striped withTableBorder>
            <Table.Thead>
              <Table.Tr>
                <Table.Th w={260}>Setting</Table.Th>
                <Table.Th w={150}>Value</Table.Th>
                <Table.Th w={110}>Range</Table.Th>
                <Table.Th>What it does</Table.Th>
                <Table.Th w={110}></Table.Th>
              </Table.Tr>
            </Table.Thead>

            <Table.Tbody>
              {rows.map((setting) => {
                const draft = drafts[setting.id] ?? setting.value
                const dirty = draft !== setting.value

                return (
                  <Table.Tr key={setting.id}>
                    <Table.Td>
                      <Group gap="xs">
                        <Text ff="monospace" size="xs">
                          {setting.key}
                        </Text>
                        {setting.isChanged && (
                          <Tooltip label={`Shipped as ${setting.default}`}>
                            <Badge size="xs" color="blue">
                              tuned
                            </Badge>
                          </Tooltip>
                        )}
                      </Group>
                    </Table.Td>

                    <Table.Td>
                      <TextInput
                        size="xs"
                        value={draft}
                        onChange={(e) =>
                          setDrafts((prev) => ({ ...prev, [setting.id]: e.currentTarget.value }))
                        }
                      />
                    </Table.Td>

                    <Table.Td>
                      <Text size="xs" c="dimmed">
                        {setting.minValue} – {setting.maxValue}
                      </Text>
                    </Table.Td>

                    <Table.Td>
                      <Text size="xs">{setting.description}</Text>
                    </Table.Td>

                    <Table.Td>
                      <Group gap={4} wrap="nowrap" justify="flex-end">
                        {/* Only offered when the value has drifted — a reset button on an
                            untouched row is a button that does nothing. */}
                        {setting.isChanged && !dirty && (
                          <Tooltip label={`Reset to ${setting.default}`}>
                            <Button
                              size="compact-xs"
                              variant="subtle"
                              leftSection={<IconRotate size={12} />}
                              onClick={() =>
                                save.mutate({ id: setting.id, value: setting.default })
                              }
                            >
                              Reset
                            </Button>
                          </Tooltip>
                        )}

                        {dirty && (
                          <Button
                            size="compact-xs"
                            loading={save.isPending}
                            onClick={() => save.mutate({ id: setting.id, value: draft })}
                          >
                            Save
                          </Button>
                        )}
                      </Group>
                    </Table.Td>
                  </Table.Tr>
                )
              })}
            </Table.Tbody>
          </Table>
        </Stack>
      ))}
    </Stack>
  )
}
