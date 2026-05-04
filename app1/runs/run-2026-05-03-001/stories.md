# Implementation Stories

| Field       | Value                    |
|-------------|--------------------------|
| Run ID      | run-2026-05-03-001       |
| Version     | 1.0                      |
| Status      | draft                    |
| Author      | Tech Lead Agent          |
| Date        | 2026-05-03               |
| Source Spec | tech-spec.md v1.0        |

---

## Overview

This document decomposes `tech-spec.md` v1.0 into independently deliverable
stories. Stories are split across two tracks:

- **dotnet** — .NET 8 Web API + EF Core 8 + PostgreSQL
- **react** — React 18 + Vite + TypeScript SPA

Track ordering: dotnet stories that expose the contact submission API must be
delivered before the react stories that consume it. Stories within a single
track can largely proceed in parallel where dependencies allow.

Estimation legend: **S** = ~0.5 day, **M** = ~1 day, **L** = ~2 days.

---

## STORY-001 — Contact Submission Persistence Layer

**Track:** dotnet
**Implements:** TECH-005
**Depends on:** (none)
**Estimate:** M

**Description**
Stand up the `ContactDbContext` (EF Core 8 + Npgsql) with a single
`DbSet<ContactSubmission>`, mapped to the PostgreSQL table
`contact_submissions`. Generate the `InitialCreate` migration that produces
the schema described in TECH-005, including the `received_at` index.
Submissions are append-only — no update or delete code paths.

**Acceptance Criteria**
- AC1: A `ContactSubmission` entity exists with properties `Id` (Guid),
  `FullName` (string), `Email` (string), `Message` (string),
  `ReceivedAt` (DateTime) matching TECH-005.
- AC2: A `ContactDbContext` exposes `DbSet<ContactSubmission> Submissions`
  and configures column types: `full_name varchar(200) NOT NULL`,
  `email varchar(320) NOT NULL`, `message text NOT NULL`,
  `received_at timestamptz NOT NULL`, `id uuid PRIMARY KEY`.
- AC3: An EF Core migration named `InitialCreate` is committed under
  `backend/.../Migrations/` and, when applied to an empty PostgreSQL 16
  database, produces the table and the index `ix_contact_submissions_received_at`.
- AC4: An xUnit integration test using Testcontainers PostgreSQL inserts
  one `ContactSubmission` and asserts a single row with the expected column
  values is read back.
- AC5: The DbContext does not expose `Update`/`Remove` helpers; the test
  suite verifies that no production code path calls `Submissions.Remove(...)`
  or `Update(...)` (grep-based test or design-by-convention check).

---

## STORY-002 — Contact Submission API Endpoint with Validation

**Track:** dotnet
**Implements:** TECH-004
**Depends on:** STORY-001
**Estimate:** M

**Description**
Expose the minimal-API endpoint `POST /api/contact` that accepts a
`ContactSubmissionRequest`, server-side-validates it, persists a new row
via `ContactDbContext`, and returns the structured responses defined in
TECH-004. Also expose `GET /api/health` returning `200 { "status": "ok" }`
for compose healthchecks. The endpoint is registered without `[Authorize]`.

**Acceptance Criteria**
- AC1: `POST /api/contact` with a valid JSON body
  `{ "fullName": "...", "email": "a@b.c", "message": "..." }` returns
  HTTP 201 with body `{ "id": "<guid>", "receivedAt": "<ISO-8601>" }`
  and a row appears in `contact_submissions` with the trimmed values.
- AC2: `POST /api/contact` with an empty `fullName` returns HTTP 400
  with body `{ "errors": { "fullName": ["..."] } }` and persists no row.
- AC3: `POST /api/contact` with `email = "not-an-email"` returns HTTP 400
  with `errors.email` populated; the regex `^[^\s@]+@[^\s@]+\.[^\s@]+$`
  is the source of truth.
- AC4: `POST /api/contact` with `message` length 5001 returns HTTP 400
  with `errors.message` populated.
- AC5: A request with multiple invalid fields returns HTTP 400 with all
  offending fields present as keys in `errors`.
