import type { NextConfig } from 'next'

const nextConfig: NextConfig = {
  // Item images are served by the API through the proxy, not by Next's optimiser — they
  // are small icons behind an auth-aware route, and optimising them would mean Next
  // fetching them server-side without the cookie.
  images: { unoptimized: true },
}

export default nextConfig
