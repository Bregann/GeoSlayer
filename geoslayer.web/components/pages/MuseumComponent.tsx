'use client'

import {
  ActionIcon,
  Alert,
  Badge,
  Button,
  Group,
  Image,
  Loader,
  Stack,
  Table,
  Text,
  TextInput,
  Title,
  Tooltip,
} from '@mantine/core'
import { useDisclosure } from '@mantine/hooks'
import {
  IconAlertTriangle,
  IconPencil,
  IconPhoto,
  IconPlus,
  IconSearch,
  IconTrash,
} from '@tabler/icons-react'
import { useQuery } from '@tanstack/react-query'
import { useMemo, useState } from 'react'

import { DeleteConfirmationModal } from '@/components/common/DeleteConfirmationModal'
import { SpriteModal } from '@/components/common/SpriteModal'
import { EditMuseumEntryModal } from '@/components/museum/EditMuseumEntryModal'
import { doQueryGet } from '@/helpers/apiClient'
import { useMutationDelete } from '@/helpers/mutations/useMutationDelete'
import { messageFrom, notifyError, notifySuccess } from '@/helpers/notificationHelper'
import { QueryKeys } from '@/helpers/QueryKeys'
import type { AdminMuseumEntry } from '@/interfaces/api/admin/AdminMuseumEntry'
import { MuseumRarities, MuseumWings, spriteOwner } from '@/interfaces/api/admin/ItemEnums'

const RARITY_COLOURS = ['gray', 'teal', 'blue', 'grape']

/**
 * Museum entries (Stage 18 task 8).
 *
 * The useful column here is **found by**. A Museum entry nobody has ever found is either
 * too rare or unreachable, and there is no way to tell which from the definition alone —
 * §5A.1 makes the empty plinth a pull rather than noise, but a plinth that is empty for
 * *everyone* is just a broken promise.
 */
