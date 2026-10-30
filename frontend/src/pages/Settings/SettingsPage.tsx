import { useEffect, useState } from 'react'
import { usePreferences } from '../../hooks/usePreferences'
import { api } from '../../services/api'
import type { GenetecStatus } from '../../types/models'

export function SettingsPage() {
  const preferences = usePreferences()
  const [status, setStatus] = useState<GenetecStatus | null>(null)
  const zones = ['UTC', 'Asia/Seoul', 'America/New_York', 'Europe/London', preferences.timeZone]

  useEffect(() => {
    void api<GenetecStatus>('/api/genetec/status').then(setStatus).catch(() => setStatus(null))
  }, [])

  return (
    <div className="mx-auto max-w-2xl space-y-6 p-6">
      <section className="rounded border border-line bg-panel p-5">
        <h2 className="text-lg font-semibold">Display</h2>
        <label className="mt-4 block text-sm">
          Time zone
          <select aria-label="Time zone" className="mt-1 w-full rounded border border-line bg-ink px-3 py-2" value={preferences.timeZone} onChange={(event) => preferences.update({ timeZone: event.target.value })}>
            {[...new Set(zones)].map((zone) => <option key={zone}>{zone}</option>)}
          </select>
        </label>
        <p className="mt-2 text-xs text-mist">Timestamps are stored in UTC and shown in this zone.</p>
      </section>
      <section className="rounded border border-line bg-panel p-5">
        <h2 className="text-lg font-semibold">Playback window</h2>
        <div className="mt-4 grid grid-cols-2 gap-3">
          <label className="text-sm">
            Seconds before
            <input aria-label="Seconds before" type="number" min={0} max={1800} className="mt-1 w-full rounded border border-line bg-ink px-3 py-2" value={preferences.preEventSeconds} onChange={(event) => preferences.update({ preEventSeconds: Number(event.target.value) })} />
          </label>
          <label className="text-sm">
            Seconds after
            <input aria-label="Seconds after" type="number" min={0} max={1800} className="mt-1 w-full rounded border border-line bg-ink px-3 py-2" value={preferences.postEventSeconds} onChange={(event) => preferences.update({ postEventSeconds: Number(event.target.value) })} />
          </label>
        </div>
      </section>
      <section className="rounded border border-line bg-panel p-5 text-sm">
        <h2 className="text-lg font-semibold">Integration</h2>
        <p className="mt-3 text-mist">Mode: {status?.mode ?? 'Checking…'}</p>
        <p className="text-mist">Transport: {status?.transport ?? '—'}</p>
        <p className="mt-2">{status?.directoryDetail}</p>
      </section>
    </div>
  )
}
