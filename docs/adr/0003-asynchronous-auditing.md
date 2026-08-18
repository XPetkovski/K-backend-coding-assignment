# ADR 0003 — In-memory queue plus background writer for auditing

**Status:** accepted

## Context

`Auditer.AuditClaim` / `AuditCover` called `SaveChanges()` synchronously against SQL Server on the
request thread, so every `POST` and `DELETE` waited on an audit round-trip. Related defects: deletes
were audited *before* being attempted (recording removals that never happened), and timestamps used
machine-local `DateTime.Now`.

Task 3 permits an Azure managed service or anything in-memory that works.

## Decision

An in-process bounded queue drained by a `BackgroundService`.

```
request thread   →  IAuditTrail.Record()  →  bounded Channel.TryWrite()  →  returns
background       →  AuditBackgroundService  →  batches  →  EfAuditWriter  →  own DI scope  →  SQL
```

- `IAuditTrail.Record(AuditEvent)` is the seam, expressed as intent rather than mechanism — so the
  implementation can change without touching business code. (Named `IAuditTrail`, not `IAuditQueue`,
  for that reason.)
- `System.Threading.Channels` bounded at 1000, `FullMode = Wait` but written with `TryWrite`, so a full
  queue is reported rather than waited on. A dropped event is logged as a warning.
- `EfAuditWriter` creates a DI scope per batch. `AuditContext` is scoped and the background service is
  a singleton; injecting the context directly would be a captive dependency.
- `StopAsync` completes the channel and drains what remains on its own timeout.
- Write failures are logged and the loop continues.
- Events are recorded only *after* the business operation is confirmed, and stamped from `IClock.UtcNow`.
- The domain `AuditAction.Created`/`Deleted` is mapped back to the existing `HttpRequestType` column
  values `"POST"`/`"DELETE"`, so no HTTP vocabulary leaks into `Claims.Core` and **the database schema
  and migrations are unchanged**.

## Consequences

- Requests no longer wait on the audit database, and a failing audit store cannot fail a request or
  take down the writer loop.
- **Audit loss is possible**: events queued but not yet written are lost if the process dies, and
  events are dropped when the queue is full. This is the deliberate trade-off — auditing must never
  delay or fail the business operation.
- If audit loss is unacceptable, the replacement is a transactional outbox (write the audit row in the
  same transaction as the business change) or Azure Service Bus. Either swaps in behind `IAuditTrail`
  with no change to `Claims.Core`.
- Writing through an `IAuditWriter` seam rather than touching `AuditContext` directly makes batching,
  shutdown drain, and failure recovery unit-testable without Docker — 11 tests cover them.