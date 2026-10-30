import { api } from './api'

interface LiteEvent<T> {
  register(handler: (data: T) => void): void
  unregister(handler: (data: T) => void): void
}

export interface GwpPlayer {
  start(camera: string, mediaGatewayEndpoint: string, tokenRetriever: (cameraId: string) => Promise<string>): Promise<void>
  seek(seekTime: Date): void
  pause(): void
  resume(): void
  stop(): void
  dispose(): void
  setPlaySpeed(playSpeed: number): void
  isPaused: boolean
  lastFrameTime: Date
  timelineProvider: {
    setTimelineRange(start: Date, end: Date): void
    onTimelineContentUpdated: LiteEvent<{ events: Array<{ kind: number; time: Date; duration: number }> }>
  }
  onErrorStateRaised: LiteEvent<{ errorCode: number; value: string; isFatal: boolean }>
  onFrameRendered: LiteEvent<Date>
}

declare global {
  interface Window {
    gwp?: {
      buildPlayer(container: HTMLElement): GwpPlayer
      version(): string
    }
  }
}

const loaded = new Map<string, Promise<void>>()

export function loadGwp(scriptUrl: string): Promise<void> {
  const existing = loaded.get(scriptUrl)
  if (existing) {
    return existing
  }

  const promise = new Promise<void>((resolve, reject) => {
    const script = document.createElement('script')
    script.src = scriptUrl
    script.async = true
    script.onload = () => resolve()
    script.onerror = () => {
      script.remove()
      loaded.delete(scriptUrl)
      reject(new Error('The Genetec Web Player script could not be loaded from the Media Gateway.'))
    }
    document.body.appendChild(script)
  })
  loaded.set(scriptUrl, promise)
  return promise
}

export async function createTokenRetriever(tokenPath: string): Promise<(cameraId: string) => Promise<string>> {
  return async (cameraId: string) => {
    const target = tokenPath.includes(cameraId) ? tokenPath : `/api/playback/token/${cameraId}`
    const payload = await api<{ token: string }>(target, { method: 'POST' })
    return payload.token
  }
}
