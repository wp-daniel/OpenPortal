import { currentLanguage, translate } from '@/i18n/store'
import type { AntiforgeryToken, ValidationProblemDetails } from './types'

/**
 * The error every rejected request rejects with.
 *
 * `fetch` does not reject on a 4xx or 5xx, so without this the caller's `catch` would silently miss every
 * server-reported failure and only ever see network faults. Turning the response into a rejection here is
 * what makes the React Query error paths reachable at all.
 */
export class ApiError extends Error {
  readonly status: number
  readonly problem: ValidationProblemDetails | null

  constructor(status: number, problem: ValidationProblemDetails | null) {
    super(problem?.title ?? translate('clientError.requestFailed', { status }))
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }

  /** True when nobody is signed in, so callers can route to the sign-in page. */
  get isUnauthenticated(): boolean {
    return this.status === 401
  }

  get isForbidden(): boolean {
    return this.status === 403
  }

  get isConflict(): boolean {
    return this.status === 409
  }

  /** Field-keyed messages from a validation failure, keyed by the field names the form also uses. */
  get fieldErrors(): Record<string, readonly string[]> {
    return this.problem?.errors ?? {}
  }

  /**
   * A single message for display. A validation failure is reported per field by the form, so the summary
   * line only needs to say something went wrong, not repeat the field text.
   */
  get summary(): string {
    const detail = this.problem?.detail

    if (detail) {
      return detail
    }

    if (this.problem?.errors && Object.keys(this.problem.errors).length > 0) {
      return translate('clientError.fieldsNeedAttention')
    }

    return this.message
  }

  /**
   * The trace id, so a user reporting a problem can name the exact server-side failure. It is only useful
   * when the server actually sent one.
   */
  get traceId(): string | undefined {
    return this.problem?.traceId
  }
}

/**
 * Holds the antiforgery request token for the browser session.
 *
 * The value sent in the header is the **request token** from `GET /api/auth/antiforgery`, never the contents
 * of the `XSRF-TOKEN` cookie. Those are two different values: ASP.NET Core keeps a secret in the cookie and
 * issues a request token derived from it, and sending the cookie value in the header fails with "the cookie
 * token and the request token were swapped". Reading the readable cookie is therefore not a shortcut - it is
 * the one thing that cannot work. Integration tests cannot catch this, because the test helper performs the
 * same handshake the browser does not.
 *
 * The token is bound to the signed-in identity, so signing in or out discards it. Caching it is safe in
 * between: the cookie token does not rotate on its own, and a stale token is recovered from by invalidating
 * on the 400 below and letting the caller retry.
 */
class AntiforgeryStore {
  #headerName = 'X-XSRF-TOKEN'
  #token: string | null = null
  #inFlight: Promise<string> | null = null

  /** Drops the cached token. Called whenever the identity behind the session may have changed. */
  invalidate(): void {
    this.#token = null
    this.#inFlight = null
  }

  async get(): Promise<string> {
    if (this.#token !== null) {
      return this.#token
    }

    // Concurrent writes share one token request: asking twice would race, and one response would be
    // discarded along with the nonce it carried.
    this.#inFlight ??= this.#requestToken().finally(() => {
      this.#inFlight = null
    })

    this.#token = await this.#inFlight

    return this.#token
  }

  async #requestToken(): Promise<string> {
    const response = await fetch('/api/auth/antiforgery', {
      credentials: 'same-origin',
      headers: { Accept: 'application/json' },
    })

    if (!response.ok) {
      throw new ApiError(response.status, null)
    }

    const token = (await response.json()) as AntiforgeryToken
    this.#headerName = token.headerName

    return token.requestToken
  }

  get headerName(): string {
    return this.#headerName
  }
}

const antiforgery = new AntiforgeryStore()

/** Exposed for the auth flow, which must not send a token minted for the previous identity. */
export function invalidateAntiforgeryToken(): void {
  antiforgery.invalidate()
}

type Method = 'GET' | 'POST' | 'PUT' | 'DELETE'

const safeMethods: ReadonlySet<Method> = new Set<Method>(['GET'])

export interface RequestOptions {
  readonly method?: Method
  readonly body?: unknown
  readonly signal?: AbortSignal
}

/**
 * Reads a problem+json body.
 *
 * Falls back to `null` when the body is not problem+json. A proxy or a crash page can answer an error with
 * HTML, and trying to parse that as JSON would replace a useful status code with a parse exception.
 */
async function readProblem(response: Response): Promise<ValidationProblemDetails | null> {
  try {
    return (await response.json()) as ValidationProblemDetails
  } catch {
    return null
  }
}

export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const method = options.method ?? 'GET'
  const headers: Record<string, string> = {
    Accept: 'application/json',
    // The server localizes its own messages (errors, validation) from this header.
    'Accept-Language': currentLanguage(),
  }

  if (options.body !== undefined) {
    headers['Content-Type'] = 'application/json'
  }

  if (!safeMethods.has(method)) {
    headers[antiforgery.headerName] = await antiforgery.get()
  }

  const response = await fetch(path, {
    method,
    headers,
    credentials: 'same-origin',
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
    signal: options.signal,
  })

  if (!response.ok) {
    const problem = await readProblem(response)

    // 400 from the antiforgery filter means the token no longer matches this identity. Dropping it makes the
    // next attempt fetch a fresh one instead of replaying the same rejected token forever.
    if (response.status === 400 && problem?.errorCode === 'antiforgery.invalid_token') {
      antiforgery.invalidate()
    }

    throw new ApiError(response.status, problem)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

export const api = {
  get: <T>(path: string, signal?: AbortSignal) => request<T>(path, { method: 'GET', signal }),
  post: <T>(path: string, body?: unknown) => request<T>(path, { method: 'POST', body }),
  put: <T>(path: string, body?: unknown) => request<T>(path, { method: 'PUT', body }),
  delete: <T>(path: string) => request<T>(path, { method: 'DELETE' }),
}

/** Builds a query string, dropping empty values so the server sees an absent filter rather than a blank one. */
export function withQuery(path: string, params: Record<string, string | number | undefined | null>): string {
  const search = new URLSearchParams()

  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && `${value}` !== '') {
      search.set(key, `${value}`)
    }
  }

  const query = search.toString()

  return query ? `${path}?${query}` : path
}