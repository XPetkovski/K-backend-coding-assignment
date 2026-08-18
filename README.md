# Insurance Claims API

Backend for insurance claims handling: maintain covers, and the claims made against them.

The original task brief is preserved at [`docs/README.md`](docs/README.md) and is left untouched.
This document covers how to run the solution and how it is structured.

For the reasoning behind the structure — what the template looked like, which principles drove the
refactor, how a request flows through the seams, and what was deliberately left out — see
[`HOW-AND-WHY.md`](docs/HOW-AND-WHY.md). Individual decisions with their costs are recorded in [`adr/`](docs/adr).

---

## Running it

**Prerequisites:** .NET 9 SDK, and Docker if you want the zero-configuration local experience.

```bash
dotnet run --project Claims
```

Development starts throwaway MongoDB and SQL Server containers via Testcontainers, so no local
database installation is needed. Then open:

- Swagger UI — <http://localhost:12325/swagger>
- Health — <http://localhost:12325/health>

### Running without Docker, or hosting it

Docker is a local-development convenience, not a requirement. Set `Persistence:UseTestContainers`
to `false` and supply connection strings:

```jsonc
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
```

Misconfiguration fails at startup with a message naming the missing keys, rather than failing on the
first request.

### Tests

```bash
dotnet test Claims.Tests                 # 110 unit tests, no I/O, ~50ms
dotnet test Claims.IntegrationTests      # 42 tests over real HTTP + real databases; needs Docker
```

> **Apple Silicon note:** `mcr.microsoft.com/mssql/server` publishes no arm64 image, so the audit
> database container requires amd64 emulation (Docker Desktop → *Use Rosetta for x86/amd64
> emulation*). This applies to the original template too — it is not specific to this solution.

---

## Structure

```
Claims/                     ASP.NET Core host — controllers, middleware, composition root
Claims.Core/                Business rules. ZERO package references.
Claims.Infrastructure/      MongoDB, EF/SQL Server, the audit queue
Claims.Tests/               Fast unit tests
Claims.IntegrationTests/    Testcontainers-backed HTTP tests
```

Dependencies point inward only: `Claims` → `Claims.Infrastructure` → `Claims.Core` → nothing.

`Claims.Core.csproj` has an empty package-reference list, and that emptiness is the architectural
assertion: the premium calculation and the validation rules cannot reach for a database, an HTTP
context, or Docker, because those assemblies are not referenced. It is enforced by the compiler
rather than by convention.

Inside each project, code is organised by **feature slice** rather than by technical kind:

```
Claims.Core/Features/Covers/     Cover, CoverType, CreateCoverCommand, CreateCoverCommandValidator,
                                 CoverResponse, CoversService, ICoverRepository,
                                 PremiumCalculator, PremiumRates
```

A change to "how covers work" touches `Core/Features/Covers/` and `Claims/Features/Covers/` — not four
scattered `Entities/`, `Services/`, `Validators/`, `Dtos/` folders.

**Three projects, not folders**, because a folder boundary is a naming convention that one stray
`using` defeats, while an assembly boundary cannot be crossed at all. Three, not four, because the
Domain↔Application seam is a *grouping* distinction at this size, not a dependency one — so it is
expressed as folders inside `Claims.Core`.

---

## API

| Method | Route | Success | Failure |
| --- | --- | --- | --- |
| `GET` | `/Claims` | `200` | |
| `GET` | `/Claims/{id}` | `200` | `404` |
| `POST` | `/Claims` | `201` + `Location` | `400` invalid, `404` unknown cover |
| `DELETE` | `/Claims/{id}` | `204` | `404` |
| `GET` | `/Covers` | `200` | |
| `GET` | `/Covers/{id}` | `200` | `404` |
| `POST` | `/Covers` | `201` + `Location` | `400` |
| `DELETE` | `/Covers/{id}` | `204` | `404` |
| `GET` | `/Covers/compute` | `200` | `400` |
| `GET` | `/health` | `200` / `503` | |

Failures return [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807) `ProblemDetails`. Validation
failures report **every** broken rule at once, so a client fixes one round-trip rather than four:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "DamageCost": ["DamageCost cannot exceed 100,000."],
    "Name": ["Name is required."]
  }
}
```

Dates are exchanged as plain dates (`"2026-09-01"`), and enums as names (`"Tanker"`).

---

## Premium computation

Premium depends on cover type and period length, with the period split into three tiers:

```
dayRate = 1250 × typeMultiplier
  Yacht 1.10 · PassengerShip 1.20 · Tanker 1.50 · ContainerShip/BulkCarrier 1.30

