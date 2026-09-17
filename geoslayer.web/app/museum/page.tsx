import { dehydrate, HydrationBoundary, QueryClient } from '@tanstack/react-query'
import { cookies } from 'next/headers'
import type { Metadata } from 'next'

import MuseumComponent from '@/components/pages/MuseumComponent'
import { doQueryGet } from '@/helpers/apiClient'
import { QueryKeys } from '@/helpers/QueryKeys'
import type { AdminMuseumEntry } from '@/interfaces/api/admin/AdminMuseumEntry'

export const metadata: Metadata = { title: 'Museum' }

export default async function MuseumPage() {
  const queryClient = new QueryClient()
  const cookieStore = await cookies()

  if (cookieStore.has('accessToken')) {
    const cookieHeader = cookieStore
      .getAll()
      .map((c) => `${c.name}=${c.value}`)
      .join('; ')

    await queryClient.prefetchQuery({
      queryKey: [QueryKeys.MuseumEntries],
      queryFn: async () =>
        await doQueryGet<AdminMuseumEntry[]>('/api/Admin/GetMuseumEntries', { headers: { Cookie: cookieHeader } }),
    })
  }

  return (
    <HydrationBoundary state={dehydrate(queryClient)}>
      <MuseumComponent />
    </HydrationBoundary>
  )
}
