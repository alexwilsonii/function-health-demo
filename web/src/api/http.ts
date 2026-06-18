export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  errors?: Record<string, string[]>
}

/** Thrown for any non-2xx response. Carries the parsed ProblemDetails so forms can map field errors. */
export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails | null

  constructor(status: number, problem: ProblemDetails | null) {
    super(problem?.title ?? `Request failed (${status})`)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }

  /** Field-keyed validation errors (camelCased keys matching the form fields). */
  get fieldErrors(): Record<string, string[]> {
    return this.problem?.errors ?? {}
  }
}

let onUnauthorized: (() => void) | null = null

/** Registered once at startup to clear auth + redirect to login on an unexpected 401. */
export function setUnauthorizedHandler(handler: () => void) {
  onUnauthorized = handler
}

export async function api<T>(path: string, options: RequestInit = {}): Promise<T> {
  const res = await fetch(`/api${path}`, {
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', ...options.headers },
    ...options,
  })

  // A 401 on a normal call means the session expired — bounce to login. Auth calls handle their
  // own 401 (bad credentials) inline, so they're excluded.
  if (res.status === 401 && !path.startsWith('/auth/')) {
    onUnauthorized?.()
  }

  if (!res.ok) {
    let problem: ProblemDetails | null = null
    try {
      problem = (await res.json()) as ProblemDetails
    } catch {
      /* no/!json body */
    }
    throw new ApiError(res.status, problem)
  }

  if (res.status === 204) return undefined as T
  return (await res.json()) as T
}
