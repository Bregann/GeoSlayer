import { dehydrate, HydrationBoundary, QueryClient } from '@tanstack/react-query'
import { cookies } from 'next/headers'
import type { Metadata } from 'next'

import SettingsComponent from '@/components/pages/SettingsComponent'
import { doQueryGet } from '@/helpers/apiClient'
import { QueryKeys } from '@/helpers/QueryKeys'
import type { AdminGameSetting } from '@/interfaces/api/admin/AdminGameSetting'

export const metadata: Metadata = { title: 'Tuning' }

export default async function SettingsPage() {
  const queryClient = new QueryClient()
  const cookieStore = await cookies()

  if (cookieStore.has('accessToken')) {
    const cookieHeader = cookieStore
      .getAll()
      .map((c) => `${c.name}=${c.value}`)
      .join('; ')

    await queryClient.prefetchQuery({
      queryKey: [QueryKeys.GameSettings],
      queryFn: async () =>
        await doQueryGet<AdminGameSetting[]>('/api/Admin/GetGameSettings', { headers: { Cookie: cookieHeader } }),
    })
  }

  return (
    <HydrationBoundary state={dehydrate(queryClient)}>
      <SettingsComponent />
    </HydrationBoundary>
  )
}
