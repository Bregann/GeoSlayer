'use client'

import {
  Alert,
  Badge,
  Button,
  Card,
  Group,
  Loader,
  SimpleGrid,
  Stack,
  Table,
  Text,
  TextInput,
  Title,
} from '@mantine/core'
import { useDebouncedValue, useDisclosure } from '@mantine/hooks'
import { IconPencil, IconSearch } from '@tabler/icons-react'
import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'

import { AdjustPlayerModal } from '@/components/players/AdjustPlayerModal'
import { doQueryGet } from '@/helpers/apiClient'
import { messageFrom } from '@/helpers/notificationHelper'
import { QueryKeys } from '@/helpers/QueryKeys'
import type { AdminPlayer, AdminPlayerSummary } from '@/interfaces/api/admin/AdminPlayer'

/** One labelled number in the summary grid. */
function Stat({ label, value }: { label: string; value: string | number }) {
  return (
    <Card withBorder padding="xs">
      <Text size="xs" c="dimmed">
        {label}
      </Text>
      <Text size="lg" fw={600}>
        {typeof value === 'number' ? value.toLocaleString() : value}
      </Text>
    </Card>
  )
}

/**
 * Player administration (Stage 18 task 9).
 *
 * **The only screen in this interface that edits player state.** Everywhere else a mistake
 * is a retunable config value; here it is in someone's balance. Adjustments are uncapped —
 * an admin is trusted, and the audit trail is the control rather than a limit — but the
 * reason field is required, because an entry reading "coin +5000" and nothing else cannot
 * answer the question it exists for.
 *
 * The view came first and earns its place on its own: what level are they, what do they
 * hold, when did they last sync. Most support questions end there without anyone changing
 * anything.
 */
