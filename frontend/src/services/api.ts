import type { Problem } from '../types/models'

export class ApiError extends Error {
  status: number
  code?: string

  constructor(message: string, status: number, code?: string) {
    super(message)
    this.status = status
    this.code = code
  }
}

export async function api<T>(path: string, options: RequestInit = {}): Promise<T> {
  const headers = new Headers(options.headers)
  headers.set('Accept', 'application/json')
  if (options.body && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }

  const token = sessionStorage.getItem('portal.token')
  if (token) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  const response = await fetch(path, { ...options, headers })
  if (response.status === 401 && !path.endsWith('/api/auth/login')) {
    sessionStorage.removeItem('portal.token')
    window.location.assign('/login')
  }

  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as Problem | null
    throw new ApiError(problem?.detail || problem?.title || response.statusText, response.status, problem?.title)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return response.json() as Promise<T>
}
