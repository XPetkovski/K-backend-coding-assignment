# How and why this code is built the way it is

A walkthrough of the reasoning, for a reader who wants to know *why the code looks like this* rather
than *how to run it*.

Where the other documents fit:

| Document | Answers |
| --- | --- |
| [`README.md`](../README.md) | How do I run it, and what does it do? |
| [`adr/`](adr) | What was decided, and what did it cost? (one decision per file) |
| [`ARCHITECTURE-PLAN.md`](ARCHITECTURE-PLAN.md) | Working plan — findings, phases, sequencing. |
| **this file** | How does the code hang together, and why is each seam there? |

---

## 1. The starting point

The template was two projects: an ASP.NET Core app and a test project with one test. The problems were
not stylistic — they were structural, and each one blocked a task in the brief.

```csharp
// CoversController.cs, as it shipped
public CoversController(ClaimsContext claimsContext, AuditContext auditContext, ILogger<CoversController> logger)
{
    _claimsContext = claimsContext;
    _logger = logger;                       // injected, never used
    _auditer = new Auditer(auditContext);   // hand-newed: nothing here can be substituted
}

private decimal ComputePremium(DateTime startDate, DateTime endDate, CoverType coverType)
{
    // 30 lines of business rules, private to an HTTP controller
}
```

Read as a list of obstacles rather than a list of smells:

- **The premium formula was a `private` method on a controller.** Task 5 asks for it to be fixed and
  made readable, and Task 4 asks for tests. You cannot unit-test a private controller method without
  constructing two `DbContext`s. *That* is why Task 4's note says "you will find it hard to write
  proper tests" — the difficulty is the finding.
- **`new Auditer(auditContext)`** in the constructor. Task 3 asks to change how auditing executes. With
  the dependency hand-newed, there is no seam to change it at.
- **A `DbContext` used directly as the persistence API.** Validation (Task 2) needs to load a cover to
  check a claim's date against its period; with EF in the controller, every validation test needs a
  database.
- **Entities were the API contract.** `Cover` went out on the wire, so the storage shape and the
  published shape could not move independently.
- **Genuine bugs.** `GET /Covers/{id}` loaded the entire collection and filtered in memory. Missing
  records returned `200` with a `null` body instead of `404`. `DELETE` returned `200` with an empty
  body. Deletes were audited *before* being attempted, recording removals that never happened.
  Timestamps used machine-local `DateTime.Now`. `async` methods had no `await`.

So the refactor is not decoration. Every task in the brief is gated behind removing one of these.

## 2. The governing principles

Four decisions drove everything else. Each is stated with the concrete thing it bought.

### Projects for boundaries, folders for grouping

A `.csproj` is a compilation unit. A dependency that is not referenced *cannot* be used — the compiler
refuses. A folder communicates the same intent and enforces nothing; one `using` defeats it.

So the boundaries that genuinely must hold got assemblies, and everything else got folders:

```
Claims                  →  Claims.Infrastructure  →  Claims.Core  →  (nothing)
HTTP, composition root     MongoDB, EF, the queue    business rules
```

Three, not four: the Domain↔Application seam (entities vs. services/DTOs/validators) is a *grouping*
distinction at two aggregates, not a dependency one, so it is folders inside `Claims.Core`.

### `Claims.Core` has zero package references, on purpose

`Claims.Core.csproj` contains a `PropertyGroup`, a comment explaining itself, and no `ItemGroup` at
all. That absence is the load-bearing part of the design. It is a claim — *the business rules cannot
reach for a database, an HTTP context, or Docker* — that the build verifies on every compile.
Documentation drifts; this cannot.

It is also why the 122 unit tests run in ~50ms with no containers, which is what makes Task 4
tractable at all.

The rule costs something, and the cost was paid deliberately twice:

- **Validation is hand-rolled** (~45 lines in `Common/Validation.cs`) rather than FluentValidation,
  because FluentValidation would be the first `PackageReference` in `Claims.Core`. For five rules per
  command that trade is worth it; past roughly twenty it would not be.
