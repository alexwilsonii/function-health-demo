# Task Manager

This is a small task management application built on an **ASP.NET Core (.NET 8) API** backed by a simple **SQLite** 
on database and with a **Vue 3 + TypeScript** (Vite, Pinia, TanStack Query) frontend.

The app has a limited scope with auth, **team-based collaboration** (tasks
belong to teams; members share visibility and can be assigned work), full CRUD with optimistic UI,
validation with inline errors, search/filter/sort, and tests on the parts that carry risk. 

To begin with, I immediately thought of this as a "Mantis Bug Tracker-esque" or "Jira Ultra Lite-esque" type
of project vs a simple "ToDo List" application. The guide seems to caution on not over-scoping or over-engineering
the project, which makes me think maybe a simple to do list was the intent, but the more I worked on this and
thought about it, the more I felt that semantics matter and that "task manager" sounds like something that would
at the very least be expected to be something used by multiple people in collaboration vs one person working alone.
This was when I added the Teams concept (more below). My original and first commits were more designed towards the
individual but set up in such a way that adding more features like Teams could be done later. 

If I wanted to spend even a couple more hours on this, I'd set up an activity history (who changed what on which task)
and email notifications for the tasks. I'd also likely work in concepts like "Releases" and/or "Road Maps" so that tasks
could be grouped into Jira style "Epics". Again though, I intentionally kept this assessment more limited in scope in terms
of direct user features added.

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

Open **http://localhost:5173** and either register an account or use a seeded demo login:

> **Demo logins (development only):** `demo@example.com` and `teammate@example.com`, both
> `Password123!`. They share an **"Acme Web Team"** (with tasks cross-assigned between them) and each
> has a private **Personal** team — log in as either to see both sides of team collaboration. Seeded
> on first startup locally or in dev **only**.

No secrets to set, no HTTPS dev cert; dev runs over plain `http://localhost` for now.

**Run the tests:**

```bash
dotnet test
```
---

## What I built

- **Auth** — register, login, logout, current-user. The JWT lives in an **httpOnly cookie**;
  sessions survive a refresh, and an expired session takes users to the login page.
- **Task CRUD** — create, list, edit, delete, and one-click **status cycling** and **"pinned"** toggling.
  Every mutation updates the list **optimistically** and **rolls back with a toast** if the server
  rejects it.
- **Teams & assignment** — every task belongs to a team and team members share visibility. A **Teams**
  page lets you create teams, add members **by email**, and leave/delete. Tasks can be **assigned** to
  any member of the Team and filtered by team and assignee. Each user has a private **Personal** team created on sign-up.
  Editing and commenting are open to any member of the Team; **only the creator can delete a task**.
- **Detail view + comments** — clicking a task (anywhere on the card) opens a read-only **view**
  modal; a pencil flips it to edit. Each task has a **comments** thread at the bottom of the view,
  lazily loaded, with add/delete. There is no editing of comments at this time.
- **Search / filter / sort** — search title+notes, filter by status and priority, sort by due date,
  priority, or recency. **Pinned** tasks float to the top; **overdue** tasks are flagged.
- **Validation** — server-side rules surfaced as **inline field errors**; the form keeps your input
  on failure.
- **Time zones** — due dates are stored in **UTC** and rendered in your **local** time.
- **Accessible & responsive** — keyboard-operable throughout, a focus-trapped create/edit dialog
  (`Esc` to close, focus restored on close), `aria-live` announcements for validation and toasts,
  status/priority/overdue conveyed by **text + icon, never color alone**, and a layout that reflows
  from ~320px to desktop.

## What I deliberately left out — and why

The guide mentions matching the solution to the size of the problem. These were considered and cut at this time:

- **Activity history, tags, bulk actions** — each is more surface area and another "claim it works
  end to end," with low marginal value and high risk to *finishedness*. (Bulk actions also hide
  partial-failure semantics that deserve to be done properly or not at all.) 
- **Client-generated IDs / idempotency keys** — IDs are server-generated and the submit button is
  disabled while saving. The idempotency-key pattern is what I'd add for an offline-capable client
  in case of potential Internet connection issues.