days   1–30    dayRate                     (full rate)
days  31–180   dayRate × (1 − d₂)          d₂ = 5% Yacht, 2% other
days 181+      dayRate × (1 − d₃)          d₃ = 8% Yacht, 3% other   ("additional" 3% / 1%)
```

### The bug that was there

The original implementation used three independent `if` blocks rather than `else if`, so the tiers
**overlapped instead of partitioning** the period. Days 0–29 were charged three times, days 30–179
twice, and days past 365 were charged nothing at all:

| Days | Was | Should be | Overcharge |
| --- | --- | --- | --- |
| 30 | 118,387.50 | 41,250.00 | **2.87×** |
| 180 | 504,075.00 | 237,187.50 | 2.13× |
| 365 | 738,100.00 | 471,212.50 | 1.57× |
| 400 | (free after day 365) | 515,487.50 | — |

*(Yacht; the pattern holds for every type.)*

The loop is now a closed-form calculation over three non-overlapping tiers, with rates named in
`PremiumRates` instead of scattered as literals. 53 unit tests pin every tier boundary (days
1/30/31/180/181/365 × all five cover types), and `Never_decreases_as_the_period_lengthens` asserts
monotonicity across days 1–400 for every type.

---

## Design decisions

### Layering, not CQRS

Clean-Architecture-style layering, deliberately **without** CQRS or a mediator.

CQRS in its real sense — separate read and write models, projections, eventual consistency — answers a
read/write scaling asymmetry that two aggregates and ten endpoints do not have. MediatR's genuine
appeal is pipeline behaviours for cross-cutting validation and auditing, but an `IExceptionHandler`
plus an `IValidator<T>` reach the same place without the indirection, and without adding a
commercially-licensed dependency to the project.

Commands are still named as commands (`CreateCoverCommand`), so read and write intent is explicit at
the API boundary. What is missing is only the dispatcher.

**When CQRS would be the right answer here:** read and write load diverging enough to scale
independently; write-side invariants complex enough to justify aggregates and domain events; several
client shapes needing different projections; or audit history becoming a first-class read model — at
which point the conversation is event sourcing, not just CQRS. None of those hold today.

### Auditing does not block the request

`POST` and `DELETE` used to wait on a synchronous `SaveChanges()` against SQL Server before
responding. Now:

```
request thread   →  IAuditTrail.Record()  →  bounded Channel.TryWrite()  →  returns
background       →  AuditBackgroundService  →  batches  →  EfAuditWriter  →  own DI scope  →  SQL
```

Details that matter:

- **A scope per batch.** `AuditContext` is scoped and the background service is a singleton, so
  `EfAuditWriter` creates its own scope via `IServiceScopeFactory`. Injecting the context directly
  would be a captive dependency.
- **A full queue drops rather than blocks.** `TryWrite` returns `false` instead of waiting; the event
  is logged as a warning. This is the deliberate trade-off — auditing must never delay or fail the
  business operation.
- **Shutdown drains.** `StopAsync` completes the channel and flushes what is queued on its own
  timeout, so a cancelled stopping token does not silently discard events.
- **A failing audit store cannot take down the API.** Write failures are logged and the loop
  continues.

Audit events are queued **after** the business operation is confirmed, which fixes a real bug: the
original code audited deletes *before* attempting them, recording removals of records that never
existed.

*If audit loss is unacceptable, the replacement is a transactional outbox or Azure Service Bus. The
`IAuditTrail` seam is precisely where that swap happens, with no change to business code.*

### Validation is hand-rolled

`IValidator<T>` is about 45 lines rather than a FluentValidation dependency, because adding
FluentValidation would put the first `PackageReference` into `Claims.Core` and break the guarantee that
file makes. For five rules per command that trade is worth it; past roughly twenty rules it would not
be.

The cross-entity rule — a claim's `Created` date must fall inside its cover's period — needs the cover
loaded, so it lives in `ClaimsService` rather than in a validator, and returns `404` for a missing
cover versus `400` for a date outside the period.

### `DateOnly` at the boundary, `DateTime` in storage

Commands and responses use `DateOnly`; entities keep `DateTime`. The rules in the brief are all
date-based ("cannot be in the past", "cannot exceed 1 year", "within the period of the cover"), and
`DateOnly` removes any time-component ambiguity from them. Keeping `DateTime` on the entities avoids
depending on MongoDB provider `DateOnly` support, and the conversion is a single line in each service.

---

## Assumptions

Stated explicitly, because reasonable people would choose differently:

1. **Day counting is inclusive.** A cover starting and ending on 1 January is one day, so one calendar
   year runs to the day before the anniversary. The original code counted exclusively
   (`(end - start).TotalDays`).
2. **One year is a calendar year**, via `StartDate.AddYears(1)`. A cover starting in a leap year
   legitimately gets 366 days; hardcoding 365 would shortchange it.
3. **`DamageCost` must be positive** and at most 100,000. The brief only gives the upper bound.
4. **`Created` is supplied by the client**, since the brief validates it against the cover period.
5. **In-memory audit durability is acceptable** — events queued but unwritten are lost if the process
   dies. See the outbox note above.
6. **`GET /Covers/compute`** replaces the original `POST /Covers/compute`: it computes a quote and has
   no side effects. Same path, corrected verb.

---

## Task map

| Task | Where |
| --- | --- |
| 1 — layering, SOLID, docs | Project split, feature slices, thin controllers, `ProblemDetails`, this README, [`adr/`](docs/adr) |
| 2 — validation | `Claims.Core/Features/*/Create*CommandValidator.cs`; cross-entity rule in `ClaimsService` |
| 3 — non-blocking auditing | `Claims.Infrastructure/Auditing/` — `QueuedAuditTrail`, `AuditEventChannel`, `AuditBackgroundService`, `EfAuditWriter` |
| 4 — tests | `Claims.Tests` (110), `Claims.IntegrationTests` (42) |
| 5 — premium computation | `Claims.Core/Features/Covers/PremiumCalculator.cs`, `PremiumRates.cs` |

---

## Known limitations

Called out so the omissions read as decisions rather than oversights:

- **No authentication or authorisation.** `UseAuthorization()` is present but no scheme is registered;
  it is decorative. Not part of the brief.
- **Deleting a cover leaves its claims orphaned.** Blocking the delete with `409 Conflict` is the
  cheapest correct behaviour; cascade-versus-soft-delete is the real design conversation.
- **No cross-store transaction.** Business writes go to MongoDB and audit writes to SQL Server; they
  cannot share a transaction. An outbox is the answer.
- **No pagination** on the list endpoints.
- **No optimistic concurrency** — there is no update endpoint in scope.
- **`Claims.csproj` still references Testcontainers**, to preserve zero-configuration local
  development. A deployment-only build would drop `LocalContainers` and that reference.