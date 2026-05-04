# Implementation Stories

| Field        | Value                  |
|--------------|------------------------|
| Run ID       | run-2026-05-05-001     |
| Version      | 1.0                    |
| Status       | draft                  |
| Author       | Tech Lead Agent        |
| Date         | 2026-05-05             |
| Linked Spec  | tech-spec.md v1.0      |

---

## Overview

This document decomposes `tech-spec.md` v1.0 into independently deliverable stories grouped by track (`dotnet` or `react`). Stories are sequenced so dotnet API stories can be implemented in parallel with frontend scaffolding, with the React integration stories depending on the corresponding API stories.

Tracks:
- `dotnet` — backend at `src/backend/`
- `react` — frontend at `src/frontend/`

Note: The `docker-compose.yml` and overall stack orchestration is owned jointly. It is placed in the `dotnet` track (STORY-007) because the backend depends on Postgres being healthy and EF migrations run on backend startup; the frontend image is wired in as a service consumer.

---

## STORY-001 — Backend persistence: EF Core DbContext, entity, and initial migration

| Field         | Value                                       |
|---------------|---------------------------------------------|
| Track         | dotnet                                      |
| Implements    | TECH-003                                    |
| Depends on    | —                                           |
| Complexity    | M                                           |
| Path          | `src/backend/Api/Data/`                     |

### Description
Create the EF Core 8 persistence layer for contact submissions: a single entity `ContactSubmission`, an `AppDbContext`, the Npgsql snake_case naming convention, and the initial migration that creates the `contact_submissions` table. Wire `Database.MigrateAsync()` invocation so migrations are applied on startup.

### Acceptance Criteria
1. `src/backend/Api/Data/Entities/ContactSubmission.cs` defines properties `Id (Guid)`, `FullName (string)`, `Email (string)`, `Phone (string)`, `Subject (string)`, `Message (string)`, `ReceivedAt (DateTime, UTC)`.
2. `src/backend/Api/Data/AppDbContext.cs` exposes `DbSet<ContactSubmission> ContactSubmissions` and configures column types: `FullName varchar(200) NOT NULL`, `Email varchar(320) NOT NULL`, `Phone varchar(50) NOT NULL`, `Subject varchar(200) NOT NULL`, `Message varchar(1000) NOT NULL`, `ReceivedAt timestamp with time zone NOT NULL` with default `now() at time zone 'utc'`.
3. `Id` is the primary key. The application sets `Guid.NewGuid()` on insert (or DB-side `gen_random_uuid()` is acceptable).
4. Snake_case naming convention is applied so the table is named `contact_submissions` and columns are snake_case (e.g. `full_name`, `received_at`).
5. An index on `received_at` exists in the initial migration.
6. An EF Core migration named `InitialCreate` exists under `src/backend/Api/Data/Migrations/` and produces the table when applied to a clean Postgres 16 instance.
7. The connection string is read from configuration key `ConnectionStrings:Default` (i.e. env var `ConnectionStrings__Default`).
8. A unit-style test or integration test (Testcontainers Postgres acceptable) demonstrates that calling `MigrateAsync()` against a clean database creates the table and that a `ContactSubmission` row can be inserted and retrieved with all fields round-tripping correctly.

---

## STORY-002 — Backend DTO and FluentValidation validator for contact submissions

| Field         | Value                                                       |
|---------------|-------------------------------------------------------------|
| Track         | dotnet                                                      |
| Implements    | TECH-002 (validation portion)                               |
| Depends on    | —                                                           |
| Complexity    | S                                                           |
| Path          | `src/backend/Api/Dtos/`, `src/backend/Api/Validators/`      |

### Description
Define the request DTO `ContactSubmissionRequest` and a FluentValidation validator enforcing the documented length and format rules.

### Acceptance Criteria
1. `src/backend/Api/Dtos/ContactSubmissionRequest.cs` defines string properties `FullName`, `Email`, `Phone`, `Subject`, `Message` with PascalCase and JSON serialization configured (or relied upon) to produce camelCase wire form (`fullName`, etc.).
2. `src/backend/Api/Validators/ContactSubmissionRequestValidator.cs` is a `FluentValidation.AbstractValidator<ContactSubmissionRequest>` that enforces:
   - `FullName`: NotEmpty, MaximumLength 200.
   - `Email`: NotEmpty, EmailAddress.
   - `Phone`: NotEmpty, MaximumLength 50.
   - `Subject`: NotEmpty, MaximumLength 200.
   - `Message`: NotEmpty, MaximumLength 1000.
