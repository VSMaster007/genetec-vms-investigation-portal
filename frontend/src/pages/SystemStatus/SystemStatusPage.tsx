import { useEffect, useState } from 'react'
import { StatusPill } from '../../components/common/StatusPill'
import { api } from '../../services/api'
import type { SystemStatus } from '../../types/models'
import { formatInZone } from '../../utils/time'

export function SystemStatusPage() {
  const [status, setStatus] = useState<SystemStatus | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [checkedAt, setCheckedAt] = useState<string | null>(null)

  const load = () => {
    setError(null)
    void api<SystemStatus>('/api/system/status')
      .then((value) => {
        setStatus(value)
        setCheckedAt(new Date().toISOString())
      })
      .catch((caught: Error) => setError(caught.message))
  }

  useEffect(load, [])

  const rows = status ? [
    ['Application API', status.api],
    ['Database', status.database],
    ['Genetec Directory', status.genetecDirectory],
    ['SDK authentication', status.sdkAuthentication],
    ['Media Gateway', status.mediaGateway],
    ['External incident API', status.externalApi]
  ] as const : []

  return (
    <div className="mx-auto max-w-3xl p-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-xl font-semibold">System status</h2>
          <p className="text-sm text-mist">{status ? `${status.genetecMode} mode · ${status.transport}` : 'Checking connections…'}</p>
        </div>
        <button type="button" className="rounded border border-line px-3 py-2 text-sm" onClick={load}>Refresh</button>
      </div>
      {error ? <p className="mt-4 text-sm text-rose-300">{error}</p> : null}
      <ul className="mt-6 divide-y divide-line rounded border border-line bg-panel">
        {rows.map(([label, check]) => (
          <li key={label} className="px-4 py-4">
            <div className="flex items-center justify-between">
              <h3 className="font-medium">{label}</h3>
              <StatusPill state={check.state} />
            </div>
            <p className="mt-1 text-sm text-mist">{check.detail}</p>
          </li>
        ))}
      </ul>
      {checkedAt ? <p className="mt-3 text-xs text-mist">Last checked {formatInZone(checkedAt, Intl.DateTimeFormat().resolvedOptions().timeZone)}</p> : null}
    </div>
  )
}
