import type { TimeRange } from '../../types/models'
import { fraction } from '../../utils/time'

export function PlaybackTimeline({
  windowStartUtc,
  windowEndUtc,
  eventUtc,
  playheadUtc,
  sequences,
  gaps,
  disabled = false,
  onSeek
}: {
  windowStartUtc: string
  windowEndUtc: string
  eventUtc: string
  playheadUtc: string
  sequences: TimeRange[]
  gaps: TimeRange[]
  disabled?: boolean
  onSeek: (isoUtc: string) => void
}) {
  return (
    <button
      type="button"
      aria-label="Recording timeline"
      className="relative h-14 w-full overflow-hidden rounded border border-line bg-ink text-left disabled:opacity-60"
      disabled={disabled}
      onClick={(event) => {
        const bounds = event.currentTarget.getBoundingClientRect()
        const ratio = (event.clientX - bounds.left) / bounds.width
        const start = new Date(windowStartUtc).getTime()
        const end = new Date(windowEndUtc).getTime()
        onSeek(new Date(start + (end - start) * Math.min(1, Math.max(0, ratio))).toISOString())
      }}
    >
      <span className="absolute inset-x-0 top-6 h-2 bg-panel2" />
      {sequences.map((sequence) => (
        <span
          key={`${sequence.startUtc}-${sequence.endUtc}`}
          className="absolute top-6 h-2 bg-accent"
          style={{
            left: `${fraction(windowStartUtc, windowEndUtc, sequence.startUtc) * 100}%`,
            width: `${Math.max(0.4, (fraction(windowStartUtc, windowEndUtc, sequence.endUtc) - fraction(windowStartUtc, windowEndUtc, sequence.startUtc)) * 100)}%`
          }}
        />
      ))}
      {gaps.map((gap) => (
        <span
          key={`${gap.startUtc}-${gap.endUtc}`}
          className="absolute top-6 h-2 bg-amber-500/80"
          style={{
            left: `${fraction(windowStartUtc, windowEndUtc, gap.startUtc) * 100}%`,
            width: `${Math.max(0.4, (fraction(windowStartUtc, windowEndUtc, gap.endUtc) - fraction(windowStartUtc, windowEndUtc, gap.startUtc)) * 100)}%`
          }}
        />
      ))}
      <span
        className="absolute top-3 h-8 w-px bg-rose-400"
        style={{ left: `${fraction(windowStartUtc, windowEndUtc, eventUtc) * 100}%` }}
      />
      <span
        className="absolute top-2 h-10 w-0.5 bg-paper"
        style={{ left: `${fraction(windowStartUtc, windowEndUtc, playheadUtc) * 100}%` }}
      />
    </button>
  )
}
