import { dehydrate, HydrationBoundary, QueryClient } from '@tanstack/react-query'
import { cookies } from 'next/headers'
import type { Metadata } from 'next'

import MaterialsComponent from '@/components/pages/MaterialsComponent'
import { doQueryGet } from '@/helpers/apiClient'
import { QueryKeys } from '@/helpers/QueryKeys'
import type { AdminMaterial } from '@/interfaces/api/admin/AdminMaterial'

export const metadata: Metadata = { title: 'Materials' }

export default async function MaterialsPage() {
  const queryClient = new QueryClient()
  const cookieStore = await cookies()

  if (cookieStore.has('accessToken')) {
    const cookieHeader = cookieStore
      .getAll()
      .map((c) => `${c.name}=${c.value}`)
      .join('; ')

    await queryClient.prefetchQuery({
      queryKey: [QueryKeys.Materials],
      queryFn: async () =>
        await doQueryGet<AdminMaterial[]>('/api/Admin/GetMaterials', {
          headers: { Cookie: cookieHeader },
        }),
    })
  }

  return (
    <HydrationBoundary state={dehydrate(queryClient)}>
      <MaterialsComponent />
    </HydrationBoundary>
  )
}
