import { useMutation, useQueryClient } from '@tanstack/react-query'
import { doPost } from '../apiClient'

interface MutationUploadOptions {
  url: string
  queryKey: string[]
  alsoInvalidate?: string[][]
  onError?: (_error: Error) => void
  onSuccess?: () => void
}

/**
 * Multipart upload.
 *
 * Separate from useMutationPost because a FormData body must not be JSON-stringified and
 * must not carry a manually-set Content-Type — the browser sets it, with the multipart
 * boundary the server needs to parse the request.
 */
export function useMutationUpload(options: MutationUploadOptions) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (formData: FormData) => {
      const res = await doPost<unknown>(options.url, { formData })

      if (!res.ok) {
        throw new Error(res.statusMessage ?? 'Upload failed')
      }
    },
    onSuccess: () => {
      options.onSuccess?.()

      for (const key of options.alsoInvalidate ?? []) {
        queryClient.invalidateQueries({ queryKey: key })
      }

      queryClient.invalidateQueries({ queryKey: options.queryKey })
    },
    onError: (error: Error) => options.onError?.(error),
  })
}