3. Each rule has a human-readable error message (e.g. "Message must be 1000 characters or fewer.").
4. xUnit tests cover: (a) a fully valid payload passes; (b) each field individually failing produces a validation error keyed under that field; (c) a 1000-character message passes and a 1001-character message fails; (d) an invalid email format fails.

---

## STORY-003 — Backend POST /api/contact endpoint with persistence and error contract

| Field         | Value                                            |
|---------------|--------------------------------------------------|
| Track         | dotnet                                           |
| Implements    | TECH-002 (controller portion), TECH-005          |
| Depends on    | STORY-001, STORY-002                             |
| Complexity    | M                                                |
| Path          | `src/backend/Api/Controllers/ContactController.cs` |

### Description
Implement the public, anonymous `POST /api/contact` endpoint that validates the request, persists a `ContactSubmission`, and returns either a 201 with `{ id, receivedAt }` or a 400 with a structured `errors` dictionary. Define the wire-format contract so it matches what the frontend (TECH-005) expects.

### Acceptance Criteria
1. A controller (or minimal API endpoint) handles `POST /api/contact` and accepts `application/json`.
2. The endpoint has no `[Authorize]` attribute and no auth middleware applies to it.
3. On a valid payload: a `ContactSubmission` row is persisted with `ReceivedAt` set to `DateTime.UtcNow`, and the response is HTTP 201 with body `{ "id": "<uuid>", "receivedAt": "<ISO-8601 UTC>" }`.
4. On a payload that fails validation (any rule from STORY-002): the response is HTTP 400 with body shape `{ "errors": { "<camelCaseField>": ["<message>", ...] } }`. No row is persisted.
5. On an unhandled exception: the response is HTTP 500 with a sanitized JSON body (RFC 7807 problem details acceptable). No HTML stack trace is leaked.
6. Field keys in the `errors` dictionary use camelCase (`email`, `fullName`, `message`, `phone`, `subject`) to match the frontend mapping.
7. Integration test (Testcontainers Postgres or in-memory equivalent): (a) POST valid payload returns 201 and a row exists in the database; (b) POST invalid payload returns 400 with `errors` keyed correctly and no row inserted; (c) POST malformed JSON returns 400.

---

## STORY-004 — Backend host wiring: Program.cs, CORS, healthcheck, no auth

| Field         | Value                              |
|---------------|------------------------------------|
| Track         | dotnet                             |
| Implements    | TECH-004                           |
| Depends on    | STORY-001, STORY-002, STORY-003    |
| Complexity    | M                                  |
| Path          | `src/backend/Api/Program.cs`       |

### Description
Configure the ASP.NET Core 8 host to compose the controller, validation, EF Core, CORS policy, healthcheck, global exception handler, and conditional Swagger. Explicitly omit authentication and authorization registration.

### Acceptance Criteria
1. `Program.cs` calls `AddControllers()` and registers FluentValidation auto-validation for the controller pipeline.
2. `Program.cs` registers `AppDbContext` via `AddDbContext<AppDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("Default")))`.
3. A CORS policy named `FrontendPolicy` is registered allowing the origin from env var `FRONTEND_ORIGIN` (default `http://localhost:3000`), permitting `POST` and headers `Content-Type`. `app.UseCors("FrontendPolicy")` is in the pipeline before endpoint routing of the controller.
4. `MapHealthChecks("/healthz")` is registered and returns HTTP 200 when the app is alive.
5. `app.UseAuthentication()` and `app.UseAuthorization()` are NOT called. No `AddAuthentication`/`AddAuthorization` is registered.
6. On startup, the application invokes `dbContext.Database.MigrateAsync()` (e.g. inside a startup-time scope) so migrations apply on first run.
7. Swagger/OpenAPI is enabled only when `app.Environment.IsDevelopment()`; disabled in `Production`.
8. A global exception handler is wired so unhandled exceptions return HTTP 500 with sanitized JSON body, never HTML.
9. `ASPNETCORE_URLS` is honoured (default binding `http://+:8080` inside container).
10. A smoke test (or integration test) confirms: `GET /healthz` returns 200; `OPTIONS /api/contact` from origin `http://localhost:3000` returns CORS-allow headers.

---

## STORY-005 — Backend Dockerfile

| Field         | Value                                  |
|---------------|----------------------------------------|
| Track         | dotnet                                 |
| Implements    | TECH-004 (deployment artifact portion) |
| Depends on    | STORY-004                              |
| Complexity    | S                                      |
| Path          | `src/backend/Dockerfile`               |

