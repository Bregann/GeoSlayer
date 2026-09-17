import { dehydrate, HydrationBoundary, QueryClient } from '@tanstack/react-query'
import { cookies } from 'next/headers'
import type { Metadata } from 'next'

import EncountersComponent from '@/components/pages/EncountersComponent'
import { doQueryGet } from '@/helpers/apiClient'
import { QueryKeys } from '@/helpers/QueryKeys'
import type { AdminEncounter } from '@/interfaces/api/admin/AdminEncounter'

export const metadata: Metadata = { title: 'Encounters' }

export default async function EncountersPage() {
  const queryClient = new QueryClient()
  const cookieStore = await cookies()

  if (cookieStore.has('accessToken')) {
    const cookieHeader = cookieStore
      .getAll()
      .map((c) => `${c.name}=${c.value}`)
      .join('; ')

    await queryClient.prefetchQuery({
      queryKey: [QueryKeys.Encounters],
      queryFn: async () =>
        await doQueryGet<AdminEncounter[]>('/api/Admin/GetEncounters', {
          headers: { Cookie: cookieHeader },
        }),
    })
  }

  return (
    <HydrationBoundary state={dehydrate(queryClient)}>
      <EncountersComponent />
    </HydrationBoundary>
  )
}
