/**
 * API access, following orbit.web's apiClient.
 *
 * Every request goes through the Next proxy at `/api/...` rather than straight to the
 * backend: that is what keeps the access token in an httpOnly cookie the browser's
 * JavaScript cannot read, and it means there is no CORS to configure.
 */

const isBrowser = typeof window !== 'undefined'

/** Server components have no origin, so they need an absolute URL. */
const BASE_URL = isBrowser
  ? ''
  : (process.env.NEXT_PUBLIC_SITE_URL ?? 'http://localhost:3000')

type HttpMethod = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'

export interface FetchResponse<T> {
  data?: T
  status: number
  ok: boolean
  statusMessage?: string
}

/** ASP.NET's error shape — GeoSlayer.Core uses AddProblemDetails. */
interface ProblemDetails {
  status?: number
  title?: string
  detail?: string
}

interface RequestOptions {
  headers?: HeadersInit
  body?: unknown
  /** A FormData body is sent as-is, so the browser can set the multipart boundary. */
  formData?: FormData
}

async function doRequest<T>(
  method: HttpMethod,
  endpoint: string,
  options: RequestOptions = {}
): Promise<FetchResponse<T>> {
  const { body, headers = {}, formData } = options

  try {
    const res = await fetch(`${BASE_URL}${endpoint}`, {
      method,
      credentials: 'include',
      headers: {
        // Content-Type is deliberately omitted for FormData: setting it manually would
        // drop the multipart boundary the browser generates, and the upload would arrive
        // unparseable.
        ...(formData === undefined ? { 'Content-Type': 'application/json' } : {}),
        ...headers,
      },
      body: formData ?? (body !== undefined ? JSON.stringify(body) : undefined),
    })

    let data: T | undefined
    let statusMessage: string | undefined

    const text = await res.text()

    if (!res.ok) {
      // The API's message is far more useful than a status code — SaveItem explains
      // *why* a modifier was refused, for instance — so it is surfaced rather than
      // swallowed.
      try {
        if (text !== '') {
          const problem: ProblemDetails = JSON.parse(text)
          statusMessage = problem.detail ?? problem.title ?? text
        }
      } catch {
        statusMessage = text
      }
    } else if (text !== '') {
      try {
        data = JSON.parse(text) as T
      } catch {
        console.warn(`Failed to parse JSON from ${endpoint}`)
      }
    }

    return { data, status: res.status, ok: res.ok, statusMessage }
  } catch (error) {
    console.error(`Request failed: ${method} ${endpoint}`, error)

    return { status: 500, ok: false, statusMessage: 'Could not reach the server.' }
  }
}

export const doGet = <T>(endpoint: string, options?: RequestOptions) =>
  doRequest<T>('GET', endpoint, options)

export const doPost = <T>(endpoint: string, options?: RequestOptions) =>
  doRequest<T>('POST', endpoint, options)

export const doPut = <T>(endpoint: string, options?: RequestOptions) =>
  doRequest<T>('PUT', endpoint, options)

export const doPatch = <T>(endpoint: string, options?: RequestOptions) =>
  doRequest<T>('PATCH', endpoint, options)

export const doDelete = <T>(endpoint: string, options?: RequestOptions) =>
  doRequest<T>('DELETE', endpoint, options)

/**
 * The React Query variant: throws rather than returning a result shape, because that is
 * what useQuery expects in order to populate `isError`.
 */
export async function doQueryGet<T>(endpoint: string, options?: RequestOptions): Promise<T> {
  const res = await doGet<T>(endpoint, options)

  if (!res.ok) {
    throw new Error(res.statusMessage ?? `Failed to fetch ${endpoint}`)
  }

  return res.data as T
}
