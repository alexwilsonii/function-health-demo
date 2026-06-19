# Task Manager

A small, multi-tenant to-do application: **ASP.NET Core (.NET 8) Minimal API + EF Core/SQLite** on
the backend, **Vue 3 + TypeScript** (Vite, Pinia, TanStack Query) on the frontend.

It's deliberately a small surface area, finished end to end: real auth and per-user ownership, full
CRUD with optimistic UI, validation with inline errors, search/filter/sort, and tests on the parts
that actually carry risk.

---

## Prerequisites

- **.NET 8 SDK**
- **Node 20+** (built with Node 22)

> You do **not** need the `dotnet-ef` tool or a database server. The API applies migrations on
> startup and SQLite is just a file on disk.

## Run it (two terminals)

```bash
# 1) API  →  http://localhost:5000   (creates + migrates the SQLite DB automatically)
cd api
dotnet run

# 2) Web  →  http://localhost:5173   (Vite dev server; proxies /api to the API)
cd web
npm install
npm run dev
```

Open **http://localhost:5173** and either register an account or use the seeded demo login:

> **Demo login (development only):** `demo@example.com` / `Password123!`
> Seeded on first startup in Development, with a handful of sample tasks (overdue, pinned, mixed
> priorities/statuses). It is **never** seeded in production, and seeding is idempotent (runs once).

No secrets to set, no HTTPS dev cert — dev runs over plain `http://localhost`.

**Run the tests:**

```bash
dotnet test
```

**Optional — production-style single process:** build the SPA into the API and serve both on one
port:

```bash
cd web && npm run build                      # outputs web/dist
mkdir -p ../api/wwwroot && cp -r dist/* ../api/wwwroot/   # API serves the SPA from wwwroot
cd ../api && dotnet run                       # http://localhost:5000 serves SPA + API
```

---

## What I built

- **Auth** — register, login, logout, current-user. The JWT lives in an **httpOnly cookie**;
  sessions survive a refresh, and an expired session cleanly bounces you to the login page.
- **Task CRUD** — create, list, edit, delete, plus one-click **status cycling** and **pin** toggling.
  Every mutation updates the list **optimistically** and **rolls back with a toast** if the server
  rejects it.
- **Detail view + comments** — clicking a task (anywhere on the card) opens a read-only **view**
  modal; a pencil flips it to edit. Each task has a **comments** thread at the bottom of the view,
  lazily loaded, with optimistic add/delete.
- **Search / filter / sort** — search title+notes, filter by status and priority, sort by due date,
  priority, or recency. **Pinned** tasks float to the top; **overdue** tasks are flagged.
- **Validation** — server-side rules surfaced as **inline field errors**; the form keeps your input
  on failure.
- **Time zones** — due dates are stored in **UTC** and rendered in your **local** time.
- **Accessible & responsive** — keyboard-operable throughout, a focus-trapped create/edit dialog
  (`Esc` to close, focus restored on close), `aria-live` announcements for validation and toasts,
  status/priority/overdue conveyed by **text + icon, never color alone**, and a layout that reflows
  from ~320px to desktop. Built to WCAG 2.1 AA guidance (verified by keyboard walkthrough; not a
  full audit).

## What I deliberately left out — and why

The brief rewards matching the solution to the size of the problem. These were considered and cut:

- **Activity history, tags, bulk actions** — each is more surface area and another "claim it works
  end to end." For a single-user task manager the marginal value is low and the risk to
  *finishedness* is high. (Bulk actions also hide partial-failure semantics that deserve to be done
  properly or not at all.) *(Comments were originally on this list but have since been added.)*
- **Client-generated IDs / idempotency keys** — IDs are server-generated and the submit button is
  disabled while saving. The idempotency-key pattern is what I'd add for an offline-capable client.
