import { useMutation, useQueryClient } from '@tanstack/react-query'
import { doDelete } from '../apiClient'

interface MutationDeleteOptions<TInput> {
  url: string | ((_input: TInput) => string)
  queryKey: string[]
  alsoInvalidate?: string[][]
  onError?: (_error: Error) => void
  onSuccess?: () => void
}

export function useMutationDelete<TInput>(options: MutationDeleteOptions<TInput>) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (input: TInput) => {
      const url = typeof options.url === 'function' ? options.url(input) : options.url
      const res = await doDelete<unknown>(url)

      if (!res.ok) {
        throw new Error(res.statusMessage ?? 'Failed to delete')
      }
    },
    onSuccess: () => {
      options.onSuccess?.()

      for (const key of options.alsoInvalidate ?? []) {
        queryClient.invalidateQueries({ queryKey: key })
      }

      // Always refetched rather than patched locally: a delete can be refused server-side
      // (an item a recipe produces), so the list must come from the server.
      queryClient.invalidateQueries({ queryKey: options.queryKey })
    },
    onError: (error: Error) => options.onError?.(error),
  })
}
