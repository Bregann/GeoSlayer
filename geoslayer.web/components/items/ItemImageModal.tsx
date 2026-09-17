'use client'

import { Button, Group, Image, Modal, Stack, Text } from '@mantine/core'
import { Dropzone } from '@mantine/dropzone'
import { IconPhotoUp, IconUpload, IconX } from '@tabler/icons-react'
import { useState } from 'react'

import { useMutationDelete } from '@/helpers/mutations/useMutationDelete'
import { useMutationUpload } from '@/helpers/mutations/useMutationUpload'
import { messageFrom, notifyError, notifySuccess } from '@/helpers/notificationHelper'
import { QueryKeys } from '@/helpers/QueryKeys'
import type { AdminItem } from '@/interfaces/api/admin/AdminItem'

interface Props {
  opened: boolean
  onClose: () => void
  item: AdminItem
}

/** Kept in step with ImageValidation.MaxSizeBytes on the server. */
const MAX_SIZE_BYTES = 2 * 1024 * 1024

/** Matches the server's allow-list — SVG is excluded: it is a script host, not an icon. */
const ACCEPTED = ['image/png', 'image/jpeg', 'image/webp']

/**
 * Item image upload (Stage 18 task 3).
 *
 * The client's checks are a courtesy, not the guard — the server validates magic bytes and
 * refuses anything whose contents disagree with its declared type. Rejecting early here
 * just saves an admin a round trip.
 */
export function ItemImageModal({ opened, onClose, item }: Props) {
  // Bumped after each change to bust the browser cache: the URL is stable per item, and
  // the response is deliberately cached for a day, so a replaced image would otherwise
  // keep showing the old one.
  const [version, setVersion] = useState(0)

  const src = `/api/Admin/GetItemImage?itemId=${item.id}&v=${version}`

  const upload = useMutationUpload({
    url: `/api/Admin/UploadItemImage`,
    queryKey: [QueryKeys.Items],
    alsoInvalidate: [[QueryKeys.AuditTrail]],
    onSuccess: () => {
      notifySuccess('Image uploaded.')
      setVersion((v) => v + 1)
    },
    onError: (error) => notifyError(messageFrom(error)),
  })

  const remove = useMutationDelete<number>({
    url: (id) => `/api/Admin/DeleteItemImage?itemId=${id}`,
    queryKey: [QueryKeys.Items],
    alsoInvalidate: [[QueryKeys.AuditTrail]],
    onSuccess: () => {
      notifySuccess('Image removed.')
      setVersion((v) => v + 1)
    },
    onError: (error) => notifyError(messageFrom(error)),
  })

  const send = (files: File[]) => {
    const file = files[0]
    if (file === undefined) return

    const data = new FormData()
    data.append('itemId', String(item.id))
    data.append('file', file)

    upload.mutate(data)
  }

  return (
    <Modal opened={opened} onClose={onClose} title={`Image · ${item.name}`} centered>
      <Stack>
        {item.hasImage ? (
          <Group justify="center">
            <Image src={src} alt={item.name} w={128} h={128} fit="contain" />
          </Group>
        ) : (
          <Text c="dimmed" size="sm" ta="center">
            No image yet. The app falls back to an emoji until one is uploaded.
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
                PNG, JPEG or WebP. Replaces the current image.
              </Text>
            </div>
          </Group>
        </Dropzone>

        <Group justify="space-between">
          <Button
            color="red"
            variant="subtle"
            disabled={!item.hasImage || remove.isPending}
            loading={remove.isPending}
            onClick={() => remove.mutate(item.id)}
          >
            Remove image
          </Button>

          <Button variant="default" onClick={onClose}>
            Close
          </Button>
        </Group>
      </Stack>
    </Modal>
  )
}