- AC6: When the persistence layer throws, the response is HTTP 500 with
  body `{ "error": "An unexpected error occurred." }` (via global
  `UseExceptionHandler`).
- AC7: `GET /api/health` returns HTTP 200 with body `{ "status": "ok" }`.
- AC8: An xUnit test suite covers AC1–AC7. JSON property casing is
  camelCase on both request and response sides.
- AC9: A smoke test asserts the application pipeline does **not** register
  authentication or authorization middleware (no `UseAuthentication`,
  no `UseAuthorization`).

---

## STORY-003 — Backend Composition, CORS, and Startup Migration

**Track:** dotnet
**Implements:** TECH-006, startup-migration aspect of TECH-005
**Depends on:** STORY-001, STORY-002
**Estimate:** S

**Description**
Wire `Program.cs` to register `ContactDbContext` with Npgsql using
`ConnectionStrings__DefaultConnection`, configure the named CORS policy
`frontend` reading allowed origin from `Cors__AllowedOrigin`
(default `http://localhost:5173`), apply EF Core migrations on startup
within a 5-attempt / 2-second-backoff retry loop, and ensure pipeline
order is `UseExceptionHandler` -> `UseCors("frontend")` -> endpoint mapping.

**Acceptance Criteria**
- AC1: Setting `ConnectionStrings__DefaultConnection` via environment
  variable overrides the `appsettings.json` default (verified by an
  integration test or a documented manual repro).
- AC2: On startup against an empty PostgreSQL instance, the API applies
  pending migrations and the `contact_submissions` table exists before
  the first request is served.
- AC3: If PostgreSQL is unreachable on the first attempt, the migration
  runner retries up to 5 times with ~2s backoff and succeeds once the DB
  is reachable; on permanent failure it logs and rethrows.
- AC4: A preflight `OPTIONS /api/contact` from origin
  `http://localhost:5173` returns 204 with
  `Access-Control-Allow-Origin: http://localhost:5173` and
  `Access-Control-Allow-Methods` including `POST`. A request from a
  disallowed origin does **not** receive that ACAO header.
- AC5: An xUnit smoke test starts the app via `WebApplicationFactory` and
  asserts the registered CORS policy named `frontend` matches the
  configured origin.
- AC6: Each successful submission emits one `Information` log entry of
  the form `Contact submission accepted: {Id}` containing **no** name,
  email, or message values.

---

## STORY-004 — Backend Dockerfile and Compose Service `api`

**Track:** dotnet
**Implements:** deployment aspects of TECH-006 (api service)
**Depends on:** STORY-003
**Estimate:** S

**Description**
Author a multi-stage Dockerfile under `./backend` that builds the .NET 8
Web API and produces a runtime image listening on port 8080. Add the
`api` service to `docker-compose.yml` with the env vars, healthcheck,
and `depends_on: db (service_healthy)` per the Deployment Topology
section of the tech-spec.

**Acceptance Criteria**
- AC1: `docker build ./backend` succeeds and produces an image whose
  default command starts the API on `http://+:8080`.
- AC2: `docker compose up db api` results in the `api` container becoming
  healthy via `GET /api/health` within 60 seconds on a clean machine.
- AC3: With the compose stack running, `curl -X POST http://localhost:8080/api/contact`
  with a valid body returns HTTP 201 and a row is persisted in the
  `db` container's `contact_submissions` table.
- AC4: The compose `api` service sets every env var listed in the
  "Required Environment Variables" table for `api`
  (`ASPNETCORE_ENVIRONMENT`, `ASPNETCORE_URLS`,
  `ConnectionStrings__DefaultConnection`, `Cors__AllowedOrigin`).

---

## STORY-005 — PostgreSQL Compose Service `db`

**Track:** dotnet
**Implements:** deployment aspects of TECH-005 (db service)
**Depends on:** (none)
**Estimate:** S

**Description**
Add the `db` service to `docker-compose.yml` using `postgres:16-alpine`,
with the named volume `pgdata`, the documented env vars, the
`pg_isready` healthcheck, and host port mapping `5432:5432`. This story
is independent of the .NET code and can land first.

