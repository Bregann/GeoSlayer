import { dehydrate, HydrationBoundary, QueryClient } from '@tanstack/react-query'
import { cookies } from 'next/headers'
import type { Metadata } from 'next'

import RecipesComponent from '@/components/pages/RecipesComponent'
import { doQueryGet } from '@/helpers/apiClient'
import { QueryKeys } from '@/helpers/QueryKeys'
import type { AdminRecipe } from '@/interfaces/api/admin/AdminRecipe'
import type { AdminMaterial } from '@/interfaces/api/admin/AdminMaterial'
import type { AdminItem } from '@/interfaces/api/admin/AdminItem'

export const metadata: Metadata = { title: 'Recipes' }

export default async function RecipesPage() {
  const queryClient = new QueryClient()
  const cookieStore = await cookies()

  if (cookieStore.has('accessToken')) {
    const cookieHeader = cookieStore
      .getAll()
      .map((c) => `${c.name}=${c.value}`)
      .join('; ')

    const headers = { Cookie: cookieHeader }

    // Materials and items are prefetched too: the edit form needs both to offer an output
    // and inputs, and fetching them on open would make the modal flash empty.
    await Promise.all([
      queryClient.prefetchQuery({
        queryKey: [QueryKeys.Recipes],
        queryFn: async () => await doQueryGet<AdminRecipe[]>('/api/Admin/GetRecipes', { headers }),
      }),
      queryClient.prefetchQuery({
        queryKey: [QueryKeys.Materials],
        queryFn: async () => await doQueryGet<AdminMaterial[]>('/api/Admin/GetMaterials', { headers }),
      }),
      queryClient.prefetchQuery({
        queryKey: [QueryKeys.Items],
        queryFn: async () => await doQueryGet<AdminItem[]>('/api/Admin/GetItems', { headers }),
      }),
    ])
  }

  return (
    <HydrationBoundary state={dehydrate(queryClient)}>
      <RecipesComponent />
    </HydrationBoundary>
  )
}
