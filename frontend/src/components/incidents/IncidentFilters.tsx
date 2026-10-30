export function IncidentFilters({
  incidentId,
  from,
  to,
  camera,
  eventType,
  location,
  eventTypes,
  locations,
  onChange
}: {
  incidentId: string
  from: string
  to: string
  camera: string
  eventType: string
  location: string
  eventTypes: string[]
  locations: string[]
  onChange: (patch: Record<string, string>) => void
}) {
  const field = 'w-full rounded border border-line bg-ink px-2 py-1.5 text-sm'
  return (
    <div className="grid gap-2">
      <input aria-label="Incident ID" className={field} placeholder="Incident ID" value={incidentId} onChange={(event) => onChange({ incidentId: event.target.value })} />
      <input aria-label="From" className={field} type="datetime-local" value={from} onChange={(event) => onChange({ from: event.target.value })} />
      <input aria-label="To" className={field} type="datetime-local" value={to} onChange={(event) => onChange({ to: event.target.value })} />
      <input aria-label="Camera" className={field} placeholder="Camera" value={camera} onChange={(event) => onChange({ camera: event.target.value })} />
      <select aria-label="Event type" className={field} value={eventType} onChange={(event) => onChange({ eventType: event.target.value })}>
        <option value="">All event types</option>
        {eventTypes.map((value) => <option key={value}>{value}</option>)}
      </select>
      <select aria-label="Location" className={field} value={location} onChange={(event) => onChange({ location: event.target.value })}>
        <option value="">All locations</option>
        {locations.map((value) => <option key={value}>{value}</option>)}
      </select>
    </div>
  )
}
