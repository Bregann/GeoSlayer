import { useMutation, useQueryClient } from '@tanstack/react-query'
import { doPost } from '../apiClient'

interface MutationPostOptions<TInput, TOutput> {
  url: string | ((_input: TInput) => string)
  queryKey: string[]
  /** Extra keys to invalidate alongside queryKey. */
  alsoInvalidate?: string[][]
  invalidateQuery: boolean
  onError?: (_error: Error) => void
  onSuccess?: (_data: TOutput | undefined) => void
}

export function useMutationPost<TInput, TOutput>(options: MutationPostOptions<TInput, TOutput>) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (input: TInput) => {
      const url = typeof options.url === 'function' ? options.url(input) : options.url
      const res = await doPost<TOutput>(url, { body: input })

      if (!res.ok) {
        // The API's own message, not a status code — it is written to be read.
        throw new Error(res.statusMessage ?? 'Failed to save')
      }

      return res.data
    },
    onSuccess: (data) => {
      options.onSuccess?.(data)

      for (const key of options.alsoInvalidate ?? []) {
        queryClient.invalidateQueries({ queryKey: key })
      }

      if (options.invalidateQuery) {
        queryClient.invalidateQueries({ queryKey: options.queryKey })
        return
      }

      if (data !== undefined) {
        queryClient.setQueryData(options.queryKey, data)
      }
    },
    onError: (error: Error) => options.onError?.(error),
  })
}
