# POC demonstration

## Mock walkthrough

This path runs without Security Center and is labeled as mock at every step.

1. Start PostgreSQL, the API, and the UI as described in the README.
2. Sign in as `investigator@investigation.local`.
3. Open System Status. Directory, SDK authentication, and Media Gateway show **Mock**, not Healthy.
4. Open Investigations and choose `INC-2026-001`.
5. The right panel shows Main Entrance Camera (mock) and its synthetic GUID.
6. The player banner says the surface is not a Genetec recording. Pause, resume, and the timeline still work against the mock clock.
7. Open `INC-2026-004`, `INC-2026-005`, `INC-2026-006`, and `INC-2026-007` to see retention, permission, gap, and missing-mapping failures.

## Real Security Center walkthrough

Complete this on a network that can reach the Directory and the Media Gateway.

1. Install or confirm the Web SDK role and the Media Gateway role.
2. Create a local user with **Log on using the SDK**, **Web App**, and **View playback** for the test camera.
3. Put the SDK certificate application ID, username, password, Web SDK URL, and both Media Gateway URLs in the environment. Do not commit them.
4. Set `GENETEC_MODE=Real` and `GENETEC_REAL_TRANSPORT=WebSdk`.
5. Restart the API. System Status must show Directory and SDK authentication as Healthy, and Media Gateway as Healthy only after `gwp.js` returns HTTP 200.
6. Refresh cameras. Copy the real camera GUID from Camera Management.
7. Map the external incident camera ID to that catalog row. Remove any mock mapping first. Real mode rejects mappings that still point at mock cameras.
8. Sync incidents from the external API, or insert one incident whose timestamp falls inside retained video.
9. Open the incident. The player loads `gwp.js` from the Media Gateway, seeks to the incident timestamp, and uses the server token endpoint.
10. Pause, resume, and click the timeline. If the archive does not cover the time, the player shows the Web Player error instead of a substitute video.

A mock-only run does not satisfy the acceptance test that archived Genetec video plays in the browser. That step needs the environment above.