export default function MuseumComponent() {
  const [search, setSearch] = useState('')
  const [editing, setEditing] = useState<AdminMuseumEntry | null>(null)
  const [deleting, setDeleting] = useState<AdminMuseumEntry | null>(null)
  const [spriting, setSpriting] = useState<AdminMuseumEntry | null>(null)

  const [editOpen, { open: openEdit, close: closeEdit }] = useDisclosure(false)
  const [deleteOpen, { open: openDelete, close: closeDelete }] = useDisclosure(false)
  const [spriteOpen, { open: openSprite, close: closeSprite }] = useDisclosure(false)

  const remove = useMutationDelete<number>({
    url: (id) => `/api/Admin/DeleteMuseumEntry?entryId=${id}`,
    queryKey: [QueryKeys.MuseumEntries],
    alsoInvalidate: [[QueryKeys.AuditTrail]],
    onSuccess: () => {
      notifySuccess('Entry deleted.')
      closeDelete()
    },
    // The API refuses when players have found it, and says how many.
    onError: (error) => notifyError(messageFrom(error)),
  })

  const entries = useQuery<AdminMuseumEntry[]>({
    queryKey: [QueryKeys.MuseumEntries],
    queryFn: async () => await doQueryGet<AdminMuseumEntry[]>('/api/Admin/GetMuseumEntries'),
  })

  const filtered = useMemo(() => {
    const list = entries.data ?? []
    const needle = search.trim().toLowerCase()

    if (needle === '') return list

    return list.filter(
      (e) =>
        e.name.toLowerCase().includes(needle) ||
        e.key.toLowerCase().includes(needle) ||
        (MuseumWings[e.wing] ?? '').toLowerCase().includes(needle)
    )
  }, [entries.data, search])

  const warnings = entries.data?.[0]?.setWarnings ?? []
  const neverFound = (entries.data ?? []).filter((e) => e.foundBy === 0).length

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={2}>Museum</Title>

        <Button
          leftSection={<IconPlus size={16} />}
          onClick={() => {
            setEditing(null)
            openEdit()
          }}
        >
          New entry
        </Button>
      </Group>

      <Text c="dimmed" size="sm">
        Entries, wings and what players have actually found. An entry nobody has ever found
        is either too rare or unreachable (§5A.1).
      </Text>

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

      {neverFound > 0 && entries.data && (
        <Alert color="blue" variant="light">
          {neverFound} of {entries.data.length} entries have never been found by anyone.
          Expected for a young database; worth checking once there are real players.
        </Alert>
      )}

      <TextInput
        placeholder="Filter by name, key or wing"
        leftSection={<IconSearch size={16} />}
        value={search}
        onChange={(event) => setSearch(event.currentTarget.value)}
      />

      {entries.isLoading && <Loader />}

      {entries.isError && (
        <Alert color="red" title="Could not load Museum entries">
          {messageFrom(entries.error)}
        </Alert>
      )}

      {entries.data && (
        <Table striped highlightOnHover withTableBorder>
          <Table.Thead>
            <Table.Tr>
              <Table.Th w={60}>Art</Table.Th>
              <Table.Th>Name</Table.Th>
              <Table.Th>Key</Table.Th>
              <Table.Th w={130}>Wing</Table.Th>
              <Table.Th w={120}>Rarity</Table.Th>
              <Table.Th>How it is found</Table.Th>
              <Table.Th w={100}>Found by</Table.Th>
              <Table.Th w={130}></Table.Th>
            </Table.Tr>
          </Table.Thead>

          <Table.Tbody>
            {filtered.map((entry) => (
              <Table.Tr key={entry.id}>
                <Table.Td>
                  {entry.hasSprite ? (
                    <Image
                      src={`/api/Admin/GetSprite?ownerType=${spriteOwner('MuseumEntry')}&ownerId=${entry.id}`}
                      alt={entry.name}
                      w={28}
                      h={28}
                      fit="contain"
                    />
                  ) : (
                    <Text c="dimmed" size="xs">
                      —
                    </Text>
                  )}
                </Table.Td>

                <Table.Td>{entry.name}</Table.Td>

                <Table.Td>
                  <Text ff="monospace" size="xs">
                    {entry.key}
                  </Text>
                </Table.Td>

                <Table.Td>{MuseumWings[entry.wing] ?? entry.wing}</Table.Td>

                <Table.Td>
                  <Badge size="sm" variant="light" color={RARITY_COLOURS[entry.rarity] ?? 'gray'}>
                    {MuseumRarities[entry.rarity] ?? entry.rarity}
                  </Badge>
                </Table.Td>

                <Table.Td>
                  <Text size="xs">{entry.unlockCondition}</Text>
                </Table.Td>

                <Table.Td>
                  {entry.foundBy === 0 ? (
                    <Tooltip label="Nobody has found this yet">
                      <Text size="sm" c="dimmed">
                        —
                      </Text>
                    </Tooltip>
                  ) : (
                    <Text size="sm">{entry.foundBy}</Text>
                  )}
                </Table.Td>

                <Table.Td>
                  <Group gap={4} justify="flex-end" wrap="nowrap">
                    <Tooltip label="Sprite">
                      <ActionIcon
                        variant="subtle"
                        onClick={() => {
                          setSpriting(entry)
                          openSprite()
                        }}
                      >
                        <IconPhoto size={16} />
                      </ActionIcon>
                    </Tooltip>

                    <Tooltip label="Edit">
                      <ActionIcon
                        variant="subtle"
                        onClick={() => {
                          setEditing(entry)
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
                          setDeleting(entry)
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

      {filtered.length === 0 && entries.data && (
        <Text c="dimmed">No entries match that filter.</Text>
      )}

      <EditMuseumEntryModal opened={editOpen} onClose={closeEdit} entry={editing} />

      {spriting && (
        <SpriteModal
          opened={spriteOpen}
          onClose={closeSprite}
          ownerType={spriteOwner('MuseumEntry')}
          ownerId={spriting.id}
          ownerName={spriting.name}
          hasSprite={spriting.hasSprite}
          invalidate={[[QueryKeys.MuseumEntries], [QueryKeys.AuditTrail]]}
        />
      )}

      <DeleteConfirmationModal
        opened={deleteOpen}
        onClose={closeDelete}
        title="Delete Museum entry"
        body={
          deleting
            ? `Delete "${deleting.name}"? This is refused if any player has already found it — it would take it off their shelf.`
            : ''
        }
        loading={remove.isPending}
        onConfirm={() => deleting && remove.mutate(deleting.id)}
      />
    </Stack>
  )
}
