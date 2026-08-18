Running it
Prerequisites: .NET 9 SDK, and Docker if you want the zero-configuration local experience.

dotnet run --project Claims
Development starts throwaway MongoDB and SQL Server containers via Testcontainers, so no local database installation is needed. Then open:

Swagger UI — http://localhost:12325/swagger
Health — http://localhost:12325/health
Running without Docker, or hosting it
Docker is a local-development convenience, not a requirement. Set Persistence:UseTestContainers to false and supply connection strings:

{
  "Persistence": {
    "UseTestContainers": false,
    "ApplyMigrationsOnStartup": true,      // deployed environments usually run migrations separately
    "Mongo": {
      "ConnectionString": "mongodb://…",
      "DatabaseName": "Claims"
    },
    "AuditDbConnectionString": "Server=…;Database=Claims.Audit;…"
  },
  "Auditing": {
    "Capacity": 1000,                       // bounded queue; excess events are dropped with a warning
    "BatchSize": 100,
    "ShutdownDrainTimeout": "00:00:05"
  }
}
Misconfiguration fails at startup with a message naming the missing keys, rather than failing on the first request.

Tests
dotnet test Claims.Tests                 # 110 unit tests, no I/O, ~50ms
dotnet test Claims.IntegrationTests      # 42 tests over real HTTP + real databases; needs Docker
Apple Silicon note: mcr.microsoft.com/mssql/server publishes no arm64 image, so the audit database container requires amd64 emulation (Docker Desktop → Use Rosetta for x86/amd64 emulation). This applies to the original template too — it is not specific to this solution.
