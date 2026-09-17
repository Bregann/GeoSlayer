import '@mantine/core/styles.css'
import '@mantine/notifications/styles.css'
import '@mantine/dropzone/styles.css'

import { ColorSchemeScript, mantineHtmlProps } from '@mantine/core'
import type { Metadata } from 'next'
import NextTopLoader from 'nextjs-toploader'

import { Providers } from './providers'
import { AdminShell } from '@/components/common/AdminShell'

export const metadata: Metadata = {
  title: { default: 'GeoSlayer Admin', template: '%s · GeoSlayer Admin' },
  description: 'Game management for GeoSlayer.',
}

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" {...mantineHtmlProps}>
      <head>
        <ColorSchemeScript defaultColorScheme="dark" />
      </head>
      <body>
        <NextTopLoader showSpinner={false} />
        <Providers>
          <AdminShell>{children}</AdminShell>
        </Providers>
      </body>
    </html>
  )
}
