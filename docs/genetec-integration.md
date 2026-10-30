# Genetec integration

This portal was implemented against Genetec's published DAP guides, not against a guessed SDK surface. The Security Center SDK assemblies were not installed on the Linux build machine. Nothing in mock mode is reported as a live Directory connection.

## Browser playback

Documented player: Genetec Web Player (`gwp.js`), loaded from the same Media Gateway that streams the camera.

Used calls:

- `gwp.buildPlayer(container)`
- `player.start(cameraGuid, mediaGatewayEndpoint, tokenRetriever)`
- `player.seek(date)`, `pause()`, `resume()`, `setPlaySpeed(speed)`, `dispose()`
- `player.timelineProvider.setTimelineRange(start, end)`
- `player.onFrameRendered` and `player.onErrorStateRaised`

Token retrieval, kept on the server:

`GET {mediaGateway}/v2/token/{cameraGuid}`

`Authorization: Basic base64(username;sdkCertificate:password)`

The API returns the token text to the signed-in browser because the Web Player requires it. The certificate and password are not embedded in frontend source and are not written to the audit log.

Media Gateway reachability is a GET of `{endpoint}/v2/files/gwp.js`. The status page reports success only when that request returns HTTP 200.

## Web SDK transport

`Genetec:Mode=Real` and `Genetec:RealTransport=WebSdk`.

Base URL example: `https://directory-host:4590/WebSdk/`

Authentication header: `Basic base64(username;applicationId:password)` with `Accept: text/json`.

Verified operations:

- `GET /` and require `Rsp.Status = Ok` before reporting the Directory as connected
- `GET /report/EntityConfiguration?q=EntityTypes@Camera,Page,PageSize,ReturnFields@Name`
- `GET /entity?q=entity={guid},Name,Description,IsOnline`
- `GET /report/VideoSequence?q=Cameras@{guid},TimeRange.SetTimeRange(start,end)`

HTTP 401 and 403 are authentication or privilege failures. They are not reported as a successful connection. A camera entity that returns 403 is treated as access denied.

The Web SDK user must be a local Security Center account with **Log on using the SDK**. Playback also needs the Web App privilege and **View playback** on the camera.

## Platform SDK transport

Compiled only when `GSC_SDK_CORE` is set on Windows and `Genetec.Sdk.dll` is referenced. The code uses the documented members:

- `new Engine()`
- `engine.ClientCertificate`
- `LoginManager.RequestDirectoryCertificateValidation` and `AcceptDirectory`
- `LogOnAsync(directory, username, password)` compared with `ConnectionStateCode.Success`
- `LoginManager.IsConnected`, `LogOff`, `Dispose`
- `ReportManager.CreateReportQuery(ReportType.EntityConfiguration)` cast to `EntityConfigurationQuery`
- `EntityTypeFilter.Add(EntityType.Camera)`, then `GetEntity<Camera>`
- `CreateReportQuery(ReportType.VideoSequence, null)` cast to `SequenceQuery`
- `TimeRange.SetTimeRange(start, end)`

If the sequence result does not contain the documented `CameraGuid`, `StartTime`, and `EndTime` columns, availability fails with the actual column names instead of guessing. Directory certificates are rejected unless `Genetec:Platform:AcceptDirectoryCertificate` is true.

If this transport is selected where the SDK is not referenced, the status stays failed with `SdkNotInstalled`.

## What this build did not verify

No Security Center directory, SDK certificate, or Media Gateway was available in this environment. Real playback still has to be exercised against an authorized system. The steps are in `docs/poc-demonstration.md`.
