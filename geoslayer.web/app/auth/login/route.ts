import { NextRequest, NextResponse } from 'next/server'

/**
 * Exchanges credentials for httpOnly cookies.
 *
 * The API returns its tokens in the response body. Handing that body to the browser would
 * mean storing a token somewhere JavaScript can read it, and any XSS would then be a full
 * account takeover. This route keeps the token server-side and sets it as an httpOnly
 * cookie the proxy reads — the browser never holds it.
 */

let API_BASE_URL = process.env.API_BASE_URL

if (process.env.NODE_ENV === 'development') {
  API_BASE_URL = 'http://localhost:5199/api'
}

interface LoginResponse {
  accessToken: string
  refreshToken: string
  username: string
}

export async function POST(req: NextRequest) {
  const body = await req.json()

  const res = await fetch(`${API_BASE_URL}/Auth/Login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })

  if (!res.ok) {
    // The API's own message, so "wrong password" does not become "500".
    const text = await res.text()

    return NextResponse.json(
      { title: text !== '' ? text : 'Could not sign in.' },
      { status: res.status }
    )
  }

  const data = (await res.json()) as LoginResponse

  const response = NextResponse.json({ username: data.username })

  const secure = process.env.NODE_ENV === 'production'

  response.cookies.set('accessToken', data.accessToken, {
    httpOnly: true,
    secure,
    sameSite: 'lax',
    path: '/',
    // Matches the token's own hour-long lifetime; a cookie outliving its token just
    // produces confusing 401s.
    maxAge: 60 * 60,
  })

  response.cookies.set('refreshToken', data.refreshToken, {
    httpOnly: true,
    secure,
    sameSite: 'lax',
    path: '/',
    maxAge: 60 * 60 * 24 * 30,
  })

  return response
}
