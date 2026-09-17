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
  TextInput,
  Title,
  Tooltip,
} from '@mantine/core'
import { useDisclosure } from '@mantine/hooks'
import { IconPencil, IconPlus, IconSearch, IconTrash } from '@tabler/icons-react'
import { useQuery } from '@tanstack/react-query'
import { useMemo, useState } from 'react'

import { DeleteConfirmationModal } from '@/components/common/DeleteConfirmationModal'
import { EditMaterialModal } from '@/components/materials/EditMaterialModal'
import { doQueryGet } from '@/helpers/apiClient'
import { useMutationDelete } from '@/helpers/mutations/useMutationDelete'
import { messageFrom, notifyError, notifySuccess } from '@/helpers/notificationHelper'
import { QueryKeys } from '@/helpers/QueryKeys'
import type { AdminMaterial } from '@/interfaces/api/admin/AdminMaterial'
import { MaterialCategories, SkillTypes } from '@/interfaces/api/admin/ItemEnums'

/**
 * Material management (Stage 18 task 6).
 *
 * Grouped by category and tier, because that is the shape of the thing being tuned — a
 * ladder. Sorting by name would hide the rung ordering that every invariant is about.
 *
 * The server refuses anything that breaks a ladder rule and says which one; this table's
 * job is to make the ladder visible enough that those refusals are rarely a surprise.
 */
export default function MaterialsComponent() {
  const [search, setSearch] = useState('')
  const [editing, setEditing] = useState<AdminMaterial | null>(null)
  const [deleting, setDeleting] = useState<AdminMaterial | null>(null)

  const [editOpen, { open: openEdit, close: closeEdit }] = useDisclosure(false)
  const [deleteOpen, { open: openDelete, close: closeDelete }] = useDisclosure(false)

  const materials = useQuery<AdminMaterial[]>({
    queryKey: [QueryKeys.Materials],
    queryFn: async () => await doQueryGet<AdminMaterial[]>('/api/Admin/GetMaterials'),
  })

  const remove = useMutationDelete<number>({
    url: (id) => `/api/Admin/DeleteMaterial?materialId=${id}`,
    queryKey: [QueryKeys.Materials],
    alsoInvalidate: [[QueryKeys.AuditTrail]],
    onSuccess: () => {
      notifySuccess('Material deleted.')
      closeDelete()
    },
    // The API names what still references it — a recipe, a drop table — which is what an
    // admin needs in order to act.
    onError: (error) => notifyError(messageFrom(error)),
  })

  const filtered = useMemo(() => {
    const list = materials.data ?? []
    const needle = search.trim().toLowerCase()

    if (needle === '') return list

    return list.filter(
      (m) =>
        m.name.toLowerCase().includes(needle) ||
        m.key.toLowerCase().includes(needle) ||
        (MaterialCategories[m.category] ?? '').toLowerCase().includes(needle)
    )
  }, [materials.data, search])

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={2}>Materials</Title>

        <Button
          leftSection={<IconPlus size={16} />}
          onClick={() => {
            setEditing(null)
            openEdit()
          }}
        >
          New material
        </Button>
      </Group>

      <Text c="dimmed" size="sm">
        Ordered by category then tier — the shape of the ladder. Sell price is derived from
        tier and category (§5D.1) rather than set here.
      </Text>

      <TextInput
        placeholder="Filter by name, key or category"
        leftSection={<IconSearch size={16} />}
        value={search}
        onChange={(event) => setSearch(event.currentTarget.value)}
      />

      {materials.isLoading && <Loader />}

      {materials.isError && (
        <Alert color="red" title="Could not load materials">
          {messageFrom(materials.error)}
        </Alert>
      )}

      {materials.data && (
        <Table striped highlightOnHover withTableBorder>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Name</Table.Th>
              <Table.Th>Key</Table.Th>
              <Table.Th>Category</Table.Th>
              <Table.Th>Skill</Table.Th>
              <Table.Th>Tier</Table.Th>
              <Table.Th>Level</Table.Th>
              <Table.Th>Gather</Table.Th>
              <Table.Th>XP/unit</Table.Th>
              <Table.Th>XP/sec</Table.Th>
              <Table.Th>Price</Table.Th>
              <Table.Th w={90}></Table.Th>
            </Table.Tr>
          </Table.Thead>

          <Table.Tbody>
            {filtered.map((material) => (
              <Table.Tr key={material.id}>
                <Table.Td>
                  <Group gap="xs">
                    <Text size="sm">{material.name}</Text>
                    {material.isUnique && (
                      <Tooltip label="From the rarest POIs only (§7.4)">
                        <Badge size="xs" color="grape">
                          unique
                        </Badge>
                      </Tooltip>
                    )}
                  </Group>
                </Table.Td>

                <Table.Td>
                  <Text ff="monospace" size="sm">
                    {material.key}
                  </Text>
                </Table.Td>

                <Table.Td>{MaterialCategories[material.category] ?? material.category}</Table.Td>

                <Table.Td>
                  {material.skillType === null ? (
                    <Text c="dimmed" size="sm">
                      —
                    </Text>
                  ) : (
                    (SkillTypes[material.skillType] ?? material.skillType)
                  )}
                </Table.Td>

                <Table.Td>{material.tier}</Table.Td>
                <Table.Td>{material.levelRequired}</Table.Td>
                <Table.Td>{material.baseGatherSeconds}s</Table.Td>
                <Table.Td>{material.xpPerUnit}</Table.Td>

                <Table.Td>
                  {/* The number §4.1a constrains: it must never fall as tiers rise. */}
                  <Text size="sm" c="dimmed">
                    {material.xpPerSecond}
                  </Text>
                </Table.Td>

                <Table.Td>{material.unitPrice}c</Table.Td>

                <Table.Td>
                  <Group gap={4} justify="flex-end" wrap="nowrap">
                    <Tooltip label="Edit">
                      <ActionIcon
                        variant="subtle"
                        onClick={() => {
                          setEditing(material)
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
                          setDeleting(material)
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

      {filtered.length === 0 && materials.data && (
        <Text c="dimmed">No materials match that filter.</Text>
      )}

      <EditMaterialModal opened={editOpen} onClose={closeEdit} material={editing} />

      <DeleteConfirmationModal
        opened={deleteOpen}
        onClose={closeDelete}
        title="Delete material"
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
