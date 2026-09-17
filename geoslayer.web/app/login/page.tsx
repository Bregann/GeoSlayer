import type { Metadata } from 'next'

import LoginComponent from '@/components/pages/LoginComponent'

export const metadata: Metadata = { title: 'Sign in' }

export default function LoginPage() {
  return <LoginComponent />
}
