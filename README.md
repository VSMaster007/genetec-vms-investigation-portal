# Genetec VMS Investigation Portal

Browser-based proof of concept that connects timestamped incident records to archived Genetec Security Center video.

An investigator searches incidents, and the portal resolves the external camera ID to a Genetec camera, checks the archived window, and opens playback at the incident time. Video is played by the Genetec Web Player against the Media Gateway. The API does not proxy video files.

## What is real, and what is mock

`Genetec:Mode=Mock` is the default. It uses a synthetic camera catalog and a clearly labeled playback surface. It does not contact Security Center and it does not present mock video as a Genetec recording.

`Genetec:Mode=Real` with `Genetec:RealTransport=WebSdk` calls the official Web-based SDK for directory authentication, camera discovery, and `VideoSequence` availability. Browser playback loads `gwp.js` from the Media Gateway and requests tokens through this API so the SDK certificate and password stay on the server.

`Genetec:RealTransport=Platform` uses `Genetec.Sdk.Engine.LogOnAsync` when the build machine is Windows and `GSC_SDK_CORE` points at an installed Security Center SDK. That SDK is not a browser player. Playback still uses the Web Player. This repository was built on Linux, where the Platform SDK assemblies are not installed, so that transport reports itself as unavailable instead of pretending to connect.

## Local mock demo

Requirements: .NET 8 SDK, Node.js 22, and Docker for PostgreSQL.

```bash
docker compose up -d postgres
cd backend
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Investigation.Api
```

In another terminal:

```bash
cd frontend
npm install
npm run dev
```

Open http://localhost:5173.

Development sign-in, also listed in `.env.example`:

- `investigator@investigation.local` / `ChangeMe-Investigator-123`
- `admin@investigation.local` / `ChangeMe-Admin-123`

The administrator can change camera mappings. Sample incidents are loaded from `database/seed/incidents.json` on startup. `INC-2026-001` is the happy path in mock mode. `INC-2026-004` is past retention, `INC-2026-005` is denied, `INC-2026-006` falls in a recording gap, and `INC-2026-007` has no mapping.

## Full stack with Docker

```bash
docker compose up --build
```

The UI is published at http://localhost:8088 and the API at http://localhost:8080.

## Real Genetec connection

Copy `.env.example` to `.env` and set:

- `GENETEC_MODE=Real`
- `GENETEC_REAL_TRANSPORT=WebSdk`
- Web SDK base URL, normally `https://<directory>:4590/WebSdk/`
- A local Security Center user with **Log on using the SDK**
- The SDK certificate application ID
- Media Gateway server URL for token requests and the browser-reachable URL, normally `https://<gateway>/media`

The browser must be able to open the Media Gateway WebSocket. Enable **Web App** plus **View playback** on the integration user. Configure the Media Gateway role for the Web Player, including a trusted certificate. Do not put the certificate or password in frontend source.

Platform SDK requirements, verified from Genetec's DAP documentation:

- Security Center SDK 5.12.2 or later for `net8.0-windows` assemblies
- `GSC_SDK_CORE` set to that folder
- Application target `net8.0-windows` when those assemblies are referenced
- Certificate file beside the executable, or `Engine.ClientCertificate`
- Outbound TCP 5500 to the Directory

See `docs/genetec-integration.md` for the calls this portal actually uses.

## Tests

```bash
cd backend && dotnet test
cd frontend && npm test
```

## Documentation

- `docs/architecture.md`
- `docs/setup.md`
- `docs/genetec-integration.md`
- `docs/api-documentation.md`
- `docs/poc-demonstration.md`
