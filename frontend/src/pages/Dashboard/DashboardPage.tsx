import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../../services/api'
import { usePreferences } from '../../hooks/usePreferences'
import { formatInZone } from '../../utils/time'
import { StatusPill } from '../../components/common/StatusPill'
import type { Dashboard } from '../../types/models'

export function DashboardPage() {
  const { timeZone } = usePreferences()
  const [data, setData] = useState<Dashboard | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    void api<Dashboard>('/api/dashboard').then(setData).catch((caught: Error) => setError(caught.message))
  }, [])

  if (error) {
    return <p className="p-6 text-rose-300">{error}</p>
  }
  if (!data) {
    return <p className="p-6 text-mist">Loading dashboard…</p>
  }

  const cards = [
    ['Total incidents', String(data.totalIncidents)],
    ['Today (UTC)', String(data.todaysIncidents)],
    [data.cameraCatalogMode === 'Mock' ? 'Mock cameras' : 'Catalog cameras', String(data.catalogCameras)],
    ['Mapped cameras', String(data.mappedCameras)]
  ]

  return (
    <div className="space-y-6 p-6">
      <div className="grid gap-4 md:grid-cols-4">
        {cards.map(([label, value]) => (
          <article key={label} className="rounded border border-line bg-panel p-4">
            <p className="text-xs uppercase tracking-wide text-mist">{label}</p>
            <p className="mt-2 text-3xl font-semibold">{value}</p>
          </article>
        ))}
      </div>
      <section className="rounded border border-line bg-panel p-4">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-semibold">Genetec connection</h2>
          <StatusPill state={data.genetec.mode === 'Mock' ? 'Mock' : data.genetec.directoryConnected ? 'Healthy' : 'Unhealthy'} />
        </div>
        <p className="mt-2 text-sm text-mist">{data.genetec.directoryDetail}</p>
      </section>
      <div className="grid gap-4 lg:grid-cols-2">
        <section className="rounded border border-line bg-panel p-4">
          <h2 className="text-lg font-semibold">Recent incidents</h2>
          <ul className="mt-3 divide-y divide-line">
            {data.recentIncidents.map((incident) => (
              <li key={incident.id} className="py-3">
                <Link to={`/investigations/${incident.id}`} className="font-medium text-accent">{incident.externalIncidentId}</Link>
                <p className="text-sm">{incident.title}</p>
                <p className="text-xs text-mist">{formatInZone(incident.eventTimestampUtc, timeZone)} · {incident.location}</p>
              </li>
            ))}
          </ul>
        </section>
        <section className="rounded border border-line bg-panel p-4">
          <h2 className="text-lg font-semibold">Recent investigations</h2>
          <ul className="mt-3 divide-y divide-line">
            {data.recentInvestigations.map((item) => (
              <li key={item.id} className="py-3 text-sm">
                <p>{item.action}{item.externalIncidentId ? ` · ${item.externalIncidentId}` : ''}</p>
                <p className="text-xs text-mist">{item.details}</p>
              </li>
            ))}
          </ul>
        </section>
      </div>
    </div>
  )
}
