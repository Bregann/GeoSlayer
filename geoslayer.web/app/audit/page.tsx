import { dehydrate, HydrationBoundary, QueryClient } from '@tanstack/react-query'
import { cookies } from 'next/headers'
import type { Metadata } from 'next'

import AuditComponent from '@/components/pages/AuditComponent'
import { doQueryGet } from '@/helpers/apiClient'
import { QueryKeys } from '@/helpers/QueryKeys'
import type { AdminAudit } from '@/interfaces/api/admin/AdminAudit'

export const metadata: Metadata = { title: 'Audit trail' }

export default async function AuditPage() {
  const queryClient = new QueryClient()
  const cookieStore = await cookies()

  if (cookieStore.has('accessToken')) {
    const cookieHeader = cookieStore
      .getAll()
      .map((c) => `${c.name}=${c.value}`)
      .join('; ')

    await queryClient.prefetchQuery({
      queryKey: [QueryKeys.AuditTrail],
      queryFn: async () =>
        await doQueryGet<AdminAudit[]>('/api/Admin/GetAuditTrail?limit=200', {
          headers: { Cookie: cookieHeader },
        }),
    })
  }

  return (
    <HydrationBoundary state={dehydrate(queryClient)}>
      <AuditComponent />
    </HydrationBoundary>
  )
}
