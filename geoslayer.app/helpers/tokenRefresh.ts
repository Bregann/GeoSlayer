import axios from 'axios'

import { API_BASE_URL } from '@/constants/api'
import { keychainHelper } from './keychainHelper'

/**
 * Token refresh, extracted so the background location task can use it too.
 *
 * Deliberately free of React, expo-router and the axios interceptors: the background
 * task runs with no navigation container mounted, so anything that calls
 * `router.replace` would throw there.  Callers decide what a failure means — the
 * foreground client logs the user out, the background task just gives up and retries
 * on the next tick.
 */

/** Bare client for the refresh call itself — using the auth client would recurse. */
const refreshClient = axios.create({
  baseURL: API_BASE_URL,
  validateStatus: (status) => status < 500,
})

/** In-flight refresh, shared so concurrent callers do not each burn a refresh token. */
let inFlight: Promise<string | null> | null = null

/**
 * Exchange the stored refresh token for a new access token.
 *
 * Returns the new access token, or null when there is nothing to refresh with or the
 * server rejected it.  On a null return the stored tokens have already been cleared.
 */
export async function refreshAccessToken(): Promise<string | null> {
  // Collapse concurrent callers onto one request.  A refresh token is typically
  // single-use, so two parallel refreshes would leave one caller holding a dead one.
  if (inFlight) return inFlight

  inFlight = (async () => {
    try {
      const refreshToken = await keychainHelper.getRefreshToken()
      if (refreshToken === null) return null

      const { data, status } = await refreshClient.post('/api/Auth/RefreshAppToken', {
        refreshToken,
      })

      if (status >= 400 || !data?.accessToken) {
        await keychainHelper.deleteTokens()
        return null
      }

      await keychainHelper.setAccessToken(data.accessToken)
      await keychainHelper.setRefreshToken(data.refreshToken)

      return data.accessToken as string
    } catch {
      // A network failure is not proof the refresh token is bad, but we cannot tell
      // the difference here.  Leave the tokens alone and let the caller retry.
      return null
    } finally {
      inFlight = null
    }
  })()

  return inFlight
}
