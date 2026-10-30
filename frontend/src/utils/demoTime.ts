export interface PlaybackOffsets {
  playbackStartOffsetSeconds: number
  playbackEndOffsetSeconds: number
  seekOffsetSeconds: number
}

export function mediaOffsetSeconds(recordingStartUtc: string, instantUtc: string): number {
  return (new Date(instantUtc).getTime() - new Date(recordingStartUtc).getTime()) / 1000
}

export function utcAtOffset(recordingStartUtc: string, offsetSeconds: number): string {
  return new Date(new Date(recordingStartUtc).getTime() + offsetSeconds * 1000).toISOString()
}

export function mapPlaybackWindow(
  durationSeconds: number,
  incidentOffsetSeconds: number,
  preEventSeconds: number,
  postEventSeconds: number
): PlaybackOffsets | null {
  if (durationSeconds <= 0 || incidentOffsetSeconds < 0 || incidentOffsetSeconds > durationSeconds || preEventSeconds < 0 || postEventSeconds < 0) {
    return null
  }

  return {
    playbackStartOffsetSeconds: Math.max(0, incidentOffsetSeconds - preEventSeconds),
    playbackEndOffsetSeconds: Math.min(durationSeconds, incidentOffsetSeconds + postEventSeconds),
    seekOffsetSeconds: incidentOffsetSeconds
  }
}

export function clampOffset(offsetSeconds: number, durationSeconds: number): number {
  if (!Number.isFinite(offsetSeconds) || !Number.isFinite(durationSeconds) || durationSeconds <= 0) {
    return 0
  }
  return Math.min(durationSeconds, Math.max(0, offsetSeconds))
}
