import type { ReactNode } from 'react'
import { NavLink, useNavigate } from 'react-router-dom'
import { useEffect, useState } from 'react'
import { useAuth } from '../../hooks/useAuth'
import { api } from '../../services/api'
import type { AuditRecord } from '../../types/models'

const links = [
  ['/dashboard', 'Dashboard'],
  ['/investigations', 'Investigations'],
  ['/cameras', 'Camera Management'],
  ['/status', 'System Status'],
  ['/settings', 'Settings']
]

export function AppShell({ children }: { children: ReactNode }) {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const [query, setQuery] = useState('')
  const [open, setOpen] = useState(false)
  const [notes, setNotes] = useState<AuditRecord[]>([])

  useEffect(() => {
    void api<AuditRecord[]>('/api/audit?take=6').then(setNotes).catch(() => setNotes([]))
  }, [])

  return (
    <div className="flex min-h-screen bg-ink text-paper">
      <aside className="flex w-60 shrink-0 flex-col border-r border-line bg-panel">
        <div className="border-b border-line px-5 py-5">
          <p className="text-xs uppercase tracking-[0.18em] text-accent">Genetec VMS</p>
          <h1 className="mt-1 text-lg font-semibold">Investigation Portal</h1>
        </div>
        <nav className="flex flex-1 flex-col gap-1 p-3">
          {links.map(([path, label]) => (
            <NavLink
              key={path}
              to={path}
              className={({ isActive }) => `rounded px-3 py-2 text-sm ${isActive ? 'bg-accent text-ink' : 'text-mist hover:bg-panel2 hover:text-paper'}`}
            >
              {label}
            </NavLink>
          ))}
        </nav>
      </aside>
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex h-16 items-center gap-4 border-b border-line px-5">
          <form
            className="flex flex-1 items-center gap-2"
            onSubmit={(event) => {
              event.preventDefault()
              navigate(`/investigations?incidentId=${encodeURIComponent(query)}`)
            }}
          >
            <input
              aria-label="Search incidents"
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              placeholder="Search incident ID or title"
              className="w-full max-w-xl rounded border border-line bg-panel px-3 py-2 text-sm outline-none focus:border-accent"
            />
            <button type="submit" className="rounded border border-line px-3 py-2 text-sm">Search</button>
          </form>
          <div className="relative">
            <button type="button" className="rounded border border-line px-3 py-2 text-sm" onClick={() => setOpen((value) => !value)}>
              Notifications
            </button>
            {open ? (
              <div className="absolute right-0 z-10 mt-2 w-80 rounded border border-line bg-panel p-3 shadow-xl">
                {notes.length === 0 ? <p className="text-sm text-mist">No investigation activity yet.</p> : notes.map((note) => (
                  <p key={note.id} className="border-b border-line py-2 text-xs last:border-0">
                    <span className="text-paper">{note.action}</span>
                    <span className="mt-1 block text-mist">{note.details}</span>
                  </p>
                ))}
              </div>
            ) : null}
          </div>
          <div className="text-right text-sm">
            <p>{user?.displayName}</p>
            <p className="text-xs text-mist">{user?.role}</p>
          </div>
          <button type="button" className="rounded border border-line px-3 py-2 text-sm" onClick={logout}>Sign out</button>
        </header>
        <main className="min-h-0 flex-1">{children}</main>
      </div>
    </div>
  )
}
