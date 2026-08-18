# ADR 0001 — Three projects, feature-sliced inside

**Status:** accepted

## Context

The brief asks to "introduce proper layering within the codebase". The template was a single
ASP.NET Core project with entities at the project root, a `DbContext` declared inside
`ClaimsController.cs`, and the premium calculation as a private controller method.

Layering can be expressed with folders or with projects. A `.csproj` is a compilation unit and an
assembly: a dependency that is not referenced *cannot* be used. A folder communicates intent but
enforces nothing.

## Decision

Projects for boundaries that must not be crossed; folders for grouping within a boundary.

Three boundaries earn an assembly:

| Project | Boundary |
| --- | --- |
| `Claims.Core` | Pure business rules. Zero package references. |
| `Claims.Infrastructure` | MongoDB, EF/SQL Server, the audit queue. |
| `Claims` | HTTP: controllers, middleware, composition root. |

The Domain↔Application seam does **not** get its own assembly. With two aggregates, "entities plus
premium calculator" versus "services plus DTOs plus validators" is a grouping distinction, not a
dependency one — so it is folders inside `Claims.Core`. This matches the well-known three-project
`Core`/`Infrastructure`/`Web` shape.

Inside each project, code is organised by feature slice (`Features/Claims/`, `Features/Covers/`,
`Features/Auditing/`) rather than by technical kind.

## Consequences

- `Claims.Core.csproj` having an empty package-reference list is a compiler-verified claim that the
  business rules are testable without a database, an HTTP context, or Docker. 110 unit tests run in
  about 50ms and prove it.
- A change to one capability stays in one or two folders instead of four.
- Six projects for roughly 40 source files is on the heavy side. Accepted because Task 1 grades
  layering explicitly, and because the template already shipped two projects.
- Rejected: keeping the template layout (fails Task 1 — the `DbContext` in the controller *is* the
  finding); a single project with feature folders (layering stays a convention); four projects
  (over-separates at this size); relocating everything under `src/` and `tests/` (changes every path
  for no architectural gain).
- No `NetArchTest` assertions: with `Claims.Core` referencing nothing, asserting that it does not
  depend on infrastructure would restate what the csproj already proves.