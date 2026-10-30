import { useEffect, useRef, useState } from 'react'
import type { PlaybackSession } from '../../types/models'
import { clampOffset, utcAtOffset } from '../../utils/demoTime'
import { formatClock, formatDuration } from '../../utils/time'
import { createTokenRetriever, loadGwp, type GwpPlayer } from '../../services/webPlayer'
import { PlaybackTimeline } from './PlaybackTimeline'

export function GenetecVideoPlayer({
  session,
  timeZone
}: {
  session: PlaybackSession
  timeZone: string
}) {
  const containerRef = useRef<HTMLDivElement>(null)
  const playerRef = useRef<GwpPlayer | null>(null)
  const videoRef = useRef<HTMLVideoElement>(null)
  const demo = session.provider === 'DemoMp4' ? session.demo : null
  const [playhead, setPlayhead] = useState(session.seekUtc)
  const [playing, setPlaying] = useState(session.canPlay && session.provider === 'None')
  const [speed, setSpeed] = useState(1)
  const [loading, setLoading] = useState(session.canPlay && session.provider !== 'None')
  const [error, setError] = useState<string | null>(session.canPlay ? null : session.notice)
  const [version, setVersion] = useState<string | null>(null)
  const [muted, setMuted] = useState(false)

  useEffect(() => {
    setPlayhead(session.seekUtc)
    setError(session.canPlay ? null : session.notice)
    setPlaying(session.canPlay && session.provider === 'None')
    setLoading(session.canPlay && session.provider !== 'None')
    setSpeed(1)
  }, [session])

  useEffect(() => {
    if (!session.canPlay || session.provider !== 'None' || !playing) {
      return
    }

    const timer = window.setInterval(() => {
      setPlayhead((current) => {
        const next = new Date(current).getTime() + 250 * speed
        const end = new Date(session.windowEndUtc).getTime()
        return new Date(Math.min(next, end)).toISOString()
      })
    }, 250)
    return () => window.clearInterval(timer)
  }, [playing, session, speed])

  useEffect(() => {
    if (!demo || !session.canPlay) {
      return
    }

    const video = videoRef.current
    if (!video) {
      return
    }

    if (video.getAttribute('src') !== demo.mediaUrl) {
      video.src = demo.mediaUrl
    }

    const place = () => {
      const offset = clampOffset(demo.seekOffsetSeconds, demo.durationSeconds)
      if (Math.abs(video.currentTime - offset) > 0.25) {
        video.currentTime = offset
      }
      video.playbackRate = speed
    }
    const onReady = () => {
      place()
      setLoading(false)
      void video.play().catch(() => {
        video.muted = true
        setMuted(true)
        void video.play().catch(() => {
          setPlaying(false)
          setLoading(false)
        })
      })
    }
    const onTime = () => {
      const offset = clampOffset(video.currentTime, demo.durationSeconds)
      setPlayhead(utcAtOffset(demo.recordingStartUtc, offset))
      setLoading(false)
    }
    const onPlay = () => {
      setPlaying(true)
      setLoading(false)
    }
    const onPause = () => setPlaying(false)
    const onWaiting = () => setLoading(true)
    const onFailure = () => {
      setLoading(false)
      setPlaying(false)
      setError('The sample recording could not be played from local storage.')
    }

    video.addEventListener('loadedmetadata', onReady)
    video.addEventListener('timeupdate', onTime)
    video.addEventListener('play', onPlay)
    video.addEventListener('pause', onPause)
    video.addEventListener('waiting', onWaiting)
    video.addEventListener('error', onFailure)
    if (video.readyState >= 1) {
      onReady()
    }

    return () => {
      video.removeEventListener('loadedmetadata', onReady)
      video.removeEventListener('timeupdate', onTime)
      video.removeEventListener('play', onPlay)
      video.removeEventListener('pause', onPause)
      video.removeEventListener('waiting', onWaiting)
      video.removeEventListener('error', onFailure)
      video.pause()
      video.removeAttribute('src')
      video.load()
    }
  }, [demo, session.canPlay, session.incidentId])

  useEffect(() => {
    if (videoRef.current && demo) {
      videoRef.current.playbackRate = speed
    }
  }, [demo, speed])

  useEffect(() => {
    if (!session.canPlay || session.provider !== 'GenetecWebPlayer' || !session.gwpScriptUrl || !session.mediaGatewayEndpoint || !session.genetecCameraGuid || !session.tokenPath) {
      return
    }

    const container = containerRef.current
    if (!container) {
      return
    }

    let disposed = false
    const onError = (failure: { errorCode: number; value: string }) => {
      setError(`Playback error ${failure.errorCode}: ${failure.value}`)
      setLoading(false)
    }
    const onFrame = (frame: Date) => {
      setPlayhead(frame.toISOString())
      setLoading(false)
    }

    const start = async () => {
      try {
        await loadGwp(session.gwpScriptUrl!)
        if (disposed || !window.gwp || !containerRef.current) {
          throw new Error('The Genetec Web Player did not initialize.')
        }
        setVersion(window.gwp.version())
        const player = window.gwp.buildPlayer(containerRef.current)
        playerRef.current = player
        player.onErrorStateRaised.register(onError)
        player.onFrameRendered.register(onFrame)
        const tokenRetriever = await createTokenRetriever(session.tokenPath!)
        await player.start(session.genetecCameraGuid!, session.mediaGatewayEndpoint!, tokenRetriever)
        player.timelineProvider.setTimelineRange(new Date(session.windowStartUtc), new Date(session.windowEndUtc))
        player.seek(new Date(session.seekUtc))
        if (!disposed) {
          setLoading(false)
          setPlaying(true)
        }
      } catch (caught) {
        if (!disposed) {
          setLoading(false)
          setError(caught instanceof Error ? caught.message : 'Playback could not start.')
        }
      }
    }

    void start()
    return () => {
      disposed = true
      const player = playerRef.current
      playerRef.current = null
      if (player) {
        player.onErrorStateRaised.unregister(onError)
        player.onFrameRendered.unregister(onFrame)
        player.dispose()
      }
    }
  }, [session])

  const seek = (isoUtc: string) => {
    if (demo) {
      const offset = clampOffset((new Date(isoUtc).getTime() - new Date(demo.recordingStartUtc).getTime()) / 1000, demo.durationSeconds)
      if (videoRef.current) {
        videoRef.current.currentTime = offset
      }
      setPlayhead(utcAtOffset(demo.recordingStartUtc, offset))
      return
    }

    setPlayhead(isoUtc)
    playerRef.current?.seek(new Date(isoUtc))
  }

  const pause = () => {
    videoRef.current?.pause()
    playerRef.current?.pause()
    setPlaying(false)
  }

  const resume = () => {
    if (videoRef.current) {
      void videoRef.current.play()
    }
    playerRef.current?.resume()
    if (!demo) {
      setPlaying(true)
    }
  }

  const mockClock = session.provider === 'None'
  const cameraLabel = demo?.cameraName ?? session.cameraName ?? 'No camera'
  const externalId = demo?.externalCameraId ?? session.externalCameraId

  return (
    <section className="flex h-full min-h-0 flex-col gap-3">
      <div className="flex items-center justify-between gap-3">
        <div>
          <p className="text-base font-semibold">{cameraLabel}</p>
          <p className="font-mono text-xs text-mist">{externalId}</p>
          {demo ? <p className="text-xs text-mist">{demo.cameraType} · {demo.cameraStatus} · {demo.location}</p> : null}
        </div>
        {demo ? <span className="rounded border border-amber-400/40 bg-amber-400/10 px-2 py-1 text-[11px] font-semibold uppercase tracking-[0.16em] text-amber-200">Demo mode</span> : null}
      </div>
      <div className="relative min-h-[360px] flex-1 overflow-hidden rounded border border-line bg-black">
        {session.provider === 'GenetecWebPlayer' && session.canPlay ? <div ref={containerRef} className="h-full w-full" /> : null}
        {demo && session.canPlay ? (
          <video ref={videoRef} className="h-full w-full bg-black object-contain" src={demo.mediaUrl} muted={muted} preload="auto" playsInline />
        ) : null}
        {demo && session.canPlay ? (
          <p className="pointer-events-none absolute left-3 top-3 rounded bg-black/75 px-3 py-1.5 font-mono text-sm tracking-wide text-paper">
            {formatClock(playhead, 'UTC')} UTC
          </p>
        ) : null}
        {mockClock ? (
          <div className="flex h-full flex-col items-center justify-center gap-3 px-8 text-center">
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-amber-300">Mock playback — not a Genetec recording</p>
            {session.canPlay ? <p className="max-w-xl text-sm text-mist">{session.notice}</p> : null}
            <p className="font-mono text-2xl text-paper">{formatClock(playhead, timeZone)}</p>
            <p className="text-sm text-mist">{session.cameraName ?? 'Camera not resolved'}</p>
          </div>
        ) : null}
        {loading ? <div className="absolute inset-0 flex items-center justify-center bg-black/70 text-sm">Loading recording…</div> : null}
        {error ? <div role="alert" className="absolute inset-x-4 bottom-4 rounded border border-rose-500/50 bg-rose-950/90 px-4 py-3 text-sm text-rose-100">{error}</div> : null}
      </div>
      {demo ? (
        <p className="text-xs text-amber-200">DEMO MODE — SAMPLE VIDEO. This file is Big Buck Bunny, not a Genetec archive.</p>
      ) : null}
      <div className="flex flex-wrap items-center justify-between gap-3 text-sm">
        <div>
          <p className="font-mono text-xs text-mist">
            {formatClock(playhead, timeZone)}
            {demo ? ` / ${formatDuration(demo.durationSeconds)}` : ''}
            {version ? ` · GWP ${version}` : ''}
          </p>
          {demo ? (
            <p className="text-xs text-mist">
              Recording {formatClock(demo.recordingStartUtc, timeZone)} – {formatClock(demo.recordingEndUtc, timeZone)}
              {' · '}Incident {formatClock(session.seekUtc, timeZone)}
            </p>
          ) : null}
        </div>
        <div className="flex items-center gap-2">
          <button type="button" className="rounded border border-line px-3 py-1.5" disabled={!session.canPlay || !playing} onClick={pause}>Pause</button>
          <button type="button" className="rounded bg-accent px-3 py-1.5 text-ink" disabled={!session.canPlay || playing} onClick={resume}>Resume</button>
          <button type="button" className="rounded border border-line px-3 py-1.5" disabled={!session.canPlay} onClick={() => seek(session.seekUtc)}>Incident</button>
          {demo ? (
            <button
              type="button"
              className="rounded border border-line px-3 py-1.5"
              onClick={() => {
                const next = !muted
                setMuted(next)
                if (videoRef.current) {
                  videoRef.current.muted = next
                }
              }}
            >
              {muted ? 'Unmute' : 'Mute'}
            </button>
          ) : null}
          <label className="text-xs text-mist">
            Speed
            <select
              aria-label="Playback speed"
              className="ml-2 rounded border border-line bg-panel px-2 py-1"
              value={speed}
              disabled={!session.canPlay}
              onChange={(event) => {
                const next = Number(event.target.value)
                setSpeed(next)
                playerRef.current?.setPlaySpeed(next)
                if (videoRef.current) {
                  videoRef.current.playbackRate = next
                }
              }}
            >
              <option value={0.5}>0.5x</option>
              <option value={1}>1x</option>
              <option value={2}>2x</option>
              <option value={4}>4x</option>
            </select>
          </label>
        </div>
      </div>
      <PlaybackTimeline
        rangeStartUtc={demo?.recordingStartUtc}
        rangeEndUtc={demo?.recordingEndUtc}
        windowStartUtc={session.windowStartUtc}
        windowEndUtc={session.windowEndUtc}
        eventUtc={session.seekUtc}
        playheadUtc={playhead}
        sequences={demo ? [] : session.availability.sequences}
        gaps={session.availability.gaps}
        timeZone={timeZone}
        disabled={!session.canPlay}
        onSeek={seek}
      />
    </section>
  )
}
