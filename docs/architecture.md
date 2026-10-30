# Architecture

The portal is a monorepo with a React UI and an ASP.NET Core API. PostgreSQL stores users, synced incidents, the camera catalog, external-to-Genetec mappings, and audit records.

```
Incident source (mock file or REST)
        |
        v
Investigation API
        |
        +--> PostgreSQL
        |
        +--> Genetec adapter (Mock, Web SDK, or Platform SDK)
                |
                +--> Security Center Directory
                |
                +--> Media Gateway token endpoint
                        |
                        v
React GenetecVideoPlayer
        |
        +--> gwp.js and WebSocket on the Media Gateway
```

The API never receives the video elementary stream. `POST /api/playback/session` returns the camera GUID, the UTC window, and the Media Gateway endpoint the browser must use. The Web Player asks this API for a camera token, and the API calls `GET {mediaGateway}/v2/token/{cameraGuid}` with the server-side certificate.

Mock mode implements the same contracts and labels every camera and playback surface as mock.

Layers:

- `Investigation.Domain` — entities
- `Investigation.Application` — timestamp, recording, playback, and catalog rules
- `Investigation.Infrastructure` — EF Core, incident sources, startup seed
- `Investigation.Genetec` — mock adapter, Web SDK adapter, conditional Platform SDK adapter
- `Investigation.Api` — HTTP, JWT, audit entry points