- **Mongo mapping moved out of the entities.** The `[BsonElement]` attributes that used to sit on
  `Claim` and `Cover` became fluent `HasElementName` calls in `ClaimsContext.OnModelCreating`. Same
  stored element names, same documents — the persistence concern just moved to the layer that owns it.

### Organise by feature, not by technical kind

```
Claims.Core/Features/Covers/    Cover, CoverType, CreateCoverCommand, CreateCoverCommandValidator,
                                CoverResponse, CoversService, ICoverRepository,
                                PremiumCalculator, PremiumRates
```

"How covers work" is one folder, not four (`Entities/`, `Services/`, `Validators/`, `Dtos/`). Layering
answers *what may depend on what*; slicing answers *what changes together*. They are different
questions, so they are answered by different mechanisms — assemblies for the first, folders for the
second.

### Enough structure, and no more

The brief is a judgment test with two symmetric failure modes: leave the fat controller alone (fails
Task 1), or bolt CQRS, MediatR, separate read models and an event bus onto a CRUD app with two
aggregates (fails the unstated half). The details are in [ADR 0002](adr/0002-no-cqrs-or-mediator.md);
the short version is that commands keep command *names* so read/write intent is explicit, and only the
dispatcher is absent.

## 3. How a request actually flows

`POST /Claims` end to end — the one path that touches every seam:

```
ClaimsController.CreateAsync                        binds JSON, returns 201 + Location. No logic.
  └─ ClaimsService.CreateAsync                      the use case
       ├─ IValidator<CreateClaimCommand>            shape rules → ValidationException
       ├─ ICoverRepository.GetByIdAsync             cross-entity rule needs the cover
       │    └─ null → NotFoundException             404, not 400: the cover, not the input, is wrong
       ├─ EnsureCreatedWithinCoverPeriod            → DomainException (400)
       ├─ IClaimRepository.AddAsync                 the write
       └─ IAuditTrail.Record                       after the write succeeds
            └─ bounded Channel.TryWrite            returns immediately
                 ⋮ (background)
                 AuditBackgroundService → batches → EfAuditWriter → own DI scope → SQL Server
```

Three things about that shape are deliberate:

**The controller has no logic.** It binds, delegates, and chooses a status code. Every branch worth
testing is in `ClaimsService`, reachable without HTTP.

**Failures are exceptions, translated once.** Services throw `ValidationException`, `DomainException`,
or `NotFoundException` — all defined in `Claims.Core`, none knowing what an HTTP status code is. A
single `IExceptionHandler` maps them to RFC 7807 `ProblemDetails`:

```csharp
ProblemDetails? problem = exception switch
{
    ValidationException validation => ToValidationProblem(validation),   // 400 + per-property errors
    DomainException domain         => …,                                 // 400
    NotFoundException notFound     => …,                                 // 404
    _                              => null                              // logged, rethrown as 500
};
```

The alternative — returning a result type and mapping in each action — puts the same `switch` in nine
places. One handler means a new failure kind is wired up once, and an *unexpected* exception still
gets logged with its route and becomes a 500 rather than leaking a stack trace.

**Validation reports every broken rule at once.** `ValidationResult.AddIf` accumulates rather than
throwing on the first failure, so a client fixes four problems in one round-trip instead of four.

**Where each rule lives follows from what it needs.** Shape rules (`DamageCost` ≤ 100,000, `StartDate`
not in the past, period ≤ 1 year) need only the command, so they sit in an `IValidator<T>`. The
cross-entity rule — a claim's `Created` date must fall inside its cover's period — needs the cover
loaded, so it lives in `ClaimsService`. Pushing it into a validator would mean injecting a repository
into a validator, which makes "validate this object" secretly do I/O.

## 4. Why each seam exists

Interfaces are cost. These earned their keep:

