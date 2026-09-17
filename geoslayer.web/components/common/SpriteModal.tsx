'use client'

import { Button, Group, Image, Modal, Stack, Text } from '@mantine/core'
import { Dropzone } from '@mantine/dropzone'
import { IconPhotoUp, IconUpload, IconX } from '@tabler/icons-react'
import { useState } from 'react'

import { useMutationDelete } from '@/helpers/mutations/useMutationDelete'
import { useMutationUpload } from '@/helpers/mutations/useMutationUpload'
import { messageFrom, notifyError, notifySuccess } from '@/helpers/notificationHelper'
import { SpriteOwners } from '@/interfaces/api/admin/ItemEnums'

interface Props {
  opened: boolean
  onClose: () => void
  /** Index into SpriteOwners — the wire value the API expects. */
  ownerType: number
  ownerId: number
  /** Shown in the title so it is obvious what is being given art. */
  ownerName: string
  hasSprite: boolean
  /** Query keys to refresh after a change, so the list's badge updates. */
  invalidate: string[][]
}

/** Kept in step with ImageValidation.MaxSizeBytes on the server. */
const MAX_SIZE_BYTES = 2 * 1024 * 1024

/** Matches the server's allow-list — SVG is excluded: it is a script host, not an icon. */
const ACCEPTED = ['image/png', 'image/jpeg', 'image/webp']

/**
 * Sprite upload for anything the game draws an icon for (Stage 18 task 6).
 *
 * One modal rather than one per entity type, matching the single generalised endpoint.
 * Three near-identical upload dialogs would be three places to fix the same bug.
 *
 * The client's checks are a courtesy, not the guard — the server validates magic bytes and
 * refuses anything whose contents disagree with its declared type.
 */
export function SpriteModal({
  opened,
  onClose,
  ownerType,
  ownerId,
  ownerName,
  hasSprite,
  invalidate,
}: Props) {
  // Bumped after each change to bust the browser cache: the URL is stable per owner and the
  // response is cached for a day, so a replaced sprite would otherwise keep showing the old.
  const [version, setVersion] = useState(0)

  const src = `/api/Admin/GetSprite?ownerType=${ownerType}&ownerId=${ownerId}&v=${version}`

  const [primaryKey, ...otherKeys] = invalidate

  const upload = useMutationUpload({
    url: '/api/Admin/UploadSprite',
    queryKey: primaryKey,
    alsoInvalidate: otherKeys,
    onSuccess: () => {
      notifySuccess('Sprite uploaded.')
      setVersion((v) => v + 1)
    },
    onError: (error) => notifyError(messageFrom(error)),
  })

  const remove = useMutationDelete<void>({
    url: () => `/api/Admin/DeleteSprite?ownerType=${ownerType}&ownerId=${ownerId}`,
    queryKey: primaryKey,
    alsoInvalidate: otherKeys,
    onSuccess: () => {
      notifySuccess('Sprite removed.')
      setVersion((v) => v + 1)
    },
    onError: (error) => notifyError(messageFrom(error)),
  })

  const send = (files: File[]) => {
    const file = files[0]
    if (file === undefined) return

    const data = new FormData()
    data.append('ownerType', SpriteOwners[ownerType] ?? String(ownerType))
    data.append('ownerId', String(ownerId))
    data.append('file', file)

    upload.mutate(data)
  }

  return (
    <Modal opened={opened} onClose={onClose} title={`Sprite · ${ownerName}`} centered>
      <Stack>
        {hasSprite ? (
          <Group justify="center">
            <Image src={src} alt={ownerName} w={128} h={128} fit="contain" />
          </Group>
        ) : (
          <Text c="dimmed" size="sm" ta="center">
            No sprite yet. The app falls back to an emoji until one is uploaded.
          </Text>
        )}

        <Dropzone
          onDrop={send}
          onReject={() =>
            notifyError(`PNG, JPEG or WebP only, up to ${MAX_SIZE_BYTES / 1024 / 1024}MB.`)
          }
          maxSize={MAX_SIZE_BYTES}
          accept={ACCEPTED}
          multiple={false}
          loading={upload.isPending}
        >
          <Group justify="center" gap="sm" mih={100} style={{ pointerEvents: 'none' }}>
            <Dropzone.Accept>
              <IconUpload size={32} />
            </Dropzone.Accept>
            <Dropzone.Reject>
              <IconX size={32} />
            </Dropzone.Reject>
            <Dropzone.Idle>
              <IconPhotoUp size={32} />
            </Dropzone.Idle>

            <div>
              <Text size="sm">Drop an image, or click to choose one</Text>
              <Text size="xs" c="dimmed">
                PNG, JPEG or WebP. Replaces the current sprite.
              </Text>
            </div>
          </Group>
        </Dropzone>

        <Group justify="space-between">
          <Button
            color="red"
            variant="subtle"
            disabled={!hasSprite || remove.isPending}
            loading={remove.isPending}
            onClick={() => remove.mutate()}
          >
            Remove sprite
          </Button>

          <Button variant="default" onClick={onClose}>
            Close
          </Button>
        </Group>
      </Stack>
    </Modal>
  )
}
