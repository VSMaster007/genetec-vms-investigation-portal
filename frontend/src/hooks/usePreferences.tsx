import { createContext, useContext, useMemo, useState, type ReactNode } from 'react'

export interface Preferences {
  timeZone: string
  preEventSeconds: number
  postEventSeconds: number
}

interface PreferencesState extends Preferences {
  update(next: Partial<Preferences>): void
}

const storageKey = 'portal.preferences'
const PreferencesContext = createContext<PreferencesState | null>(null)

function load(): Preferences {
  const fallback: Preferences = {
    timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC',
    preEventSeconds: 60,
    postEventSeconds: 60
  }
  const raw = localStorage.getItem(storageKey)
  if (!raw) {
    return fallback
  }
  return { ...fallback, ...JSON.parse(raw) as Partial<Preferences> }
}

export function PreferencesProvider({ children }: { children: ReactNode }) {
  const [preferences, setPreferences] = useState<Preferences>(load)

  const value = useMemo<PreferencesState>(() => ({
    ...preferences,
    update(next) {
      const merged = { ...preferences, ...next }
      localStorage.setItem(storageKey, JSON.stringify(merged))
      setPreferences(merged)
    }
  }), [preferences])

  return <PreferencesContext.Provider value={value}>{children}</PreferencesContext.Provider>
}

export function usePreferences(): PreferencesState {
  const value = useContext(PreferencesContext)
  if (!value) {
    throw new Error('PreferencesProvider is missing.')
  }
  return value
}
