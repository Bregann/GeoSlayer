import type { Metadata } from 'next'

import PlayersComponent from '@/components/pages/PlayersComponent'

export const metadata: Metadata = { title: 'Players' }

/**
 * No prefetch: the list is a search result, and there is nothing useful to show before
 * someone has typed a query.
 */
export default function PlayersPage() {
  return <PlayersComponent />
}