**Acceptance Criteria**
- AC1: `docker compose up db` starts a healthy PostgreSQL 16 container
  exposing `5432` on the host.
- AC2: `pg_isready -U appuser -d contactdb` succeeds inside the container
  within 30 seconds of startup.
- AC3: After `docker compose down` (without `-v`) and `docker compose up db`
  again, data written to the `contactdb` database persists (volume
  `pgdata` retained).
- AC4: After `docker compose down -v`, the volume is removed and the next
  `up` starts with an empty database.

---

## STORY-006 — Frontend API Client Module

**Track:** react
**Implements:** TECH-003
**Depends on:** STORY-002 (contract only — module can be implemented against
the contract before STORY-002 lands; end-to-end run requires STORY-002)
**Estimate:** S

**Description**
Implement `src/api/contactApi.ts` exporting
`submitContact(payload): Promise<SubmitResult>` per TECH-003. The base
URL is read from `import.meta.env.VITE_API_BASE_URL`. The function maps
HTTP outcomes to the discriminated union `SubmitResult`.

**Acceptance Criteria**
- AC1: A 201 response resolves to `{ kind: 'ok' }`.
- AC2: A 400 response with body `{ "errors": { "email": ["..."] } }`
  resolves to `{ kind: 'validation', fieldErrors: { email: ["..."] } }`.
- AC3: A 5xx response resolves to `{ kind: 'error', message: <string> }`
  with a non-empty message.
- AC4: A network failure (fetch rejects) resolves to
  `{ kind: 'error', message: <string> }` and does not throw to the caller.
- AC5: The request uses `POST`, `Content-Type: application/json`, and
  serializes the payload with the keys `fullName`, `email`, `message`.
- AC6: Vitest tests using a mocked `fetch` cover AC1–AC5.
- AC7: The module reads `import.meta.env.VITE_API_BASE_URL` (verified by
  a test that sets the value via Vite env mocking).

---

## STORY-007 — Contact Form Component with Client-Side Validation

**Track:** react
**Implements:** TECH-002
**Depends on:** STORY-006
**Estimate:** L

**Description**
Build `<ContactForm>`, a controlled component with `fullName`, `email`,
and `message` fields. Validate on blur and on submit per TECH-002 rules.
Block the network call when invalid; render field-level error messages
adjacent to invalid fields. On valid submit, call `submitContact` from
STORY-006 and surface a `SubmitResult` to the parent via callback.

**Acceptance Criteria**
- AC1: Each of the three fields is rendered with a visible required marker
  and `aria-required="true"`.
- AC2: Submitting the form with all fields blank does **not** call
  `submitContact`; three field-level error messages are rendered, one per
  field, each uniquely worded.
- AC3: Entering an `email` of `not-an-email` and tabbing out shows a
  field-level error "Please enter a valid email address." (or equivalent
  matching the same regex as TECH-002) on blur, before submit.
- AC4: A valid submission calls `submitContact` exactly once with the
  trimmed values.
- AC5: While the request is in flight, the submit button is disabled;
  it becomes enabled again after the response resolves.
- AC6: When `submitContact` returns
  `{ kind: 'validation', fieldErrors: { email: ["server says no"] } }`,
  the form renders `server says no` next to the email field.
- AC7: When `submitContact` returns `{ kind: 'ok' }`, the component clears
  its internal state and notifies the parent (e.g., `onSuccess()` callback).
- AC8: When `submitContact` returns `{ kind: 'error' }`, the component
  preserves all entered field values (REQ-001 AC5) and notifies the
  parent (`onError(message)`).
- AC9: Vitest + React Testing Library tests cover AC2, AC3, AC4, AC6,
  AC7, AC8.

---

## STORY-008 — Contact Us Page Route, Banners, and Discoverability Link

**Track:** react
**Implements:** TECH-001
**Depends on:** STORY-007
**Estimate:** M

**Description**
Define the `/contact` route in `App.tsx` using `react-router-dom` v6 and
render a Contact Us page that hosts `<ContactForm>`. The page surfaces
top-level submission state — a success confirmation (replacing the form,
with a "Send another message" link) and a non-destructive error banner
above the form. No route guard or auth check exists. Add a link from `/`
to `/contact` so the page is discoverable.