### Description
Create a multi-stage Dockerfile for the backend that builds the .NET 8 API and produces a runtime image listening on port 8080.

### Acceptance Criteria
1. `src/backend/Dockerfile` uses an SDK image (e.g. `mcr.microsoft.com/dotnet/sdk:8.0`) for the build stage and a runtime image (e.g. `mcr.microsoft.com/dotnet/aspnet:8.0`) for the final stage.
2. The final image runs the API listening on port 8080 (exposes 8080).
3. The build stage restores and publishes the `Api.csproj` in `Release` configuration.
4. `docker build -f src/backend/Dockerfile src/backend` succeeds and the resulting image, when run with required env vars and a reachable Postgres, responds 200 to `GET /healthz`.

---

## STORY-006 — Backend xUnit test project scaffolding

| Field         | Value                                    |
|---------------|------------------------------------------|
| Track         | dotnet                                   |
| Implements    | TECH cross-cutting (testing) supporting TECH-002, TECH-003 |
| Depends on    | STORY-001, STORY-002, STORY-003          |
| Complexity    | S                                        |
| Path          | `src/backend/Api.Tests/` (or sibling test project) |

### Description
Create an xUnit test project that hosts the unit tests for STORY-002 (validator) and the integration tests for STORY-001 (persistence) and STORY-003 (endpoint). This story exists to ensure a real test runner target is wired and discoverable in CI; the test cases themselves live in their respective stories' acceptance criteria.

### Acceptance Criteria
1. An xUnit test project exists under the backend solution and references `Api.csproj`.
2. The project compiles and `dotnet test` discovers and runs all tests added by STORY-001, STORY-002, and STORY-003.
3. Testcontainers (or equivalent) is configured for the Postgres-backed integration tests.

---

## STORY-007 — Docker Compose orchestration (db + backend + frontend)

| Field         | Value                                                       |
|---------------|-------------------------------------------------------------|
| Track         | dotnet                                                      |
| Implements    | TECH-004 (deployment topology), supports TECH-001, TECH-002 |
| Depends on    | STORY-005, STORY-010 (frontend Dockerfile)                  |
| Complexity    | M                                                           |
| Path          | `docker-compose.yml` (repo root)                            |

### Description
Author a single `docker-compose.yml` defining the three services (`db`, `backend`, `frontend`) on a user-defined bridge network with the documented ports, env vars, healthchecks, volumes, and depends-on conditions.

### Acceptance Criteria
1. `docker-compose.yml` exists at the repo root and defines services `db`, `backend`, and `frontend`.
2. `db` uses image `postgres:16-alpine`, sets `POSTGRES_DB=contactdb`, `POSTGRES_USER=contact_user`, `POSTGRES_PASSWORD=contact_pw`, mounts volume `db_data:/var/lib/postgresql/data`, and has healthcheck `pg_isready -U contact_user -d contactdb` (interval 5s, retries 10).
3. `backend` builds from `src/backend/`, exposes 8080 -> host 8080, sets `ASPNETCORE_ENVIRONMENT=Production`, `ASPNETCORE_URLS=http://+:8080`, `ConnectionStrings__Default=Host=db;Port=5432;Database=contactdb;Username=contact_user;Password=contact_pw`, `FRONTEND_ORIGIN=http://localhost:3000`, depends on `db` with condition `service_healthy`, and has healthcheck `GET http://localhost:8080/healthz` (interval 10s, retries 6, start_period 20s).
4. `frontend` builds from `src/frontend/`, exposes container port 80 -> host port 3000, sets `VITE_API_BASE_URL=/api` at build time, depends on `backend`, and has healthcheck `GET http://localhost:80/` (interval 10s).
5. All services share a user-defined bridge network named `contact-net`.
6. A named volume `db_data` is declared at the top level.
7. `docker compose up -d` against a clean checkout brings all three services to a healthy state. `curl http://localhost:8080/healthz` returns 200. `curl http://localhost:3000/` returns 200 with the SPA HTML.
8. A smoke run posts a valid contact submission via `curl -X POST http://localhost:8080/api/contact` and receives a 201 response.

---

## STORY-008 — Frontend project scaffold (Vite + React 18 + TypeScript strict)

| Field         | Value                          |
|---------------|--------------------------------|
| Track         | react                          |
| Implements    | TECH-001 (scaffold portion)    |
| Depends on    | —                              |
| Complexity    | S                              |
| Path          | `src/frontend/`                |

### Description
Initialize the React 18 + Vite + TypeScript project with strict mode, the documented file layout, and a single route.

