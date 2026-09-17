import { dehydrate, HydrationBoundary, QueryClient } from '@tanstack/react-query'
import { cookies } from 'next/headers'
import type { Metadata } from 'next'

import ItemsComponent from '@/components/pages/ItemsComponent'
import { doQueryGet } from '@/helpers/apiClient'
import { QueryKeys } from '@/helpers/QueryKeys'
import type { AdminItem } from '@/interfaces/api/admin/AdminItem'

export const metadata: Metadata = { title: 'Items' }

/**
 * Server component: prefetch, then hand off. Follows orbit.web's page pattern — the page
 * owns fetching and metadata, the component owns interaction.
 */
export default async function ItemsPage() {
  const queryClient = new QueryClient()
  const cookieStore = await cookies()

  if (cookieStore.has('accessToken')) {
    const cookieHeader = cookieStore
      .getAll()
      .map((c) => `${c.name}=${c.value}`)
      .join('; ')

    await queryClient.prefetchQuery({
      queryKey: [QueryKeys.Items],
      queryFn: async () =>
        await doQueryGet<AdminItem[]>('/api/Admin/GetItems', {
          headers: { Cookie: cookieHeader },
        }),
    })
  }

  return (
    <HydrationBoundary state={dehydrate(queryClient)}>
      <ItemsComponent />
    </HydrationBoundary>
  )
}
