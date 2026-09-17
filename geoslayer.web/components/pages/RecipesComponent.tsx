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
import {
  IconAlertTriangle,
  IconPencil,
  IconPlus,
  IconSearch,
  IconTrash,
} from '@tabler/icons-react'
import { useQuery } from '@tanstack/react-query'
import { useMemo, useState } from 'react'

import { DeleteConfirmationModal } from '@/components/common/DeleteConfirmationModal'
import { EditRecipeModal } from '@/components/recipes/EditRecipeModal'
import { doQueryGet } from '@/helpers/apiClient'
import { useMutationDelete } from '@/helpers/mutations/useMutationDelete'
import { messageFrom, notifyError, notifySuccess } from '@/helpers/notificationHelper'
import { QueryKeys } from '@/helpers/QueryKeys'
import type { AdminRecipe } from '@/interfaces/api/admin/AdminRecipe'
import { SkillTypes } from '@/interfaces/api/admin/ItemEnums'

/** "10m" / "2h 30m" — a duration read at a glance rather than counted in seconds. */
function durationLabel(seconds: number): string {
  if (seconds < 60) return `${Math.round(seconds)}s`

  const minutes = Math.round(seconds / 60)
  if (minutes < 60) return `${minutes}m`

  const hours = Math.floor(minutes / 60)
  const rest = minutes % 60

  return rest === 0 ? `${hours}h` : `${hours}h ${rest}m`
}

/**
 * Recipe management (Stage 18 task 7).
 *
 * Shows the chain — skill, level, what goes in, what comes out — because a recipe is only
 * meaningful in relation to the ladders on either side of it.
 *
 * Warnings are surfaced in the table rather than only at save time. A loss-making recipe
 * that was saved deliberately mid-tune should stay visible, not disappear until someone
 * next opens it.
 */
export default function RecipesComponent() {
  const [search, setSearch] = useState('')
  const [editing, setEditing] = useState<AdminRecipe | null>(null)
  const [deleting, setDeleting] = useState<AdminRecipe | null>(null)

  const [editOpen, { open: openEdit, close: closeEdit }] = useDisclosure(false)
  const [deleteOpen, { open: openDelete, close: closeDelete }] = useDisclosure(false)

  const recipes = useQuery<AdminRecipe[]>({
    queryKey: [QueryKeys.Recipes],
    queryFn: async () => await doQueryGet<AdminRecipe[]>('/api/Admin/GetRecipes'),
  })

  const remove = useMutationDelete<number>({
    url: (id) => `/api/Admin/DeleteRecipe?recipeId=${id}`,
    queryKey: [QueryKeys.Recipes],
    alsoInvalidate: [[QueryKeys.AuditTrail]],
    onSuccess: () => {
      notifySuccess('Recipe deleted.')
      closeDelete()
    },
    // The API refuses while crafts are still queued, and says how many.
    onError: (error) => notifyError(messageFrom(error)),
  })

  const filtered = useMemo(() => {
    const list = recipes.data ?? []
    const needle = search.trim().toLowerCase()

    if (needle === '') return list

    return list.filter(
      (r) =>
        r.name.toLowerCase().includes(needle) ||
        r.key.toLowerCase().includes(needle) ||
        (r.outputName ?? '').toLowerCase().includes(needle)
    )
  }, [recipes.data, search])

  const flagged = (recipes.data ?? []).filter((r) => r.warnings.length > 0)

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={2}>Recipes</Title>

        <Button
          leftSection={<IconPlus size={16} />}
          onClick={() => {
            setEditing(null)
            openEdit()
          }}
        >
          New recipe
        </Button>
      </Group>

      {/* Saved deliberately or not, a flagged recipe should stay visible rather than
          disappear until someone next opens it. */}
      {flagged.length > 0 && (
        <Alert color="yellow" icon={<IconAlertTriangle size={18} />} title="Recipes worth a look">
          {flagged.length} recipe{flagged.length === 1 ? ' has' : 's have'} a warning —
          usually that the output is worth less than the inputs (§5D.1).
        </Alert>
      )}

      <TextInput
        placeholder="Filter by name, key or output"
        leftSection={<IconSearch size={16} />}
        value={search}
        onChange={(event) => setSearch(event.currentTarget.value)}
      />

      {recipes.isLoading && <Loader />}

      {recipes.isError && (
        <Alert color="red" title="Could not load recipes">
          {messageFrom(recipes.error)}
        </Alert>
      )}

      {recipes.data && (
        <Table striped highlightOnHover withTableBorder>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Name</Table.Th>
              <Table.Th>Skill</Table.Th>
              <Table.Th>Level</Table.Th>
              <Table.Th>Time</Table.Th>
              <Table.Th>XP</Table.Th>
              <Table.Th>Consumes</Table.Th>
              <Table.Th>Produces</Table.Th>
              <Table.Th>Economy</Table.Th>
              <Table.Th w={90}></Table.Th>
            </Table.Tr>
          </Table.Thead>

          <Table.Tbody>
            {filtered.map((recipe) => (
              <Table.Tr key={recipe.id}>
                <Table.Td>
                  <Group gap="xs">
                    <Text size="sm">{recipe.name}</Text>
                    {recipe.warnings.length > 0 && (
                      <Tooltip label={recipe.warnings.join(' ')} multiline w={320}>
                        <Badge color="yellow" size="sm">
                          check
                        </Badge>
                      </Tooltip>
                    )}
                  </Group>
                  <Text ff="monospace" size="xs" c="dimmed">
                    {recipe.key}
                  </Text>
                </Table.Td>

                <Table.Td>{SkillTypes[recipe.skillType] ?? recipe.skillType}</Table.Td>
                <Table.Td>{recipe.levelRequired}</Table.Td>
                <Table.Td>{durationLabel(recipe.durationSeconds)}</Table.Td>
                <Table.Td>{recipe.xpReward}</Table.Td>

                <Table.Td>
                  <Stack gap={2}>
                    {recipe.inputs.map((input) => (
                      <Text key={input.materialId} size="xs">
                        {input.quantity} × {input.materialName}
                      </Text>
                    ))}
                  </Stack>
                </Table.Td>

                <Table.Td>
                  <Text size="sm">
                    {recipe.outputQuantity} × {recipe.outputName ?? '—'}
                  </Text>
                </Table.Td>

                <Table.Td>
                  {/* Zero output value means an item, whose worth is its modifier rather
                      than a price — saying "0c" there would read as broken. */}
                  <Text size="xs" c="dimmed">
                    {recipe.inputCost}c in
                    {recipe.outputValue > 0 ? ` · ${recipe.outputValue}c out` : ' · item'}
                  </Text>
                </Table.Td>

                <Table.Td>
                  <Group gap={4} justify="flex-end" wrap="nowrap">
                    <Tooltip label="Edit">
                      <ActionIcon
                        variant="subtle"
                        onClick={() => {
                          setEditing(recipe)
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
                          setDeleting(recipe)
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

      {filtered.length === 0 && recipes.data && (
        <Text c="dimmed">No recipes match that filter.</Text>
      )}

      <EditRecipeModal opened={editOpen} onClose={closeEdit} recipe={editing} />

      <DeleteConfirmationModal
        opened={deleteOpen}
        onClose={closeDelete}
        title="Delete recipe"
        body={
          deleting
            ? `Delete "${deleting.name}"? Players will no longer be able to craft it.`
            : ''
        }
        loading={remove.isPending}
        onConfirm={() => deleting && remove.mutate(deleting.id)}
      />
    </Stack>
  )
}
