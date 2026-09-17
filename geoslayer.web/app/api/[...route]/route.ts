export const runtime = 'nodejs'

import { NextRequest, NextResponse } from 'next/server'
import { cookies } from 'next/headers'

/**
 * Proxy to the GeoSlayer API, following orbit.web's route handler.
 *
 * Two jobs. It attaches the access token from an httpOnly cookie, so the browser never
 * holds a token in JavaScript where an XSS could read it. And it keeps the API on one
 * origin from the browser's point of view, which avoids CORS entirely.
 *
 * The body is streamed rather than buffered, which is what makes multipart uploads work —
 * item images go through here.
 */

let API_BASE_URL = process.env.API_BASE_URL

if (process.env.NODE_ENV === 'development') {
  API_BASE_URL = 'http://localhost:5199/api'
}

const ACCESS_TOKEN_COOKIE = 'accessToken'

/** Connection-level headers that must not be forwarded to the backend. */
const HOP_BY_HOP_HEADERS = [
  'host',
  'connection',
  'keep-alive',
  'proxy-authenticate',
  'proxy-authorization',
  'te',
  'trailers',
  'transfer-encoding',
  'content-length', // let fetch calculate it
]

async function handler(
  req: NextRequest,
  { params }: { params: Promise<{ route: string[] }> }
) {
  const { route } = await params
  const qs = req.nextUrl.searchParams.toString()
  const url = `${API_BASE_URL}/${route.join('/')}${qs !== '' ? `?${qs}` : ''}`

  const cookieStore = await cookies()
  const accessToken = cookieStore.get(ACCESS_TOKEN_COOKIE)?.value

  const headers = new Headers()
  req.headers.forEach((value, key) => {
    if (!HOP_BY_HOP_HEADERS.includes(key.toLowerCase())) {
      headers.append(key, value)
    }
  })

  if (accessToken !== undefined) {
    headers.set('Authorization', `Bearer ${accessToken}`)
  }

  // Streamed, not buffered: an image upload should not be read into memory here just to
  // be written straight back out.
  const body = req.method === 'GET' || req.method === 'HEAD' ? null : req.body

  try {
    const res = await fetch(url, {
      method: req.method,
      headers,
      body,
      // Required by undici whenever a stream is used as a body.
      ...(body !== null ? { duplex: 'half' } : {}),
    } as RequestInit)

    const responseHeaders = new Headers()
    res.headers.forEach((value, key) => {
      if (!HOP_BY_HOP_HEADERS.includes(key.toLowerCase())) {
        responseHeaders.append(key, value)
      }
    })

    return new NextResponse(res.body, {
      status: res.status,
      statusText: res.statusText,
      headers: responseHeaders,
    })
  } catch (error) {
    console.error(`Proxy error for ${req.method} ${url}:`, error)

    return NextResponse.json(
      { title: 'The API could not be reached.' },
      { status: 502 }
    )
  }
}

export {
  handler as GET,
  handler as POST,
  handler as PUT,
  handler as PATCH,
  handler as DELETE,
}
