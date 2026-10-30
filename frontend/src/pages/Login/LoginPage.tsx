import { FormEvent, useState } from 'react'
import { Navigate } from 'react-router-dom'
import { ApiError } from '../../services/api'
import { useAuth } from '../../hooks/useAuth'

export function LoginPage() {
  const { user, login } = useAuth()
  const [email, setEmail] = useState('investigator@investigation.local')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)

  if (user) {
    return <Navigate to="/dashboard" replace />
  }

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    setPending(true)
    setError(null)
    try {
      await login(email, password)
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'Sign-in failed.')
    } finally {
      setPending(false)
    }
  }

  return (
    <main className="flex min-h-screen items-center justify-center bg-ink px-4">
      <form onSubmit={submit} className="w-full max-w-md rounded border border-line bg-panel p-8">
        <p className="text-xs uppercase tracking-[0.18em] text-accent">Genetec VMS</p>
        <h1 className="mt-2 text-2xl font-semibold">Investigation Portal</h1>
        <p className="mt-2 text-sm text-mist">Sign in to review timestamped incidents and archived camera playback.</p>
        <label className="mt-6 block text-sm">
          Email
          <input className="mt-1 w-full rounded border border-line bg-ink px-3 py-2" value={email} onChange={(event) => setEmail(event.target.value)} type="email" required />
        </label>
        <label className="mt-4 block text-sm">
          Password
          <input className="mt-1 w-full rounded border border-line bg-ink px-3 py-2" value={password} onChange={(event) => setPassword(event.target.value)} type="password" required />
        </label>
        {error ? <p className="mt-4 text-sm text-rose-300">{error}</p> : null}
        <button type="submit" disabled={pending} className="mt-6 w-full rounded bg-accent px-4 py-2 font-medium text-ink">
          {pending ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
    </main>
  )
}