| Seam | Why it exists | What it enabled |
| --- | --- | --- |
| `ICoverRepository` / `IClaimRepository` | keeps EF out of `Claims.Core` | in-memory fakes; 24 service tests with no database |
| `IAuditTrail` | names *intent* ("record this"), not mechanism ("enqueue this") | the whole of Task 3 happened behind it without touching business code |
| `IAuditWriter` | separates *what to write* from *when and in what batches* | batching, shutdown drain and failure recovery are unit-testable without Docker |
| `IClock` | `DateTime.Now` is untestable and machine-local | "StartDate cannot be in the past" is testable, and timestamps are UTC |
| `IPremiumCalculator` | lets `CoversService` be tested without re-deriving premiums | service tests assert *that* a premium was stored, not its value |
| `IValidator<T>` | one shape for all rule sets | the exception handler needs one `catch`, not one per command |

`IAuditTrail` is worth dwelling on, because the naming *is* the design. Had it been called
`IAuditQueue.Enqueue`, the queue would have leaked into `Claims.Core` and swapping in an outbox or
Service Bus would have meant renaming the interface — i.e. changing business code to accommodate an
infrastructure decision. `Record(AuditEvent)` describes what the domain wants; how it happens is
Infrastructure's business.

## 5. Why the premium calculator looks like that

The original bug was structural rather than arithmetic. Three independent `if` statements where the
tiers should have partitioned the period:

```csharp
for (var i = 0; i < insuranceLength; i++)
{
    if (i < 30) totalPremium += premiumPerDay;                       // ← no else
    if (i < 180 && coverType == CoverType.Yacht) …                   // ← no else
    else if (i < 180) …
    if (i < 365 && coverType != CoverType.Yacht) …
    else if (i < 365) …
}
```

The tiers *overlapped* instead of partitioning. Days 0–29 were billed three times, days 30–179 twice,
and days past 365 were free. Not a rounding error — a 2.87× overcharge on a 30-day yacht cover, and
unbounded free cover past a year.

```
     Was          Should be     Overcharge
30   118,387.50   41,250.00     2.87×
180  504,075.00   237,187.50    2.13×
365  738,100.00   471,212.50    1.57×
400  (free)       515,487.50    —
```

The rewrite drops the loop entirely, because a per-day loop was only ever needed to express a per-day
rule that does not exist — the tiers are ranges, so the closed form is both faster and closer to how
the brief words it:

```csharp
var fullRateDays      = Math.Min(days, PremiumRates.FullRateDays);
var firstDiscountDays = Math.Clamp(days - PremiumRates.FullRateDays, 0, PremiumRates.FirstDiscountDays);
var secondDiscountDays = Math.Max(days - PremiumRates.FullRateDays - PremiumRates.FirstDiscountDays, 0);
```

Three non-overlapping tiers, by construction rather than by careful reading. Rates live in
`PremiumRates` as named constants instead of as literals sprinkled through a loop.

Two judgement calls, both stated because a reasonable reader could disagree:

- **"Additional 3% / 1%" is additive**, so the third tier is 8% for yacht and 3% otherwise — not
  compounding (`0.95 × 0.97`). The wording is ambiguous, but the original code used literal `0.08m`
  and `0.03m`, so this matches the previous author's intent rather than merely my reading of English.
- **Day counting is inclusive** — a cover starting and ending on 1 January is one day. The original
  used `(end - start).TotalDays`, which is exclusive. Inclusive is how insurance periods are normally
  quoted, and it makes "the first 30 days" mean thirty days.

53 tests pin every tier boundary (days 1/30/31/180/181/365 × all five cover types), and
`Never_decreases_as_the_period_lengthens` asserts monotonicity across days 1–400 for every type —
which is the property that would have caught the original bug, since the old code's premium *dropped*
past day 365.

## 6. Why auditing looks like that

Task 3 says the synchronous `SaveChanges()` blocks the request and asks for an asynchronous pattern,
explicitly permitting either an Azure managed service or anything in-memory.

In-memory won for a reason beyond expedience: a `System.Threading.Channels` queue drained by a
`BackgroundService` is testable. Eleven unit tests cover batching, drain-on-shutdown, and
write-failure recovery with no Docker and no cloud subscription — none of which would be true of a
Service Bus binding, in a repository whose whole point is being cloned and run by a reviewer.

The parts that are easy to get wrong, and how each is handled:

