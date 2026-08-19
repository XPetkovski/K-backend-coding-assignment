# ADR 0002 — No CQRS, no mediator

**Status:** accepted

## Context

Clean Architecture in .NET is frequently paired with CQRS and MediatR. The obvious appeal here is that
MediatR pipeline behaviours would solve Task 2 (validation) and Task 3 (auditing) as cross-cutting
concerns in one place.

The application has two aggregates, ten endpoints, no reporting load, and no read/write scaling
asymmetry.

## Decision

Layered architecture with application services. No separate read and write models, no projections, no
mediator.

Commands keep command names (`CreateCoverCommand`, `CreateClaimCommand`) so read and write intent is
explicit at the API boundary. Only the dispatcher is absent.

Cross-cutting concerns are handled by framework primitives instead:

- validation — `IValidator<T>` invoked at the top of each service method;
- error mapping — a single `IExceptionHandler` producing `ProblemDetails`;
- auditing — the `IAuditTrail` seam, called by services.

## Consequences

- No indirection between controller and behaviour: `ClaimsController.CreateAsync` calls
  `ClaimsService.CreateAsync`, and that is the whole call chain.
- No commercially-licensed dependency. MediatR moved to a paid licence for commercial use, which is
  awkward to introduce into a repository someone else inherits.
- Cross-cutting concerns are invoked explicitly rather than implicitly. That is more lines but easier
  to follow; with twenty commands instead of two, a mediator would start to pay for itself.
- **CQRS would become the right answer if:** read and write load diverged enough to scale
  independently; write-side invariants grew complex enough to justify aggregates and domain events;
  multiple client shapes needed different projections; or audit history became a first-class read
  model — at which point the discussion is event sourcing, not merely CQRS.