- **CI/CD, Docker, deployment config, metrics/tracing/monitoring infrastructure** — explicitly not
  evaluated. (Security-relevant actions *are* logged via the built-in logger — see **Logging &
  auditability** below — but there's no metrics/tracing/dashboard stack.)
- **Password reset / email verification, refresh tokens / token revocation** — out of scope (see the
  auth caveat below).
- **Microservices / event-driven decomposition** — Function's imaging platform uses these at scale,
  but a single-entity task manager is one process, one DB, one deploy; splitting it would be
  over-engineering. If it grew into separate bounded contexts (tasks, teams, notifications) I'd split
  along those seams and introduce events for cross-context reactions.

## Auth & ownership

- JWT issued by the API in an **httpOnly + `SameSite=Strict`** cookie (`Secure` in production; off
  for `http://localhost` in dev). Passwords hashed with **BCrypt**. The SPA and API are same-origin
  (Vite proxy in dev, `wwwroot` in prod), so `SameSite=Strict` closes the CSRF vector.
- **Ownership is enforced at the data layer** by an EF Core **global query filter** that scopes every
  task query to the current user. Single-item operations also use an explicit `UserId` predicate, and
  never `DbSet.Find` (which bypasses query filters). A missing or foreign task returns **404, not
  403**, so existence isn't leaked.
- **Honest caveat:** a plain JWT isn't server-revocable before it expires; logout clears the cookie.
  Production would use short-lived access + refresh tokens, or a server-side denylist.

## Notable correctness details

- **One error envelope.** All bad input returns `400` `ValidationProblemDetails` (field-keyed) —
  including **binding failures** (invalid enum, malformed JSON), which middleware maps to the same
  shape, so the frontend consumes a single contract.
- **SQLite + `DateTimeOffset`.** SQLite can't `ORDER BY` a `DateTimeOffset`, so a value converter
  stores them as UTC `DateTime` (sortable). On SQL Server / Azure SQL this is native. Sort-by-due is
  covered by a test.
- **Completion invariant.** `CompletedAt` is set when a task becomes `Done` and cleared when it
  leaves `Done`; `UpdatedAt` bumps on every edit. Both are tested.
- **Optimistic updates** snapshot per query-key and roll back on error; the cache reconciles with the
  server after the mutation settles.

## Logging & auditability

Security-relevant actions are logged through the built-in `ILogger` under a dedicated **`Audit`**
category (filterable via `Logging:LogLevel:Audit` in `appsettings.json`):

- **`Information`** — account registered, login succeeded, logout, task created / updated / deleted
  (logged by **`UserId` + `TaskId`**, never task content).
- **`Warning`** — login failures (by attempted email), and mutating requests that resolve to a 404
  (acting on a task that isn't yours or doesn't exist).

Passwords, hashes, and tokens are **never** logged. Auth events include the email by design (it's the
audit identity); everything else prefers IDs over PII. This is deliberately *application logging*, not a
metrics/tracing stack.

A **persistent, queryable audit trail** (immutable who-did-what rows, retained for later review) is
intentionally **not** built for this single-user scope — see *What I'd do next*.

## Tests

**21 integration tests** (xUnit + `WebApplicationFactory` against a temp SQLite DB), aimed at the two
highest-risk areas the brief names, plus key invariants:

- **Ownership** — User A's data is unreachable by User B via list / get / patch / delete (404 not
  403); unauthenticated requests are 401.
- **Validation** — empty / whitespace / 201-char titles, invalid enum, and malformed JSON all return
  the same 400 envelope; a valid task persists across a *fresh* `DbContext`.
- **Behavior** — completion-timestamp invariant, `UpdatedAt` bump, sort-by-due ordering,
  case-insensitive search, duplicate-registration and wrong-password handling.
- **Comments** — add/list/delete round-trip, empty body → 400, and User B can't read or add comments
  on User A's task (404).

## What I'd do next

- Swap the EF Core provider to **SQL Server / Azure SQL** (a connection-string + provider change; the
  data access is kept provider-agnostic) and deploy as a single container to **Azure** App
  Service / Container Apps with Azure SQL + Key Vault.
- Pagination as a user's task count grows; refresh tokens + revocation; a **Playwright** e2e of the
  core flow with an **axe-core** accessibility scan.
- A **persistent, queryable audit trail** (e.g. an EF Core `SaveChanges` interceptor writing immutable
  audit rows) if actions ever need to be reviewed after the fact — the production step up from the
  application-level audit logging that's in place now.
- **Multi-user collaboration (teams)** — let users create tasks for each other with **team-based
  visibility**: a `Team` + `TeamMembership` model, tasks gaining `TeamId` / `CreatedBy` / `Assignee`,
  team-scoped authorization replacing the per-user query filter, and a settings page to create teams
  and add members. Deliberately **not** built here — it would replace this app's single-owner security
  model (its most-tested property) with team RBAC, a real step beyond this scope.
- Recurring tasks.

## Project layout

```
api/     ASP.NET Core Minimal API
  Domain/         entities + enums
  Data/           DbContext, global query filter, value converters, migrations
  Auth/           current-user, JWT token service, cookie name
  Endpoints/      auth + task + comment endpoints
  Validation/     FluentValidation validators + endpoint filter
  Infrastructure/ error-envelope middleware, cookie writer, dev seeder
web/     Vue 3 + TS SPA
  src/views/        Login, Register, Tasks
  src/components/    TaskCard, TaskDetailModal (view/edit + comments), ToastHost
  src/stores/        auth, ui (toasts + recently-viewed)
  src/composables/   useTasks, useComments (TanStack Query + optimistic mutations)
  src/api/           fetch wrapper, auth + task + comment clients
tests/   xUnit integration tests
TaskManager.sln
```
