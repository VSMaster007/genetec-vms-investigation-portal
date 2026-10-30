# HTTP API

All routes except `POST /api/auth/login` and `GET /health` require a bearer token. Roles are `Administrator` and `Investigator`. Mapping changes are administrator-only.

Timestamps in requests must include `Z` or a numeric offset. Stored values are UTC.

## Auth

`POST /api/auth/login`

```json
{ "email": "investigator@investigation.local", "password": "secret" }
```

Returns `{ token, expiresAtUtc, user }`.

`GET /api/auth/me`

## Incidents

`GET /api/incidents?incidentId&from&to&camera&eventType&location&page&pageSize`

`GET /api/incidents/{id}` records an incident-selection audit.

`GET /api/incidents/search` is the same query and requires at least one filter.

`GET /api/incidents/facets`

`POST /api/incidents/sync` administrator. Pulls the configured incident source and upserts by external incident ID.

## Cameras

`GET /api/cameras?search&includeMock`

`GET /api/cameras/{id}` uses the catalog id.

`GET /api/cameras/mappings` returns mappings, unmapped external IDs, and consistency issues.

`POST /api/cameras/mappings`

```json
{ "externalCameraId": "CAM-ENTRANCE-01", "cameraId": "catalog-guid" }
```

`PUT /api/cameras/mappings/{id}`

`DELETE /api/cameras/mappings/{id}`

## Genetec

`GET /api/genetec/status`

`POST /api/genetec/connect`

`GET /api/genetec/cameras` refreshes the catalog.

`GET /api/genetec/cameras/{genetecGuid}` reads one camera from the active adapter.

## Playback

`POST /api/playback/resolve` and `POST /api/playback/session`

```json
{ "incidentId": "catalog-guid", "preEventSeconds": 60, "postEventSeconds": 60 }
```

The session records playback initialization when the archive can be attempted. The body contains the Web Player endpoint and script URL only when the active adapter can actually start Genetec playback. Mock mode returns `provider: "None"`, `isGenetecRecording: false`, and no media URL.

`GET /api/playback/availability?genetecCameraGuid&startUtc&endUtc`

`POST /api/playback/token/{genetecCameraGuid}` returns `{ token }` for the Web Player callback. Mock mode returns 409 and does not invent a token.

## System

`GET /api/system/status`

`GET /api/dashboard`

`GET /api/audit?take=50` — investigators see their own records; administrators see all.

Swagger is available at `/swagger` in Development.
