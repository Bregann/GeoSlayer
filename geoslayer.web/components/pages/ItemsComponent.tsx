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
  IconPhoto,
  IconPencil,
  IconPlus,
  IconSearch,
  IconTrash,
} from '@tabler/icons-react'
import { useQuery } from '@tanstack/react-query'
import { useMemo, useState } from 'react'

import { DeleteConfirmationModal } from '@/components/common/DeleteConfirmationModal'
import { EditItemModal } from '@/components/items/EditItemModal'
import { ItemImageModal } from '@/components/items/ItemImageModal'
import { doQueryGet } from '@/helpers/apiClient'
import { useMutationDelete } from '@/helpers/mutations/useMutationDelete'
import { messageFrom, notifyError, notifySuccess } from '@/helpers/notificationHelper'
import { QueryKeys } from '@/helpers/QueryKeys'
import type { AdminItem } from '@/interfaces/api/admin/AdminItem'
import { ItemKinds, ItemSlots } from '@/interfaces/api/admin/ItemEnums'

/**
 * Item management (Stage 18 task 4).
 *
 * A table rather than cards: this is an admin surface, the useful operation is comparison
 * across rows, and an admin knows what they are looking at.
 */
export default function ItemsComponent() {
  const [search, setSearch] = useState('')
  const [editing, setEditing] = useState<AdminItem | null>(null)
  const [imaging, setImaging] = useState<AdminItem | null>(null)
  const [deleting, setDeleting] = useState<AdminItem | null>(null)

  const [editOpen, { open: openEdit, close: closeEdit }] = useDisclosure(false)
  const [imageOpen, { open: openImage, close: closeImage }] = useDisclosure(false)
  const [deleteOpen, { open: openDelete, close: closeDelete }] = useDisclosure(false)

  const items = useQuery<AdminItem[]>({
    queryKey: [QueryKeys.Items],
    queryFn: async () => await doQueryGet<AdminItem[]>('/api/Admin/GetItems'),
  })

  const remove = useMutationDelete<number>({
    url: (id) => `/api/Admin/DeleteItem?itemId=${id}`,
    queryKey: [QueryKeys.Items],
    onSuccess: () => {
      notifySuccess('Item deleted.')
      closeDelete()
    },
    // The API refuses a delete when a recipe produces the item, and explains why. That
    // sentence is more useful than anything this component could invent.
    onError: (error) => notifyError(messageFrom(error)),
  })

  const filtered = useMemo(() => {
    const list = items.data ?? []
    const needle = search.trim().toLowerCase()

    if (needle === '') return list

    return list.filter(
      (item) =>
        item.name.toLowerCase().includes(needle) ||
        item.key.toLowerCase().includes(needle)
    )
  }, [items.data, search])

  const unread = (items.data ?? []).filter((item) => !item.modifierIsRead)

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={2}>Items</Title>

        <Button
          leftSection={<IconPlus size={16} />}
          onClick={() => {
            setEditing(null)
            openEdit()
          }}
        >
          New item
        </Button>
      </Group>

      {/* §4.3: an equipped item that changes no behaviour is a bug. If one exists, saying
          so at the top is more useful than a quiet badge halfway down a table. */}
      {unread.length > 0 && (
        <Alert color="orange" icon={<IconAlertTriangle size={18} />} title="Items that do nothing">
          {unread.length} item{unread.length === 1 ? '' : 's'} use a modifier nothing reads,
          so equipping {unread.length === 1 ? 'it' : 'them'} changes no behaviour
          (DESIGN.md §4.3): {unread.map((i) => i.key).join(', ')}
        </Alert>
      )}

      <TextInput
        placeholder="Filter by name or key"
        leftSection={<IconSearch size={16} />}
        value={search}
        onChange={(event) => setSearch(event.currentTarget.value)}
      />

      {items.isLoading && <Loader />}

      {items.isError && (
        <Alert color="red" title="Could not load items">
          {messageFrom(items.error)}
        </Alert>
      )}

      {items.data && (
        <Table striped highlightOnHover withTableBorder>
          <Table.Thead>
            <Table.Tr>
              <Table.Th w={60}>Image</Table.Th>
              <Table.Th>Name</Table.Th>
              <Table.Th>Key</Table.Th>
              <Table.Th>Kind</Table.Th>
              <Table.Th>Slot</Table.Th>
              <Table.Th>Tier</Table.Th>
              <Table.Th>Effect</Table.Th>
              <Table.Th w={130}></Table.Th>
            </Table.Tr>
          </Table.Thead>

          <Table.Tbody>
            {filtered.map((item) => (
              <Table.Tr key={item.id}>
                <Table.Td>
                  {item.hasImage ? (
                    <Image
                      src={`/api/Admin/GetItemImage?itemId=${item.id}`}
                      alt={item.name}
                      w={32}
                      h={32}
                      fit="contain"
                    />
                  ) : (
                    <Text c="dimmed" size="xs">
                      —
                    </Text>
                  )}
                </Table.Td>

                <Table.Td>{item.name}</Table.Td>

                <Table.Td>
                  <Text ff="monospace" size="sm">
                    {item.key}
                  </Text>
                </Table.Td>

                <Table.Td>{ItemKinds[item.kind] ?? item.kind}</Table.Td>
                <Table.Td>{ItemSlots[item.slot] ?? item.slot}</Table.Td>
                <Table.Td>{item.tier}</Table.Td>

                <Table.Td>
                  <Group gap="xs">
                    <Text size="sm">{item.modifierText}</Text>
                    {!item.modifierIsRead && (
                      <Tooltip label="Nothing reads this modifier (§4.3)">
                        <Badge color="orange" size="sm">
                          inert
                        </Badge>
                      </Tooltip>
                    )}
                  </Group>
                </Table.Td>

                <Table.Td>
                  <Group gap={4} justify="flex-end" wrap="nowrap">
                    <Tooltip label="Image">
                      <ActionIcon
                        variant="subtle"
                        onClick={() => {
                          setImaging(item)
                          openImage()
                        }}
                      >
                        <IconPhoto size={16} />
                      </ActionIcon>
                    </Tooltip>

                    <Tooltip label="Edit">
                      <ActionIcon
                        variant="subtle"
                        onClick={() => {
                          setEditing(item)
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
                          setDeleting(item)
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

      {filtered.length === 0 && items.data && (
        <Text c="dimmed">No items match that filter.</Text>
      )}

      <EditItemModal opened={editOpen} onClose={closeEdit} item={editing} />

      {imaging && (
        <ItemImageModal opened={imageOpen} onClose={closeImage} item={imaging} />
      )}

      <DeleteConfirmationModal
        opened={deleteOpen}
        onClose={closeDelete}
        title="Delete item"
        body={
          deleting
            ? `Delete "${deleting.name}"? Any player holding it loses it, and this cannot be undone.`
            : ''
        }
        loading={remove.isPending}
        onConfirm={() => deleting && remove.mutate(deleting.id)}
      />
    </Stack>
  )
}
