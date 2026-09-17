'use client'

import {
  Button,
  Card,
  Center,
  PasswordInput,
  Stack,
  Text,
  TextInput,
  Title,
} from '@mantine/core'
import { useRouter } from 'next/navigation'
import { useState } from 'react'

import { messageFrom, notifyError } from '@/helpers/notificationHelper'

/**
 * Sign in.
 *
 * Posts to /auth/login rather than the API directly, so the token lands in an httpOnly
 * cookie instead of in JavaScript.
 */
export default function LoginComponent() {
  const router = useRouter()

  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [busy, setBusy] = useState(false)

  const submit = async () => {
    setBusy(true)

    try {
      const res = await fetch('/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username, password }),
      })

      if (!res.ok) {
        const problem = await res.json().catch(() => ({ title: 'Could not sign in.' }))
        notifyError(problem.title ?? 'Could not sign in.')
        return
      }

      // Server components read the cookie, so the route has to be re-rendered rather than
      // navigated to client-side.
      router.replace('/items')
      router.refresh()
    } catch (error) {
      notifyError(messageFrom(error))
    } finally {
      setBusy(false)
    }
  }

  return (
    <Center mih="100vh" p="md">
      <Card withBorder w={380} padding="lg">
        <Stack>
          <div>
            <Title order={3}>GeoSlayer Admin</Title>
            <Text c="dimmed" size="sm">
              Admin accounts only.
            </Text>
          </div>

          <TextInput
            label="Username"
            value={username}
            onChange={(e) => setUsername(e.currentTarget.value)}
            autoComplete="username"
          />

          <PasswordInput
            label="Password"
            value={password}
            onChange={(e) => setPassword(e.currentTarget.value)}
            autoComplete="current-password"
            onKeyDown={(e) => e.key === 'Enter' && submit()}
          />

          <Button onClick={submit} loading={busy} disabled={username === '' || password === ''}>
            Sign in
          </Button>
        </Stack>
      </Card>
    </Center>
  )
}
