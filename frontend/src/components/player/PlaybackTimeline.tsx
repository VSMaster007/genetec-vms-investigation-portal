import type { TimeRange } from '../../types/models'
import { formatClock, fraction } from '../../utils/time'

export function PlaybackTimeline({
  rangeStartUtc,
  rangeEndUtc,
  windowStartUtc,
  windowEndUtc,
  eventUtc,
  playheadUtc,
  sequences,
  gaps,
  timeZone,
  disabled = false,
  onSeek
}: {
  rangeStartUtc?: string
  rangeEndUtc?: string
  windowStartUtc: string
  windowEndUtc: string
  eventUtc: string
  playheadUtc: string
  sequences: TimeRange[]
  gaps: TimeRange[]
  timeZone?: string
  disabled?: boolean
  onSeek: (isoUtc: string) => void
}) {
  const start = rangeStartUtc ?? windowStartUtc
  const end = rangeEndUtc ?? windowEndUtc
  const zone = timeZone ?? 'UTC'

  const seekAt = (clientX: number, width: number, left: number) => {
    const ratio = width <= 0 ? 0 : (clientX - left) / width
    const startMs = new Date(start).getTime()
    const endMs = new Date(end).getTime()
    onSeek(new Date(startMs + (endMs - startMs) * Math.min(1, Math.max(0, ratio))).toISOString())
  }

  return (
    <div className="space-y-2">
      <div className="flex justify-between gap-3 text-[11px] uppercase tracking-wide text-mist">
        <span>Start {formatClock(start, zone)}</span>
        <span>Incident {formatClock(eventUtc, zone)}</span>
        <span>End {formatClock(end, zone)}</span>
      </div>
      <div className="relative h-14">
        <button
          type="button"
          aria-label="Recording timeline"
          className="absolute inset-0 overflow-hidden rounded border border-line bg-ink text-left disabled:opacity-60"
          disabled={disabled}
          onClick={(event) => {
            const bounds = event.currentTarget.getBoundingClientRect()
            seekAt(event.clientX, bounds.width, bounds.left)
          }}
        >
          <span className="absolute inset-x-0 top-6 h-2 bg-panel2" />
          <span
            className="absolute top-6 h-2 bg-sky-900/80"
            style={{
              left: `${fraction(start, end, windowStartUtc) * 100}%`,
              width: `${Math.max(0.4, (fraction(start, end, windowEndUtc) - fraction(start, end, windowStartUtc)) * 100)}%`
            }}
          />
          {sequences.map((sequence) => (
            <span
              key={`${sequence.startUtc}-${sequence.endUtc}`}
              className="absolute top-6 h-2 bg-accent"
              style={{
                left: `${fraction(start, end, sequence.startUtc) * 100}%`,
                width: `${Math.max(0.4, (fraction(start, end, sequence.endUtc) - fraction(start, end, sequence.startUtc)) * 100)}%`
              }}
            />
          ))}
          {gaps.map((gap) => (
            <span
              key={`${gap.startUtc}-${gap.endUtc}`}
              className="absolute top-6 h-2 bg-amber-500/80"
              style={{
                left: `${fraction(start, end, gap.startUtc) * 100}%`,
                width: `${Math.max(0.4, (fraction(start, end, gap.endUtc) - fraction(start, end, gap.startUtc)) * 100)}%`
              }}
            />
          ))}
          <span
            className="absolute top-2 h-10 w-0.5 bg-paper"
            style={{ left: `${fraction(start, end, playheadUtc) * 100}%` }}
          />
        </button>
        <button
          type="button"
          aria-label="Seek to incident"
          disabled={disabled}
          className="absolute top-1 z-10 h-12 w-3 -translate-x-1/2 disabled:opacity-60"
          style={{ left: `${fraction(start, end, eventUtc) * 100}%` }}
          onClick={() => onSeek(eventUtc)}
        >
          <span className="mx-auto block h-full w-0.5 bg-rose-400" />
        </button>
      </div>
      <div className="flex flex-wrap gap-4 text-[11px] text-mist">
        <span className="inline-flex items-center gap-1.5"><span className="h-2 w-4 rounded-sm bg-sky-900/80" />Investigation window</span>
        <span className="inline-flex items-center gap-1.5"><span className="h-3 w-0.5 bg-rose-400" />Incident</span>
        <span className="inline-flex items-center gap-1.5"><span className="h-3 w-0.5 bg-paper" />Playhead</span>
      </div>
    </div>
  )
}
