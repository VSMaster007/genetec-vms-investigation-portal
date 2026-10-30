import { FormEvent, useEffect, useState } from 'react'
import { api } from '../../services/api'
import { useAuth } from '../../hooks/useAuth'
import { MappingStatus } from '../../components/cameras/MappingStatus'
import type { Camera, MappingConsistency } from '../../types/models'

export function CameraManagementPage() {
  const [cameras, setCameras] = useState<Camera[]>([])
  const [mappings, setMappings] = useState<MappingConsistency | null>(null)
  const [search, setSearch] = useState('')
  const [selected, setSelected] = useState<Camera | null>(null)
  const [externalId, setExternalId] = useState('')
  const [editing, setEditing] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const { user } = useAuth()
  const canMap = user?.role === 'Administrator'

  const load = async (term = search) => {
    const [catalog, consistency] = await Promise.all([
      api<Camera[]>(`/api/cameras?search=${encodeURIComponent(term)}`),
      api<MappingConsistency>('/api/cameras/mappings')
    ])
    setCameras(catalog)
    setMappings(consistency)
  }

  useEffect(() => {
    void load('').catch((caught: Error) => setError(caught.message))
  }, [])

  const save = async (event: FormEvent) => {
    event.preventDefault()
    if (!selected) {
      return
    }
    setError(null)
    try {
      if (editing) {
        await api(`/api/cameras/mappings/${editing}`, { method: 'PUT', body: JSON.stringify({ externalCameraId: externalId, cameraId: selected.id }) })
      } else {
        await api('/api/cameras/mappings', { method: 'POST', body: JSON.stringify({ externalCameraId: externalId, cameraId: selected.id }) })
      }
      setEditing(null)
      setExternalId('')
      setMessage('Mapping saved.')
      await load()
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Mapping was not saved.')
    }
  }

  return (
    <div className="grid gap-4 p-6 lg:grid-cols-[minmax(0,1fr)_340px]">
      <section>
        <div className="mb-4 flex gap-2">
          <input aria-label="Search cameras" value={search} onChange={(event) => setSearch(event.target.value)} className="flex-1 rounded border border-line bg-panel px-3 py-2 text-sm" placeholder="Search name, location, or GUID" />
          <button type="button" className="rounded border border-line px-3 py-2 text-sm" onClick={() => void load()}>Search</button>
          <button type="button" className="rounded bg-accent px-3 py-2 text-sm text-ink" onClick={() => void api<Camera[]>('/api/genetec/cameras').then(setCameras)}>Refresh from Genetec</button>
        </div>
        {error ? <p className="mb-3 text-sm text-rose-300">{error}</p> : null}
        {message ? <p className="mb-3 text-sm text-emerald-300">{message}</p> : null}
        <div className="overflow-auto rounded border border-line">
          <table className="min-w-full text-left text-sm">
            <thead className="bg-panel text-xs uppercase text-mist">
              <tr>{['Name', 'Genetec GUID', 'Location', 'Status', 'Mapping'].map((heading) => <th key={heading} className="px-3 py-2">{heading}</th>)}</tr>
            </thead>
            <tbody>
              {cameras.map((camera) => {
                const mapping = mappings?.mappings.find((item) => item.cameraId === camera.id)
                return (
                  <tr key={camera.id} className="cursor-pointer border-t border-line" onClick={() => setSelected(camera)}>
                    <td className="px-3 py-2">{camera.cameraName}{camera.isMock ? ' · mock' : ''}</td>
                    <td className="px-3 py-2 font-mono text-xs">{camera.genetecCameraGuid}</td>
                    <td className="px-3 py-2">{camera.location}</td>
                    <td className="px-3 py-2">{camera.status}</td>
                    <td className="px-3 py-2"><MappingStatus mapped={Boolean(mapping)} mock={camera.isMock} /></td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      </section>
      <aside className="rounded border border-line bg-panel p-4">
        <h2 className="text-lg font-semibold">Camera mapping</h2>
        {selected ? (
          <form onSubmit={save} className="mt-4 space-y-3 text-sm">
            <p>{selected.cameraName}</p>
            <p className="break-all font-mono text-xs text-mist">{selected.genetecCameraGuid}</p>
            <label className="block">
              External camera ID
              <input aria-label="External camera ID" className="mt-1 w-full rounded border border-line bg-ink px-2 py-1.5" value={externalId} onChange={(event) => setExternalId(event.target.value)} required />
            </label>
            <button type="submit" disabled={!canMap} className="rounded bg-accent px-3 py-2 text-ink">{editing ? 'Update mapping' : 'Create mapping'}</button>
            {!canMap ? <p className="text-xs text-mist">Only an administrator can change mappings.</p> : null}
          </form>
        ) : <p className="mt-3 text-sm text-mist">Select a camera to map an external ID.</p>}
        <h3 className="mt-6 text-sm font-semibold">Existing mappings</h3>
        <ul className="mt-2 space-y-2 text-sm">
          {mappings?.mappings.map((mapping) => (
            <li key={mapping.id} className="rounded border border-line p-2">
              <p>{mapping.externalCameraId}</p>
              <p className="text-xs text-mist">{mapping.cameraName}</p>
              <div className="mt-2 flex gap-2">
                <button type="button" className="text-xs text-accent disabled:text-mist" disabled={!canMap} onClick={() => { setEditing(mapping.id); setExternalId(mapping.externalCameraId); setSelected(cameras.find((camera) => camera.id === mapping.cameraId) ?? null) }}>Edit</button>
                <button type="button" className="text-xs text-rose-300" disabled={!canMap} onClick={() => void api(`/api/cameras/mappings/${mapping.id}`, { method: 'DELETE' }).then(() => load())}>Remove</button>
              </div>
            </li>
          ))}
        </ul>
        {mappings && mappings.issues.length > 0 ? (
          <ul className="mt-4 space-y-1 text-xs text-amber-200">
            {mappings.issues.map((issue) => <li key={issue}>{issue}</li>)}
          </ul>
        ) : null}
      </aside>
    </div>
  )
}