- **Captive dependency.** `AuditContext` is scoped; the background service is a singleton. Injecting
  the context directly would hold one scoped `DbContext` for the process lifetime. `EfAuditWriter`
  creates a scope per batch via `IServiceScopeFactory`.
- **A full queue must not block the request.** `TryWrite` returns `false` rather than waiting, and the
  dropped event is logged as a warning. This is the deliberate trade-off: auditing must never delay or
  fail the business operation.
- **Shutdown must not silently discard.** `StopAsync` completes the channel first, then drains what is
  queued on its own timeout — otherwise a cancelled stopping token throws away buffered events.
- **A failing audit store must not take down the API.** Write failures are logged and the loop
  continues.
- **Ordering.** Events are recorded only *after* the business operation is confirmed, which is the fix
  for the original delete-before-audit bug.
- **The schema is unchanged.** `AuditAction.Created`/`Deleted` is mapped back to the existing
  `HttpRequestType` values `"POST"`/`"DELETE"` inside Infrastructure, so no HTTP vocabulary leaks into
  `Claims.Core` *and* the existing migration still applies.

The honest cost: **audit loss is possible.** Events queued but unwritten die with the process, and
events are dropped when the queue is full. If that is unacceptable the answer is a transactional
outbox or Service Bus — and `IAuditTrail` is exactly where that swaps in, with no change to
`Claims.Core`.

## 7. What was deliberately not built

Each of these is a decision, not an oversight, so each comes with the condition that would reverse it.

| Not built | Reverse it when |
| --- | --- |
| CQRS / MediatR | read and write load diverge; invariants justify aggregates and domain events; several client shapes need different projections |
| FluentValidation | rule count passes ~20, or rules need composition and localisation |
| Durable auditing (outbox / Service Bus) | audit records become a compliance artefact rather than a diagnostic |
| Blocking cover deletion that has claims (`409`) | as soon as the orphan matters — this is the closest thing to a real gap |
| Authentication | `UseAuthorization()` is present with no scheme registered, i.e. decorative. Out of scope for the brief |
| Pagination on list endpoints | collections stop being small |
| Optimistic concurrency | an update endpoint exists — there is none today |
| `NetArchTest` layering assertions | never, while `Claims.Core` references nothing — the csproj already proves it |

## 8. Verifying the claims in this document

Nothing here needs to be taken on trust:

```bash
dotnet build                                    # 0 warnings, 0 errors
dotnet test Claims.Tests                        # 122 passed, ~50ms, no Docker
dotnet list Claims.Core/Claims.Core.csproj package
                                                # "No packages were found for this framework."
```

The third command is the one worth running. If it ever prints a package, the central claim of this
design has been broken.

`Claims.IntegrationTests` — 52 tests against real MongoDB and SQL Server — runs in CI rather than
locally, because `mcr.microsoft.com/mssql/server` publishes no arm64 image and would need Rosetta
emulation on Apple Silicon. GitHub's `ubuntu-latest` runners are amd64 with a Docker daemon, so the
suite runs natively there on every push and pull request:

```bash
dotnet test Claims.IntegrationTests              # 52 passed, ~40s, needs Docker
```

That first CI run earned its keep immediately. All 52 failed at once, because the collection fixture
threw during startup: the audit entities declare their string columns as `string?`, the 2022 migration
snapshot maps them as `NOT NULL`, and **EF Core 9 promoted `PendingModelChangesWarning` from a warning
into a thrown exception**. Every `Database.Migrate()` call therefore failed.

That drift is inherited, not introduced — the original template called `Database.Migrate()` in
`Program.cs` and its entities were already `string?` under `<Nullable>enable</Nullable>`, so the defect
arrived with the .NET 9 upgrade. It had simply never been observable, because nothing booted the
application. `AlignAuditColumnNullability` closes it, and
`AuditMigrationsTests.Audit_model_matches_the_latest_migration` compares model to snapshot in memory via
`HasPendingModelChanges()` — no database, no Docker, ~190ms — so the next drift surfaces as one failing
unit test rather than 52 fixture errors.
