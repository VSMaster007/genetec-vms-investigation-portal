# Demo recording guide

This guide is for a 2–3 minute screen recording of the investigation workflow. Demo mode plays a local sample MP4. It does not contact Genetec, and the picture is not an archived Security Center recording.

The sample is [Big Buck Bunny](https://docs.evostream.com/sample_content/assets/bunny.mp4), saved unchanged at `frontend/public/demo-media/bunny.mp4`. Measured with ffprobe: H.264 Constrained Baseline, 424×240, 24 fps, AAC-LC, duration 596.458333 seconds (9:56). Playback uses that local file. The browser does not request the original URL.

Camera, incident, and timestamp values are in `docs/demo-scenario.json`. The API reads the same numbers from `DemoMedia` in `backend/src/Investigation.Api/appsettings.json`.

## 1. Start the application

Requirements: .NET 8 SDK, Node.js 22, and Docker.

```bash
docker compose up -d postgres
cd backend
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Investigation.Api
```

In a second terminal:

```bash
cd frontend
npm install
npm run dev
```

Open http://localhost:5173 and sign in as `investigator@investigation.local` / `ChangeMe-Investigator-123`.

On this machine the SDKs are installed under the user profile. If `dotnet` or `npm` is not on `PATH`:

```bash
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$HOME/.local/node/bin:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"
```

If `npm install` stalls, retry with `NODE_OPTIONS="--dns-result-order=ipv4first --no-network-family-autoselection"`.

## 2. Activate demo mode

Demo playback is on when both of these are true:

- `Genetec:Mode` is `Mock` (the default).
- `DemoMedia:Enabled` is `true`.

The header shows **Demo mode**. That label means the directory is simulated. It does not mean Genetec is connected. System Status keeps the directory state at Mock.

Restart the API after changing `appsettings.json` so the new recording duration is loaded. The incident title is refreshed from `database/seed/incidents.json` on startup.

## 3. Select the sample incident

1. Open **Investigations**.
2. In the left list, select **INC-2026-001**, Unauthorized Entrance, Motion Detection, Main Entrance, October 30, 2026.
3. Search, date, and event-type filters narrow the same list. Clearing them shows the other sample incidents, which demonstrate unmapped cameras, retention, permission, and recording gaps. Those incidents do not play the sample MP4.

## 4. How timestamp mapping works

Times are stored in UTC. The screen prints them in the investigator's selected time zone.

The entrance incident is fixed at `2026-10-30T02:15:20Z`. The sample recording is anchored so that instant is 180 seconds into the file:

| Point | UTC | Media offset |
| --- | --- | --- |
| Recording start | 2026-10-30T02:12:20Z | 0 s |
| Playback window start | 2026-10-30T02:14:20Z | 120 s |
| Incident | 2026-10-30T02:15:20Z | 180 s |
| Playback window end | 2026-10-30T02:16:20Z | 240 s |
| Recording end | 2026-10-30T02:22:16.458333Z | 596.458 s |

```
mediaOffsetSeconds = incidentTimestampUtc - recordingStartTimestampUtc
playbackStartOffset = max(0, incidentOffsetSeconds - preEventSeconds)
playbackEndOffset = min(videoDurationSeconds, incidentOffsetSeconds + postEventSeconds)
```

The default investigation window is 60 seconds before and after the incident. Both edges sit inside the measured duration, so nothing is clamped for this incident. An incident time outside the recording does not receive the sample file.

The player converts the video element's `currentTime` back to UTC with the recording start, so the clock and the timeline follow the file rather than a separate timer.

## 5. Demonstrate playback

After INC-2026-001 is selected, the portal:

1. Loads the incident.
2. Resolves `CAM-ENTRANCE-01` to the mapped camera.
3. Attaches the sample recording metadata.
4. Seeks the HTML5 player to 180 seconds, the incident offset.
5. Starts playback.

Show, in order:

- The center player titled **Main Entrance Camera** / `CAM-ENTRANCE-01`.
- The banner **DEMO MODE — SAMPLE VIDEO**.
- Pause, then Resume.
- The incident marker on the timeline. Clicking it returns to 02:15:20 UTC (180 seconds).
- A click elsewhere on the timeline, which seeks to that recording time.
- Playback speed.
- The right panel: incident id, Motion Detection, Main Entrance Camera, the UTC incident time in local form, Sample recording available, Ready, and Local MP4 demo.

The right panel also lists the video source, simulated camera recording, and HTML5 Video. Say explicitly that the picture is Big Buck Bunny used as sample footage.

## 6. Switch between demo and real Genetec mode

Demo MP4 is attached only for a mock endpoint. Real mode never receives `demo` metadata, a media URL, or the HTML5 player.

To leave the sample:

- Set `Genetec:Mode` to `Real` and `Genetec:RealTransport` to `WebSdk`.
- Supply the Web SDK and Media Gateway settings from `.env.example`.
- Restart the API.

Real playback loads `gwp.js` from the Media Gateway. The player shows a Genetec connection only after that path is actually available. To keep mock cameras but hide the sample, set `DemoMedia:Enabled` to `false` and restart. The entrance incident then uses the mock clock, still labeled as not a Genetec recording.

## 7. Known limitations of the sample MP4

- It is the EvoStream Big Buck Bunny sample, not camera video and not a Genetec archive.
- Picture size is 424×240. The soundtrack is the film mix.
- The container brand is QuickTime (`qt`). The streams are H.264 and AAC, which current Chrome, Edge, and Firefox play when the file is served as `video/mp4`.
- Only `CAM-ENTRANCE-01` incidents whose timestamps fall inside `2026-10-30T02:12:20Z`–`2026-10-30T02:22:16.458333Z` use the file. `INC-2026-006` stays a recording gap. `INC-2026-010` is outside the sample and stays on the mock clock.
- Some browsers block unmuted autoplay. The player then starts muted; **Unmute** turns sound on.
- Scrubbing is file-accurate. It is not a Genetec timeline, and there is no export, PTZ, or multi-camera sync.
