'use client'

import { Button, Group, Modal, Text } from '@mantine/core'

interface Props {
  opened: boolean
  onClose: () => void
  title: string
  body: string
  loading?: boolean
  onConfirm: () => void
}

/**
 * Confirmation for a destructive action.
 *
 * The confirm button is red and says what it does rather than "OK" — the one place where
 * a misread costs player data.
 */
export function DeleteConfirmationModal({
  opened,
  onClose,
  title,
  body,
  loading,
  onConfirm,
}: Props) {
  return (
    <Modal opened={opened} onClose={onClose} title={title} centered>
      <Text size="sm">{body}</Text>

      <Group justify="flex-end" mt="md">
        <Button variant="default" onClick={onClose} disabled={loading}>
          Cancel
        </Button>
        <Button color="red" onClick={onConfirm} loading={loading}>
          Delete
        </Button>
      </Group>
    </Modal>
  )
}
