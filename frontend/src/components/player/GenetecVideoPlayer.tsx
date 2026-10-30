import { useEffect, useRef, useState } from 'react'
import type { PlaybackSession } from '../../types/models'
import { formatClock } from '../../utils/time'
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
  const [playhead, setPlayhead] = useState(session.seekUtc)
  const [playing, setPlaying] = useState(session.canPlay && session.provider !== 'GenetecWebPlayer')
  const [speed, setSpeed] = useState(1)
  const [loading, setLoading] = useState(session.provider === 'GenetecWebPlayer' && session.canPlay)
  const [error, setError] = useState<string | null>(session.canPlay ? null : session.notice)
  const [version, setVersion] = useState<string | null>(null)

  useEffect(() => {
    setPlayhead(session.seekUtc)
    setError(session.canPlay ? null : session.notice)
    setPlaying(session.canPlay && session.provider !== 'GenetecWebPlayer')
    setLoading(session.provider === 'GenetecWebPlayer' && session.canPlay)
  }, [session])

  useEffect(() => {
    if (!session.canPlay || session.provider === 'GenetecWebPlayer' || !playing) {
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
    setPlayhead(isoUtc)
    playerRef.current?.seek(new Date(isoUtc))
  }

  const pause = () => {
    playerRef.current?.pause()
    setPlaying(false)
  }

  const resume = () => {
    playerRef.current?.resume()
    setPlaying(true)
  }

  const mockMode = session.mode === 'Mock' || !session.isGenetecRecording

  return (
    <section className="flex h-full min-h-0 flex-col gap-3">
      <div className="relative min-h-[280px] flex-1 overflow-hidden rounded border border-line bg-black">
        {session.provider === 'GenetecWebPlayer' && session.canPlay ? <div ref={containerRef} className="h-full w-full" /> : null}
        {mockMode ? (
          <div className="flex h-full flex-col items-center justify-center gap-3 px-8 text-center">
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-amber-300">Mock playback — not a Genetec recording</p>
            {session.canPlay ? <p className="max-w-xl text-sm text-mist">{session.notice}</p> : null}
            <p className="font-mono text-2xl text-paper">{formatClock(playhead, timeZone)}</p>
            <p className="text-sm text-mist">{session.cameraName ?? 'Camera not resolved'}</p>
          </div>
        ) : null}
        {loading ? <div className="absolute inset-0 flex items-center justify-center bg-black/70 text-sm">Connecting to the Media Gateway…</div> : null}
        {error ? <div role="alert" className="absolute inset-x-4 bottom-4 rounded border border-rose-500/50 bg-rose-950/90 px-4 py-3 text-sm text-rose-100">{error}</div> : null}
      </div>
      <div className="flex items-center justify-between gap-3 text-sm">
        <div>
          <p className="font-medium">{session.cameraName ?? 'No camera'}</p>
          <p className="font-mono text-xs text-mist">{formatClock(playhead, timeZone)}{version ? ` · GWP ${version}` : ''}</p>
        </div>
        <div className="flex items-center gap-2">
          <button type="button" className="rounded border border-line px-3 py-1.5" disabled={!session.canPlay || !playing} onClick={pause}>Pause</button>
          <button type="button" className="rounded bg-accent px-3 py-1.5 text-ink" disabled={!session.canPlay || playing} onClick={resume}>Resume</button>
          <button type="button" className="rounded border border-line px-3 py-1.5" disabled={!session.canPlay} onClick={() => seek(session.seekUtc)}>Incident</button>
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
        windowStartUtc={session.windowStartUtc}
        windowEndUtc={session.windowEndUtc}
        eventUtc={session.seekUtc}
        playheadUtc={playhead}
        sequences={session.availability.sequences}
        gaps={session.availability.gaps}
        disabled={!session.canPlay}
        onSeek={seek}
      />
    </section>
  )
}
