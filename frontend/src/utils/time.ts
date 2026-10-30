export function formatInZone(isoUtc: string, timeZone: string): string {
  return new Intl.DateTimeFormat(undefined, {
    timeZone,
    year: 'numeric',
    month: 'short',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hourCycle: 'h23',
    timeZoneName: 'short'
  }).format(new Date(isoUtc))
}

export function formatDate(isoUtc: string, timeZone: string): string {
  return new Intl.DateTimeFormat('en-US', {
    timeZone,
    year: 'numeric',
    month: 'long',
    day: 'numeric'
  }).format(new Date(isoUtc))
}

export function formatDuration(seconds: number): string {
  const total = Math.max(0, Math.floor(seconds))
  const hours = Math.floor(total / 3600)
  const minutes = Math.floor((total % 3600) / 60)
  const remainder = total % 60
  const clock = `${minutes}:${String(remainder).padStart(2, '0')}`
  return hours > 0 ? `${hours}:${String(minutes).padStart(2, '0')}:${String(remainder).padStart(2, '0')}` : clock
}

export function formatClock(isoUtc: string, timeZone: string): string {
  return new Intl.DateTimeFormat(undefined, {
    timeZone,
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hourCycle: 'h23'
  }).format(new Date(isoUtc))
}

export function fraction(startIso: string, endIso: string, valueIso: string): number {
  const start = new Date(startIso).getTime()
  const end = new Date(endIso).getTime()
  const value = new Date(valueIso).getTime()
  if (end <= start) {
    return 0
  }
  return Math.min(1, Math.max(0, (value - start) / (end - start)))
}

export function timeAtFraction(startIso: string, endIso: string, ratio: number): string {
  const start = new Date(startIso).getTime()
  const end = new Date(endIso).getTime()
  return new Date(start + (end - start) * ratio).toISOString()
}
