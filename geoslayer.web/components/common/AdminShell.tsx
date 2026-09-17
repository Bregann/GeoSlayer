'use client'

import { AppShell, Burger, Group, NavLink, Text } from '@mantine/core'
import { useDisclosure } from '@mantine/hooks'
import { IconClipboardList, IconSword } from '@tabler/icons-react'
import Link from 'next/link'
import { usePathname } from 'next/navigation'

/**
 * The admin chrome.
 *
 * Deliberately plain. This is the first surface in the project that is not for players,
 * which changes the rules: it can be dense and assume competence, where the app has to be
 * legible on a phone in one hand. Nothing here is decorated for its own sake.
 *
 * The login page renders without the shell — a nav bar full of links you cannot use yet is
 * worse than no nav bar.
 */

const NAV = [
  { href: '/items', label: 'Items', icon: IconSword },
  { href: '/audit', label: 'Audit trail', icon: IconClipboardList },
]

export function AdminShell({ children }: { children: React.ReactNode }) {
  const [opened, { toggle }] = useDisclosure()
  const pathname = usePathname()

  if (pathname === '/login') {
    return <>{children}</>
  }

  return (
    <AppShell
      header={{ height: 56 }}
      navbar={{ width: 220, breakpoint: 'sm', collapsed: { mobile: !opened } }}
      padding="md"
    >
      <AppShell.Header>
        <Group h="100%" px="md" gap="sm">
          <Burger opened={opened} onClick={toggle} hiddenFrom="sm" size="sm" />
          <Text fw={700}>GeoSlayer Admin</Text>
        </Group>
      </AppShell.Header>

      <AppShell.Navbar p="xs">
        {NAV.map(({ href, label, icon: Icon }) => (
          <NavLink
            key={href}
            component={Link}
            href={href}
            label={label}
            leftSection={<Icon size={18} />}
            active={pathname.startsWith(href)}
          />
        ))}
      </AppShell.Navbar>

      <AppShell.Main>{children}</AppShell.Main>
    </AppShell>
  )
}