- **CI/CD, Docker, deployment config, metrics/tracing/monitoring infrastructure** — explicitly not
  evaluated. (Security-relevant actions *are* logged via the built-in logger — see **Logging &
  auditability** below — but there's no metrics/tracing/dashboard stack.)
- **Password reset / email verification, refresh tokens / token revocation** — out of scope (see the
  auth caveat below).
- **Microservices / event-driven decomposition** — A single-entity task manager is one process, one
  DB, one deploy. Splitting it would be over-engineering for a task this size. If it grew into
  separate bounded contexts (tasks, teams, notifications) I'd split along those seams and introduce
  events for cross-context reactions.

## Auth, teams & access control

- JWT issued by the API in an **httpOnly + `SameSite=Strict`** cookie (`Secure` in production; off
  for `http://localhost` in dev). Passwords hashed with **BCrypt**. The SPA and API are same-origin
  (Vite proxy in dev, `wwwroot` in prod), so `SameSite=Strict` closes the CSRF vector. A plain JWT
  isn't server-revocable before it expires; logout clears the cookie. Production would use short-lived
  access + refresh tokens, or a server-side denylist.
- **Visibility is team-based.** Every task belongs to a team, and you can see a task only if you're a
  member of its team. Each user gets a private **Personal** team on registration, so solo use is just
  "a team of one." 
- **Permissions:** any member can view / edit / comment / change status on a team's tasks; **only the
  task's creator can delete it**. A task you can't see returns **404** (no existence leak for security);
  a teammate who *can* see a task but isn't its creator gets **403** on delete. Members are added **by email**;
  you can leave a team while others remain, or delete one you're the sole member of.

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

- **`Information`** — account registered, login / logout, task created / updated / deleted, comments,
  and team membership changes (logged by **IDs**, never task/comment content).
- **`Warning`** — login failures (by attempted email), and mutating requests that resolve to a 404
  (acting on a task not in one of your teams, or one that doesn't exist).

Passwords, hashes, and tokens are **never** logged. Auth events include the email by design (it's the
audit identity); everything else prefers IDs over PII. This is deliberately *application logging*, not a
metrics/tracing stack.
> I added the above largely due to having working in SOC2 or HIPPA heavy environments in the past and needing
> to know who did what and when in terms of actions that a user could take in a UI.

A **persistent, queryable audit trail** is intentionally **not** built at this scope — see *What I'd do next*.

## Tests

**30 integration tests** (xUnit + `WebApplicationFactory` against a temp SQLite DB), aimed at the two
highest-risk areas the brief names, plus key invariants:

- **Team visibility** — a member can see and edit a teammate's task; a non-member gets 404 (no leak);
  a teammate who isn't the creator gets 403 on delete; unauthenticated requests are 401.
- **Teams & assignment** — Personal team auto-created on registration; add-member-by-email grants
  visibility; you can't add to the Personal team / add an unknown email / add to a team you're not in;
  leave-rules and sole-member delete; an assignee must be a team member.
- **Validation** — empty / whitespace / 201-char titles, missing team, invalid enum, and malformed
  JSON all return the same 400 envelope; a valid task persists across a *fresh* `DbContext`.
- **Behavior** — completion-timestamp invariant, `UpdatedAt` bump, sort-by-due ordering,
  case-insensitive search, duplicate-registration and wrong-password handling.
- **Comments** — add/list/delete round-trip with author, empty body → 400, and a non-member can't read
  or add comments on a task (404).

## What I'd do next

- Swap the EF Core provider to **SQL Server / Azure SQL** and deploy as a single container to **Azure** App
  Service / Container Apps with Azure SQL + Key Vault.
- Pagination as a user's task count grows.
- Refresh tokens + revocation.
- Playwright e2e UI testing of the core flow.
- A **persistent, queryable audit trail** (e.g. an EF Core `SaveChanges` interceptor writing immutable
  audit rows) if actions ever need to be reviewed after the fact. Something that would persist over multiple
  instances, startups, etc. and stored someplace safe.
- A team **invite/acceptance flow** (today add-by-email adds people immediately), the ability to
  **remove** other members, and **per-user pins** (a pin is currently shared on the task accross a Team).
  Right now, anyone can just add anyone to a team and while the just-added user can simply leave the team, it
  would be better to allow users to accept or deny being added to a Team in the first place.
- Email notifications. I would like to see email notifications on task creation, deletion, updates (assignment,
  comments, etc), but intentionally left out email server handling for this task and scope.
- Recurring tasks. Tasks that are auto-created by the server on some routine like weekly, daily, etc. 

## Project layout

```
api/     ASP.NET Core Minimal API
  Domain/         entities + enums
  Data/           DbContext, value converters, migrations
  Auth/           current-user, team-membership scoping, JWT token service, cookie name
  Endpoints/      auth + team + task + comment endpoints
  Validation/     FluentValidation validators + endpoint filter
  Infrastructure/ error-envelope middleware, cookie writer, dev seeder
web/     Vue 3 + TS SPA
  src/views/        Login, Register, Tasks, Teams
  src/components/    TaskCard, TaskDetailModal (view/edit + comments), ToastHost
  src/stores/        auth, ui (toasts + recently-viewed)
  src/composables/   useTasks, useComments, useTeams (TanStack Query + optimistic mutations)
  src/api/           fetch wrapper, auth + team + task + comment clients
tests/   xUnit integration tests
TaskManager.sln
```
