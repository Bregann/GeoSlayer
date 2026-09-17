import { redirect } from 'next/navigation'

/** Nothing lives at the root; items is the useful landing place. */
export default function HomePage() {
  redirect('/items')
}
