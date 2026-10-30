# Setup

## Tooling

- .NET SDK 8
- Node.js 22
- Docker, for PostgreSQL and the optional full stack

The Genetec Platform SDK is Windows-only and is not required for mock mode or for the Web SDK transport.

## Database

```bash
docker compose up -d postgres
```

The API applies EF Core migrations on startup when `Database:Provider` is Postgres. The migration is `InitialCreate` in `backend/src/Investigation.Infrastructure/Persistence/Migrations`.

## API

```bash
cd backend
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Investigation.Api
```

Development listens on http://localhost:8080. The development configuration contains local-only passwords. Replace them before any shared deployment. Production refuses to seed users unless `Seed:AllowDevPasswords` is set and passwords are supplied through the environment.

## UI

```bash
cd frontend
npm install
npm run dev
```

If `npm install` sits on the first registry request, the host is likely preferring an IPv6 route that never completes. Retry with:

```bash
NODE_OPTIONS="--dns-result-order=ipv4first --no-network-family-autoselection" npm install
```

Vite proxies `/api` to http://localhost:8080.

## Configuration

All Genetec and incident-source secrets are environment variables. The names are listed in `.env.example`. The frontend build does not contain them.

`Genetec:AllowInvalidCertificates` defaults to false. Turn it on only for a lab whose Media Gateway or Web SDK certificate is not in the trust store.

## Tests

```bash
cd backend && dotnet test
cd frontend && npm test
```

Integration tests use the EF Core in-memory provider so they do not need PostgreSQL. They still run the startup seed, mapping, and playback rules.
