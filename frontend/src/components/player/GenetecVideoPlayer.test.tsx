import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { GenetecVideoPlayer } from './GenetecVideoPlayer'
import { formatInZone } from '../../utils/time'
import { mapPlaybackWindow, mediaOffsetSeconds } from '../../utils/demoTime'
import type { PlaybackSession } from '../../types/models'

const session: PlaybackSession = {
  mode: 'Mock',
  provider: 'None',
  isGenetecRecording: false,
  canPlay: true,
  notice: 'Mock playback surface. The Genetec Web Player is not connected and this is not an archived recording.',
  incidentId: '8f1c2a40-6b1e-4a1a-9c1a-aaaaaaaaaaaa',
  externalIncidentId: 'INC-2026-001',
  title: 'Unauthorized entrance',
  externalCameraId: 'CAM-ENTRANCE-01',
  cameraName: 'Main Entrance Camera (mock)',
  cameraId: '8f1c2a40-6b1e-4a1a-9c1a-bbbbbbbbbbbb',
  genetecCameraGuid: '8f1c2a40-6b1e-4a1a-9c1a-111111111111',
  mediaGatewayEndpoint: null,
  gwpScriptUrl: null,
  tokenPath: null,
  seekUtc: '2026-10-30T02:15:20.000Z',
  windowStartUtc: '2026-10-30T02:14:20.000Z',
  windowEndUtc: '2026-10-30T02:16:20.000Z',
  preEventSeconds: 60,
  postEventSeconds: 60,
  availability: {
    state: 'Available',
    coversEvent: true,
    isMockArchive: true,
    message: 'Mock archive.',
    sequences: [{ startUtc: '2026-10-30T02:14:20.000Z', endUtc: '2026-10-30T02:16:20.000Z' }],
    gaps: []
  },
  demo: null
}

describe('time display', () => {
  it('renders a UTC timestamp in the selected zone', () => {
    expect(formatInZone('2026-10-15T14:35:20Z', 'UTC')).toContain('14:35:20')
  })
})

describe('demo timestamp mapping', () => {
  it('keeps the incident inside the media and clamps the window', () => {
    const start = '2026-10-30T02:12:20.000Z'
    const incident = '2026-10-30T02:15:20.000Z'
    const offset = mediaOffsetSeconds(start, incident)
    expect(offset).toBe(180)
    expect(mapPlaybackWindow(600, offset, 60, 60)).toEqual({
      playbackStartOffsetSeconds: 120,
      playbackEndOffsetSeconds: 240,
      seekOffsetSeconds: 180
    })
    expect(mapPlaybackWindow(100, 10, 60, 60)).toEqual({
      playbackStartOffsetSeconds: 0,
      playbackEndOffsetSeconds: 70,
      seekOffsetSeconds: 10
    })
    expect(mapPlaybackWindow(100, 101, 60, 60)).toBeNull()
  })
})

describe('GenetecVideoPlayer demo recording', () => {
  it('loads the local sample and labels it as demo footage', () => {
    render(<GenetecVideoPlayer session={{
      ...session,
      provider: 'DemoMp4',
      notice: 'DEMO MODE — sample video. This MP4 is not a Genetec archived recording.',
      demo: {
        mediaUrl: '/demo-media/bunny.mp4',
        sourceName: 'Big Buck Bunny Sample MP4',
        sourceUrl: 'https://docs.evostream.com/sample_content/assets/bunny.mp4',
        cameraName: 'Main Entrance Camera',
        externalCameraId: 'CAM-ENTRANCE-01',
        location: 'Main Entrance',
        cameraType: 'IP Camera',
        cameraStatus: 'Online',
        integrationMode: 'Demo',
        recordingStartUtc: '2026-10-30T02:12:20.000Z',
        recordingEndUtc: '2026-10-30T02:22:20.000Z',
        durationSeconds: 600,
        incidentOffsetSeconds: 180,
        playbackStartOffsetSeconds: 120,
        playbackEndOffsetSeconds: 240,
        seekOffsetSeconds: 180,
        playbackEngine: 'HTML5 Video'
      }
    }} timeZone="UTC" />)
    expect(screen.getByText('DEMO MODE — SAMPLE VIDEO. This file is Big Buck Bunny, not a Genetec archive.')).toBeInTheDocument()
    expect(screen.getByText('Main Entrance Camera')).toBeInTheDocument()
    const video = document.querySelector('video')
    expect(video?.getAttribute('src')).toBe('/demo-media/bunny.mp4')
  })
})

describe('GenetecVideoPlayer', () => {
  it('labels mock playback and pauses the local clock', async () => {
    render(<GenetecVideoPlayer session={session} timeZone="UTC" />)
    expect(screen.getByText(/not a Genetec recording/i)).toBeInTheDocument()
    const pause = screen.getByRole('button', { name: 'Pause' })
    await userEvent.click(pause)
    expect(pause).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Resume' })).toBeEnabled()
  })

  it('shows a recording error instead of a player when playback cannot start', () => {
    render(<GenetecVideoPlayer session={{ ...session, canPlay: false, notice: 'No archived recording covers this playback window.' }} timeZone="UTC" />)
    expect(screen.getByRole('alert')).toHaveTextContent(/No archived recording/)
    expect(screen.getByRole('button', { name: 'Resume' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Recording timeline' })).toBeDisabled()
  })
})
