import { dehydrate, HydrationBoundary, QueryClient } from '@tanstack/react-query'
import { cookies } from 'next/headers'
import type { Metadata } from 'next'

import ProgressionComponent from '@/components/pages/ProgressionComponent'
import { doQueryGet } from '@/helpers/apiClient'
import { QueryKeys } from '@/helpers/QueryKeys'
import type { AdminProgression } from '@/interfaces/api/admin/AdminProgression'

export const metadata: Metadata = { title: 'Progression' }

export default async function ProgressionPage() {
  const queryClient = new QueryClient()
  const cookieStore = await cookies()

  if (cookieStore.has('accessToken')) {
    const cookieHeader = cookieStore
      .getAll()
      .map((c) => `${c.name}=${c.value}`)
      .join('; ')

    await queryClient.prefetchQuery({
      queryKey: [QueryKeys.Progression],
      queryFn: async () =>
        await doQueryGet<AdminProgression>('/api/Admin/GetProgression', { headers: { Cookie: cookieHeader } }),
    })
  }

  return (
    <HydrationBoundary state={dehydrate(queryClient)}>
      <ProgressionComponent />
    </HydrationBoundary>
  )
}
