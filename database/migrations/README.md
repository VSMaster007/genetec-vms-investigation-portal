Entity Framework Core owns the schema. The migration that creates the portal tables is:

`backend/src/Investigation.Infrastructure/Persistence/Migrations/20261030074919_InitialCreate.cs`

The API applies it on startup when `Database:Provider` is `Postgres`. `database/seed/incidents.json` is the mock incident source, not a SQL seed script.