### Acceptance Criteria
1. `src/frontend/package.json` declares dependencies on `react@18`, `react-dom@18`, `vite`, `typescript`, and dev dependencies for `vitest` and `@testing-library/react`.
2. `src/frontend/tsconfig.json` enables `"strict": true`.
3. `src/frontend/vite.config.ts` configures dev server on port 3000 and a dev proxy from `/api` to `http://backend:8080`.
4. `src/frontend/src/main.tsx` mounts `<App />` into `#root`.
5. `src/frontend/src/App.tsx` renders `<ContactUsPage />` (placeholder is acceptable here; real implementation lands in STORY-011). The app has no router protected area and no login route.
6. `npm run build` produces a bundle in `dist/` without errors.
7. `npm run dev` serves the SPA on `http://localhost:3000`.

---

## STORY-009 — Frontend client-side validation schema

| Field         | Value                                     |
|---------------|-------------------------------------------|
| Track         | react                                     |
| Implements    | TECH-001 (validation portion)             |
| Depends on    | STORY-008                                 |
| Complexity    | S                                         |
| Path          | `src/frontend/src/validation/contactSchema.ts` |

### Description
Implement the client-side validation rules for the contact form, exposed as a pure module so unit tests can target it without rendering React.

### Acceptance Criteria
1. `contactSchema.ts` exports a `validateContactForm(input)` function (or Zod schema with equivalent behaviour) that returns either a success result or a `Record<fieldName, string>` of inline error messages.
2. Rules enforced match the tech spec exactly:
   - `fullName`: required, trimmed length >= 1, max 200.
   - `email`: required, must match an RFC 5322-style regex.
   - `phone`: required, trimmed length >= 1, max 50.
   - `subject`: required, trimmed length >= 1, max 200.
   - `message`: required, trimmed length >= 1, max 1000.
3. Vitest unit tests cover: (a) all-valid input passes; (b) each missing field yields an inline error keyed under that field; (c) `message` of length 1000 is valid and 1001 is invalid; (d) malformed emails (`"foo"`, `"foo@"`, `"foo@bar"`) are rejected and a basic well-formed email is accepted.

---

## STORY-010 — Frontend Docker image (multi-stage Node + Nginx)

| Field         | Value                                 |
|---------------|---------------------------------------|
| Track         | react                                 |
| Implements    | TECH-001 (deployment artifact)        |
| Depends on    | STORY-008                             |
| Complexity    | S                                     |
| Path          | `src/frontend/Dockerfile`, `src/frontend/nginx.conf` |

### Description
Author the multi-stage Dockerfile (`node:20-alpine` build → `nginx:alpine` runtime) and the Nginx config that serves the SPA on port 80 and proxies `/api` to the backend service.

### Acceptance Criteria
1. `src/frontend/Dockerfile` has two stages: a build stage on `node:20-alpine` that runs `npm ci && npm run build`, and a runtime stage on `nginx:alpine` that copies `dist/` into the Nginx html root.
2. `src/frontend/nginx.conf` configures Nginx to listen on port 80, serve the SPA with SPA fallback (`try_files $uri /index.html`), and proxy `location /api/` to `http://backend:8080/api/`.
3. The build accepts `VITE_API_BASE_URL` as a build arg/env so the bundle is built with the correct base URL (default `/api`).
4. `docker build -f src/frontend/Dockerfile src/frontend` succeeds. Running the resulting image and `curl http://localhost/` returns 200 with the SPA HTML; `curl http://localhost/api/contact` is forwarded to the backend service (when running under compose).

---

## STORY-011 — ContactUsPage, ContactForm, and CharCounter components

| Field         | Value                                                                                      |
|---------------|--------------------------------------------------------------------------------------------|
| Track         | react                                                                                      |
| Implements    | TECH-001 (UI portion)                                                                      |
| Depends on    | STORY-008, STORY-009                                                                       |
| Complexity    | L                                                                                          |
| Path          | `src/frontend/src/pages/ContactUsPage.tsx`, `src/frontend/src/components/ContactForm.tsx`, `src/frontend/src/components/CharCounter.tsx` |

### Description
Build the single page of the SPA: a contact form with five controlled inputs, inline field errors, a live character counter for the message textarea, success and error banners, and a loading state on the submit button.

