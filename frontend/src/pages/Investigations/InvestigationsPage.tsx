import { useEffect, useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { IncidentFilters } from '../../components/incidents/IncidentFilters'
import { GenetecVideoPlayer } from '../../components/player/GenetecVideoPlayer'
import { usePreferences } from '../../hooks/usePreferences'
import { ApiError, api } from '../../services/api'
import type { IncidentPage, PlaybackSession } from '../../types/models'
import { formatInZone } from '../../utils/time'

export function InvestigationsPage() {
  const { incidentId } = useParams()
  const [params, setParams] = useSearchParams()
  const preferences = usePreferences()
  const [page, setPage] = useState<IncidentPage | null>(null)
  const [session, setSession] = useState<PlaybackSession | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [facets, setFacets] = useState<{ eventTypes: string[]; locations: string[] }>({ eventTypes: [], locations: [] })

  const filters = {
    incidentId: params.get('incidentId') ?? '',
    from: params.get('from') ?? '',
    to: params.get('to') ?? '',
    camera: params.get('camera') ?? '',
    eventType: params.get('eventType') ?? '',
    location: params.get('location') ?? ''
  }

  useEffect(() => {
    void api<{ eventTypes: string[]; locations: string[] }>('/api/incidents/facets').then(setFacets).catch(() => undefined)
  }, [])

  useEffect(() => {
    const query = new URLSearchParams()
    if (filters.incidentId) query.set('incidentId', filters.incidentId)
    if (filters.from) query.set('from', new Date(filters.from).toISOString())
    if (filters.to) query.set('to', new Date(filters.to).toISOString())
    if (filters.camera) query.set('camera', filters.camera)
    if (filters.eventType) query.set('eventType', filters.eventType)
    if (filters.location) query.set('location', filters.location)
    query.set('pageSize', '50')
    void api<IncidentPage>(`/api/incidents?${query.toString()}`).then(setPage).catch((caught: Error) => setError(caught.message))
  }, [filters.incidentId, filters.from, filters.to, filters.camera, filters.eventType, filters.location])

  useEffect(() => {
    if (!incidentId) {
      setSession(null)
      return
    }
    setError(null)
    void api<PlaybackSession>('/api/playback/session', {
      method: 'POST',
      body: JSON.stringify({
        incidentId,
        preEventSeconds: preferences.preEventSeconds,
        postEventSeconds: preferences.postEventSeconds
      })
    }).then(setSession).catch((caught: unknown) => {
      setSession(null)
      setError(caught instanceof ApiError ? caught.message : 'Playback could not be resolved.')
    })
  }, [incidentId, preferences.preEventSeconds, preferences.postEventSeconds])

  const update = (patch: Record<string, string>) => {
    const next = new URLSearchParams(params)
    Object.entries(patch).forEach(([key, value]) => {
      if (value) next.set(key, value)
      else next.delete(key)
    })
    setParams(next)
  }

  return (
    <div className={`grid h-[calc(100vh-4rem)] min-h-0 ${incidentId ? 'grid-cols-[280px_minmax(0,1fr)_320px]' : 'grid-cols-[280px_minmax(0,1fr)]'}`}>
      <aside className="overflow-auto border-r border-line p-4">
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-mist">Incidents</h2>
        <IncidentFilters {...filters} eventTypes={facets.eventTypes} locations={facets.locations} onChange={update} />
        <ul className="mt-4 space-y-2">
          {page?.items.map((incident) => (
            <li key={incident.id}>
              <Link to={`/investigations/${incident.id}?${params.toString()}`} className={`block rounded border px-3 py-2 text-sm ${incident.id === incidentId ? 'border-accent bg-panel2' : 'border-line'}`}>
                <span className="font-medium">{incident.externalIncidentId}</span>
                <span className="mt-1 block text-mist">{incident.title}</span>
              </Link>
            </li>
          ))}
        </ul>
      </aside>
      <section className="min-w-0 overflow-auto p-4">
        {incidentId && session ? (
          <GenetecVideoPlayer session={session} timeZone={preferences.timeZone} />
        ) : (
          <IncidentTable incidents={page?.items ?? []} timeZone={preferences.timeZone} query={params.toString()} />
        )}
        {error ? <p className="mt-4 rounded border border-rose-500/40 bg-rose-950/40 px-4 py-3 text-sm text-rose-100">{error}</p> : null}
      </section>
      {incidentId && session ? (
        <aside className="overflow-auto border-l border-line p-4 text-sm">
          <h2 className="text-lg font-semibold">{session.title}</h2>
          <dl className="mt-4 space-y-3">
            <Detail label="Incident" value={session.externalIncidentId} />
            <Detail label="Camera" value={session.cameraName ?? 'Unmapped'} />
            <Detail label="External camera" value={session.externalCameraId} />
            <Detail label="Genetec GUID" value={session.genetecCameraGuid ?? 'Not resolved'} />
            <Detail label="Event" value={formatInZone(session.seekUtc, preferences.timeZone)} />
            <Detail label="Window" value={`${formatInZone(session.windowStartUtc, preferences.timeZone)} – ${formatInZone(session.windowEndUtc, preferences.timeZone)}`} />
            <Detail label="Recording" value={`${session.availability.state}${session.availability.isMockArchive ? ' · mock archive' : ''}`} />
            <Detail label="Playback" value={session.isGenetecRecording ? 'Genetec Web Player' : 'Mock surface'} />
          </dl>
          <p className="mt-4 text-mist">{session.availability.message}</p>
        </aside>
      ) : null}
    </div>
  )
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs uppercase tracking-wide text-mist">{label}</dt>
      <dd className="mt-1 break-all">{value}</dd>
    </div>
  )
}

function IncidentTable({ incidents, timeZone, query }: { incidents: IncidentPage['items']; timeZone: string; query: string }) {
  return (
    <div className="overflow-auto rounded border border-line">
      <table className="min-w-full text-left text-sm">
        <thead className="bg-panel text-xs uppercase tracking-wide text-mist">
          <tr>
            {['Incident ID', 'Title', 'Event type', 'Camera', 'Location', 'Date', 'Timestamp', 'Recording', 'Action'].map((heading) => (
              <th key={heading} className="px-3 py-2 font-medium">{heading}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {incidents.map((incident) => (
            <tr key={incident.id} className="border-t border-line">
              <td className="px-3 py-2 font-mono text-xs">{incident.externalIncidentId}</td>
              <td className="px-3 py-2">{incident.title}</td>
              <td className="px-3 py-2">{incident.eventType}</td>
              <td className="px-3 py-2">{incident.cameraName ?? incident.externalCameraId}</td>
              <td className="px-3 py-2">{incident.location}</td>
              <td className="px-3 py-2">{formatInZone(incident.eventTimestampUtc, timeZone).split(',')[0]}</td>
              <td className="px-3 py-2 font-mono text-xs">{formatInZone(incident.eventTimestampUtc, timeZone)}</td>
              <td className="px-3 py-2">{incident.mappingStatus === 'Unmapped' ? 'Unmapped' : incident.recordingState}{incident.recordingIsMock ? ' (mock)' : ''}</td>
              <td className="px-3 py-2"><Link className="text-accent" to={`/investigations/${incident.id}?${query}`}>Open</Link></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