export default function PlayersComponent() {
  const [query, setQuery] = useState('')
  const [selected, setSelected] = useState<number | null>(null)
  const [adjustOpen, { open: openAdjust, close: closeAdjust }] = useDisclosure(false)

  // Debounced: the search runs on every keystroke otherwise, and it joins two tables.
  const [debounced] = useDebouncedValue(query, 300)

  const results = useQuery<AdminPlayerSummary[]>({
    queryKey: [QueryKeys.PlayerSearch, debounced],
    queryFn: async () =>
      await doQueryGet<AdminPlayerSummary[]>(
        `/api/Admin/SearchPlayers?query=${encodeURIComponent(debounced)}`
      ),
    // The server returns nothing under two characters, so there is no point asking.
    enabled: debounced.trim().length >= 2,
  })

  const player = useQuery<AdminPlayer>({
    queryKey: [QueryKeys.Player, selected],
    queryFn: async () =>
      await doQueryGet<AdminPlayer>(`/api/Admin/GetPlayer?playerId=${selected}`),
    enabled: selected !== null,
  })

  return (
    <Stack>
      <Title order={2}>Players</Title>

      <Text c="dimmed" size="sm">
        The only screen here that edits player state. Adjustments are uncapped — an admin is
        trusted — but every one is recorded with who, what, when and why.
      </Text>

      <TextInput
        placeholder="Search by username or email (at least 2 characters)"
        leftSection={<IconSearch size={16} />}
        value={query}
        onChange={(event) => setQuery(event.currentTarget.value)}
      />

      {results.isLoading && <Loader size="sm" />}

      {results.isError && (
        <Alert color="red" title="Search failed">
          {messageFrom(results.error)}
        </Alert>
      )}

      {results.data && results.data.length === 0 && debounced.trim().length >= 2 && (
        <Text c="dimmed">No players match that.</Text>
      )}

      {results.data && results.data.length > 0 && (
        <Table striped highlightOnHover withTableBorder>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Username</Table.Th>
              <Table.Th w={110}>Level</Table.Th>
              <Table.Th w={130}>Coin</Table.Th>
              <Table.Th w={190}>Last sync</Table.Th>
            </Table.Tr>
          </Table.Thead>

          <Table.Tbody>
            {results.data.map((row) => (
              <Table.Tr
                key={row.playerId}
                onClick={() => setSelected(row.playerId)}
                style={{ cursor: 'pointer' }}
              >
                <Table.Td>{row.username}</Table.Td>
                <Table.Td>{row.adventurerLevel}</Table.Td>
                <Table.Td>{row.coin.toLocaleString()}c</Table.Td>
                <Table.Td>
                  {row.lastSyncAtUtc === null
                    ? '—'
                    : new Date(row.lastSyncAtUtc).toLocaleString()}
                </Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      )}

      {player.isLoading && selected !== null && <Loader />}

      {player.data && (
        <Stack mt="md">
          <Group justify="space-between">
            <Group>
              <Title order={3}>{player.data.username}</Title>
              {player.data.isAdmin && <Badge color="grape">admin</Badge>}
              <Text c="dimmed" size="sm">
                {player.data.email}
              </Text>
            </Group>

            <Button
              variant="light"
              leftSection={<IconPencil size={16} />}
              onClick={openAdjust}
            >
              Adjust
            </Button>
          </Group>

          <SimpleGrid cols={{ base: 2, sm: 4 }}>
            <Stat label="Adventurer level" value={player.data.adventurerLevel} />
            <Stat label="Adventurer XP" value={player.data.adventurerXp} />
            <Stat label="Coin in hand" value={player.data.coin} />
            <Stat label="Coin deposited" value={player.data.coinDeposited} />
            <Stat
              label="Bonus points"
              value={`${player.data.bonusPointsEarned - player.data.bonusPointsSpent} of ${player.data.bonusPointsEarned}`}
            />
            <Stat label="Curation" value={player.data.curation} />
            <Stat label="Cells revealed" value={player.data.cellsRevealed} />
            <Stat label="Museum finds" value={player.data.museumEntriesFound} />
            <Stat label="Claims" value={player.data.claimCount} />
            <Stat label="Workers" value={player.data.workerCount} />
          </SimpleGrid>

          <Title order={4}>Skills</Title>

          <Table striped withTableBorder>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Skill</Table.Th>
                <Table.Th w={110}>Level</Table.Th>
                <Table.Th w={160}>XP</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {player.data.skills.map((skill) => (
                <Table.Tr key={skill.skillType}>
                  <Table.Td>{skill.name}</Table.Td>
                  <Table.Td>{skill.level}</Table.Td>
                  <Table.Td>{skill.xp.toLocaleString()}</Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>

          <Title order={4}>Equipment</Title>

          {player.data.items.length === 0 ? (
            <Text c="dimmed" size="sm">
              Nothing owned.
            </Text>
          ) : (
            <Table striped withTableBorder>
              <Table.Tbody>
                {player.data.items.map((item) => (
                  <Table.Tr key={item.id}>
                    <Table.Td>{item.name}</Table.Td>
                    <Table.Td w={110}>{item.quantity}</Table.Td>
                    <Table.Td w={110}>
                      {item.isEquipped && (
                        <Badge size="xs" color="teal">
                          equipped
                        </Badge>
                      )}
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          )}

          <Title order={4}>Inventory</Title>

          {player.data.materials.length === 0 ? (
            <Text c="dimmed" size="sm">
              Nothing held.
            </Text>
          ) : (
            <Table striped withTableBorder>
              <Table.Tbody>
                {player.data.materials.map((material) => (
                  <Table.Tr key={material.id}>
                    <Table.Td>{material.name}</Table.Td>
                    <Table.Td w={140}>{material.quantity.toLocaleString()}</Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          )}
        </Stack>
      )}

      {player.data && (
        <AdjustPlayerModal
          opened={adjustOpen}
          onClose={closeAdjust}
          player={player.data}
        />
      )}
    </Stack>
  )
}