### Acceptance Criteria
1. `ContactUsPage` is the only page in the application and renders without making any network request on mount.
2. `ContactForm` renders five inputs: `fullName` (text), `email` (email), `phone` (tel), `subject` (text), `message` (textarea). All are controlled.
3. On `change` of any field, the form runs field-level validation from STORY-009 and displays an inline error message immediately under that field if invalid.
4. `CharCounter` is rendered beneath the message textarea and shows `"<currentLength> / 1000"`. Its visual state turns red when `currentLength > 1000`.
5. On submit: full validation runs first; if any field is invalid, submission is blocked and the inline error messages are displayed.
6. While a valid submission is in flight, the submit button shows a loading state and is disabled.
7. On HTTP 200/201 from the API: the form fields are cleared and a success banner is shown on `ContactUsPage`.
8. On HTTP 400 from the API: the response `errors` map is mapped onto inline field errors (keys `fullName`, `email`, `phone`, `subject`, `message`).
9. On network failure or HTTP >= 500: a generic error banner with text `"We couldn't send your message. Please try again later."` is displayed.
10. Vitest + React Testing Library tests cover at minimum: (a) submit blocked when fields empty and inline errors shown; (b) live counter updates as the user types and turns red over the limit; (c) successful submission clears the form and shows the success banner (mocked client); (d) 400 from server maps into inline errors (mocked client); (e) 500 from server shows the generic error banner (mocked client); (f) network error shows the generic error banner.
11. The page contains no link to or reference to a login or sign-up page.

---

## STORY-012 — Frontend API client (`submitContact`)

| Field         | Value                                          |
|---------------|------------------------------------------------|
| Track         | react                                          |
| Implements    | TECH-001 (api client portion), TECH-005        |
| Depends on    | STORY-008, STORY-003                           |
| Complexity    | S                                              |
| Path          | `src/frontend/src/api/contactClient.ts`        |

### Description
Implement the typed `submitContact(payload)` wrapper around `fetch` that encodes the contract in TECH-005: success on 200/201, validation errors on 400, generic failure on network errors and 5xx.

### Acceptance Criteria
1. `contactClient.ts` exports `submitContact(payload: ContactSubmissionRequest): Promise<SubmitResult>` where `SubmitResult` is a discriminated union with variants `{ kind: 'success', id: string, receivedAt: string }`, `{ kind: 'validation', errors: Record<string, string[]> }`, and `{ kind: 'failure' }`.
2. The client POSTs to `${import.meta.env.VITE_API_BASE_URL ?? '/api'}/contact` with `Content-Type: application/json` and a JSON-encoded body.
3. HTTP 200 or 201: returns `{ kind: 'success', id, receivedAt }` parsed from response JSON.
4. HTTP 400: returns `{ kind: 'validation', errors }` parsed from response body.
5. Network error (fetch rejects) or HTTP status >= 500: returns `{ kind: 'failure' }`.
6. Vitest tests with `fetch` mocked verify each of the four branches (200/201 success, 400 validation, 500 failure, network rejection).

---

## STORY-013 — End-to-end smoke test against running compose stack

| Field         | Value                                            |
|---------------|--------------------------------------------------|
| Track         | react                                            |
| Implements    | TECH-005 (end-to-end verification)               |
| Depends on    | STORY-007, STORY-011, STORY-012                  |
| Complexity    | M                                                |
| Path          | `src/frontend/tests/e2e/`                        |

### Description
Add a single Playwright (or equivalent) end-to-end smoke test that boots against the running compose stack, fills out the contact form, submits it, and asserts the success banner appears and a row exists in Postgres.

### Acceptance Criteria
1. A Playwright (or equivalent) test exists under `src/frontend/tests/e2e/` and is runnable via `npm run test:e2e`.
2. The test navigates to `http://localhost:3000/`, fills all five fields with valid values, clicks submit, and asserts the success banner text is visible.
3. The test (or a follow-up step) verifies a corresponding row exists in `contact_submissions` (either by querying Postgres directly or via a debug-only readback endpoint — direct query preferred).
4. The test runs cleanly when invoked after `docker compose up -d` against a clean checkout.

---

## Coverage Self-Check

| TECH-ID  | Covered by stories                                           |
|----------|--------------------------------------------------------------|
| TECH-001 | STORY-008, STORY-009, STORY-010, STORY-011, STORY-012        |
| TECH-002 | STORY-002, STORY-003                                         |
| TECH-003 | STORY-001                                                    |
| TECH-004 | STORY-004, STORY-005, STORY-007                              |
| TECH-005 | STORY-003, STORY-012, STORY-013                              |

All five TECH-IDs are covered by at least one story. Each story belongs to exactly one track. React stories that depend on backend behaviour list the corresponding dotnet story under `Depends on`.