**Acceptance Criteria**
- AC1: Navigating directly to `/contact` renders the page; no redirect or
  guard runs (REQ-005).
- AC2: The landing page at `/` includes a visible link/button whose
  destination is `/contact`.
- AC3: After `<ContactForm>` reports success, the form is replaced by a
  confirmation message including a "Send another message" link/button
  that, when clicked, restores the empty form.
- AC4: When `<ContactForm>` reports a generic error, an error banner is
  rendered above the form and the form's previously entered values are
  still present in their inputs (verified by a Vitest test mocking
  `submitContact` to return `{ kind: 'error', ... }`).
- AC5: A Vitest test renders the route at `/contact` via a memory router
  and asserts that the `<ContactForm>` is in the document without any
  authentication interaction.

---

## STORY-009 — Frontend Build, Nginx Static Hosting, and Compose Service `web`

**Track:** react
**Implements:** TECH-007
**Depends on:** STORY-008, STORY-004
**Estimate:** M

**Description**
Add a multi-stage Dockerfile under `./frontend` that runs `vite build`
with `VITE_API_BASE_URL` as a build arg and serves `dist/` via
`nginx:alpine` with SPA fallback to `index.html`. Add the `web` service
to `docker-compose.yml` per the Deployment Topology section.

**Acceptance Criteria**
- AC1: `docker build --build-arg VITE_API_BASE_URL=http://localhost:8080 ./frontend`
  succeeds and produces an image listening on port 80.
- AC2: With `docker compose up`, `http://localhost:5173/` returns the SPA
  index page.
- AC3: Refreshing on `http://localhost:5173/contact` returns HTTP 200 with
  the SPA index page (SPA fallback works) — not a 404.
- AC4: From the SPA loaded at `http://localhost:5173`, submitting a valid
  contact form results in an HTTP 201 from `http://localhost:8080/api/contact`
  and a confirmation message appears (full end-to-end via the compose
  stack).
- AC5: Building with a different `VITE_API_BASE_URL` causes the SPA's
  network request target to change accordingly (verified by inspecting
  the built JS or via a smoke test).

---

## Dependency Graph (visual)

```
STORY-005 (db service)        [independent]
STORY-001 (persistence)
   |
   v
STORY-002 (api endpoint)
   |
   v
STORY-003 (composition + CORS + startup migration)
   |
   v
STORY-004 (api Dockerfile + compose api service)
   |
   |   STORY-006 (api client) ---+
   |                              |
   |                              v
   |                         STORY-007 (form component)
   |                              |
   |                              v
   |                         STORY-008 (page + banners)
   |                              |
   +-----------------+------------+
                     v
              STORY-009 (frontend Docker + compose web)
```

---

## TECH-ID Coverage Self-Check

| TECH-ID  | Covered By                       |
|----------|----------------------------------|
| TECH-001 | STORY-008                        |
| TECH-002 | STORY-007                        |
| TECH-003 | STORY-006                        |
| TECH-004 | STORY-002                        |
| TECH-005 | STORY-001, STORY-003 (startup migration), STORY-005 (db service) |
| TECH-006 | STORY-003, STORY-004             |
| TECH-007 | STORY-009                        |

Every TECH-ID from `tech-spec.md` v1.0 (TECH-001..TECH-007) appears in at
least one story's Implements list above.

---

## Notes for the Validator / Implementers

- **Track isolation** — every story is scoped to exactly one of `dotnet` or
  `react`. STORY-004, STORY-005, STORY-009 each touch `docker-compose.yml`,
  but each owns a different service block; ordering (STORY-005 first,
  STORY-004 next, STORY-009 last) avoids merge churn.
- **Parallelism** — STORY-001 and STORY-005 can start simultaneously.
  STORY-006 can be implemented against the API contract before STORY-002
  is merged; only STORY-007's end-to-end acceptance truly requires the
  live endpoint.
- **No clarification needed** — every TECH in the tech-spec was concrete
  enough to story-ize without flags.
