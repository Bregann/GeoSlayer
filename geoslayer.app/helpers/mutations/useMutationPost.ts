import { useMutation, useQueryClient } from '@tanstack/react-query'
import { authApiClient } from '../apiClient'

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
      const res = await authApiClient.post<TOutput>(url, input)

      if (res.status >= 400) {
        throw new Error('Failed to save'.concat(res.statusText))
      }

      return res.data
    },
    onSuccess: (data) => {
      if (options.onSuccess !== undefined) {
        options.onSuccess(data)
      }

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
    onError(error) {
      if (options.onError !== undefined) {
        options.onError(error)
      }
    }
  })
}
