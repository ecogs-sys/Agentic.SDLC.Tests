# Technical Specification

| Field       | Value                  |
|-------------|------------------------|
| Run ID      | run-2026-05-05-001     |
| Version     | 1.0                    |
| Status      | draft                  |
| Author      | Architect Agent        |
| Date        | 2026-05-05             |
| Linked Spec | req-spec.md v1.1       |

---

## 1. Overview

This specification translates the requirement spec for the Contact Us application into a concrete .NET 8 + React 18 architecture. The system is composed of a single-page React frontend served via Vite (production build served by an Nginx-style static container), a .NET 8 Web API backend exposing a single public POST endpoint for contact submissions, and a PostgreSQL database for persisting submissions. There is no authentication, authorisation, identity, or session-state layer anywhere in the stack — all components are deliberately public for the Contact Us feature only.

The full system is orchestrated by `docker-compose` for local development and CI smoke testing.

---

## 2. Stack

| Layer        | Technology                                         |
|--------------|----------------------------------------------------|
| Frontend     | React 18 + Vite + TypeScript                       |
| Backend API  | .NET 8 Web API (ASP.NET Core, C# 12)               |
| ORM          | Entity Framework Core 8 (Npgsql provider)          |
| Database     | PostgreSQL 16                                      |
| Validation   | FluentValidation (backend) + Zod or native (frontend) |
| HTTP client  | `fetch` API (frontend)                             |
| Orchestration| Docker Compose v2                                  |

---

## 3. High-Level Architecture

```
+-------------------+        HTTPS/HTTP         +-----------------------+        TCP/5432       +------------------+
|  React 18 SPA     |  ---- POST /api/contact ->|  .NET 8 Web API       |  ---- EF Core ------> |  PostgreSQL 16   |
|  (Vite + TS)      |  <------ JSON response ---|  (ContactController)  |                       |  (contactdb)     |
+-------------------+                            +-----------------------+                       +------------------+
        :3000                                              :8080
```

Single browser-side flow: visitor opens the Contact Us page (only page in the SPA), fills the form, the form posts JSON to the backend, the backend validates and persists, the SPA shows a success or error banner.

---

## 4. Component Catalogue

### TECH-001 — Contact Us Page (React SPA)

**Component:** Frontend React application.
**Path:** `src/frontend/`
**Implements:** REQ-001, REQ-003, REQ-004

**Description:**
A Vite-bundled React 18 + TypeScript single-page application. The application has a single route `/` that renders the `ContactUsPage` component, which contains the contact form. There is no router-protected area, no auth layer, and no login route anywhere in the SPA (REQ-003 AC1).

**Key files:**
- `src/frontend/src/App.tsx` — root, renders `<ContactUsPage />`.
- `src/frontend/src/pages/ContactUsPage.tsx` — page shell, success/error banner state.
- `src/frontend/src/components/ContactForm.tsx` — controlled form with full-name, email, phone, subject, and message inputs.
- `src/frontend/src/components/CharCounter.tsx` — live remaining-character indicator (REQ-001 AC7).
- `src/frontend/src/api/contactClient.ts` — `submitContact(payload)` wrapper around `fetch`.
- `src/frontend/src/validation/contactSchema.ts` — client-side validation rules.
- `src/frontend/vite.config.ts` — dev server on port 3000, proxy `/api` -> `http://backend:8080` in dev.
- `src/frontend/Dockerfile` — multi-stage build: `node:20-alpine` -> `nginx:alpine`.
- `src/frontend/nginx.conf` — serves SPA on port 80 inside container, proxies `/api` to backend service.

**Form validation rules (client-side, REQ-001 AC4–7):**
| Field       | Rule                                                                 |
|-------------|----------------------------------------------------------------------|
| fullName    | required, trimmed length >= 1, max 200                               |
| email       | required, must match standard RFC 5322-style regex                   |
| phone       | required, trimmed length >= 1, max 50                                |
| subject     | required, trimmed length >= 1, max 200                               |
| message     | required, trimmed length >= 1, max 1000 characters                   |

**Behaviour:**
- On mount, no network call is made.
- On `change` of any field, run field-level validation; show inline error under that field if invalid.
- Live counter "X / 1000" beneath the message textarea; turns red at >1000 (REQ-001 AC6, AC7).
- On submit: re-run full validation; if any error, block submission and show inline messages (REQ-001 AC4, AC5, AC6).
- If valid, call `submitContact(payload)`; show a loading state on the submit button.
- On HTTP 200/201: clear form and display success banner (REQ-001 AC3).
- On HTTP 400 (validation error from backend): map server `errors` dictionary onto inline field errors.
- On network failure or HTTP 5xx: display generic error banner "We couldn't send your message. Please try again later." (REQ-004 AC2).

**Notes / assumptions:**
- The SPA has no other pages; navigation chrome is minimal (a single header).
- TypeScript strict mode is enabled.

---

### TECH-002 — Contact Submission HTTP Endpoint (.NET 8 Web API)

**Component:** Backend Web API controller exposing the contact submission endpoint.
**Path:** `src/backend/Api/Controllers/ContactController.cs`
**Implements:** REQ-002, REQ-003, REQ-004

**Description:**
ASP.NET Core 8 controller (or minimal API equivalent) that exposes:

```
POST /api/contact
Content-Type: application/json
```

The endpoint is anonymous (no `[Authorize]` attribute, no auth middleware). The endpoint accepts a JSON DTO, runs server-side validation, persists a record, and returns either a 201 Created with an id, or a 400 Bad Request with a structured `errors` dictionary (REQ-002 AC2, AC3, AC4; REQ-003 AC2; REQ-004 AC1).

**Request DTO (`ContactSubmissionRequest`):**
```json
{
  "fullName": "string (1..200)",
  "email": "string (RFC email)",
  "phone": "string (1..50)",
  "subject": "string (1..200)",
  "message": "string (1..1000)"
}
```

**Success response (`201 Created`):**
```json
{ "id": "uuid", "receivedAt": "2026-05-05T12:34:56Z" }
```

**Validation error response (`400 Bad Request`):**
```json
{
  "errors": {
    "email": ["Email is not a valid format."],
    "message": ["Message must be 1000 characters or fewer."]
  }
}
```

**Validation (FluentValidation, REQ-002 AC2–4):**
- `fullName`: NotEmpty, MaximumLength 200.
- `email`: NotEmpty, EmailAddress (FluentValidation built-in).
- `phone`: NotEmpty, MaximumLength 50.
- `subject`: NotEmpty, MaximumLength 200.
- `message`: NotEmpty, MaximumLength 1000.

**Notes / assumptions:**
- No rate-limiting requirement was specified; out of scope for this iteration. Documented as a follow-up.
- No CAPTCHA required (not specified). The endpoint is intentionally fully public per REQ-003.
- CORS is enabled with a permissive origin policy for the frontend container/dev origin (`http://localhost:3000`) so the SPA can reach the API directly when not proxied.

---

### TECH-003 — Persistence Layer (EF Core + PostgreSQL)

**Component:** Data access for contact submissions.
**Path:** `src/backend/Api/Data/`
**Implements:** REQ-002 (AC1, AC5)

**Description:**
EF Core 8 `DbContext` with a single entity, mapped onto a single PostgreSQL table. Each submission is appended; no rows are ever overwritten or updated by application code (REQ-002 AC5).

**Entity (`ContactSubmission`):**
| Field        | Type                  | Constraints                          |
|--------------|-----------------------|--------------------------------------|
| Id           | `Guid`                | PK, generated server-side (`gen_random_uuid()` or app-side `Guid.NewGuid()`) |
| FullName     | `string`              | NOT NULL, varchar(200)               |
| Email        | `string`              | NOT NULL, varchar(320)               |
| Phone        | `string`              | NOT NULL, varchar(50)                |
| Subject      | `string`              | NOT NULL, varchar(200)               |
| Message      | `string`              | NOT NULL, varchar(1000)              |
| ReceivedAt   | `DateTime` (UTC)      | NOT NULL, default `now() at time zone 'utc'` |

**Table:** `contact_submissions` (snake_case via Npgsql naming convention).

**Key files:**
- `src/backend/Api/Data/AppDbContext.cs`
- `src/backend/Api/Data/Entities/ContactSubmission.cs`
- `src/backend/Api/Data/Migrations/` — EF Core migrations (initial migration creates `contact_submissions`).

**Migration strategy:**
- On API startup, the application invokes `dbContext.Database.MigrateAsync()` so the database schema is created on first run inside Docker Compose.
- Connection string read from env var `ConnectionStrings__Default`.

**Notes / assumptions:**
- An index on `received_at` is added for any future admin-listing use, even though admin listing is out of scope today; cost is negligible.

---

### TECH-004 — Application Wiring, Health, and CORS (.NET 8 host)

**Component:** Backend `Program.cs` host setup.
**Path:** `src/backend/Api/Program.cs`
**Implements:** REQ-003, REQ-004

**Description:**
Configures the ASP.NET Core 8 minimal host. Explicitly registers:
- `AddControllers()` and FluentValidation auto-validation.
- `AddDbContext<AppDbContext>` using `UseNpgsql(connectionString)`.
- `AddCors` with a named policy `FrontendPolicy` allowing the configured frontend origin (`FRONTEND_ORIGIN` env var; default `http://localhost:3000`).
- `MapHealthChecks("/healthz")` returning 200 when the API process is alive (used by Docker Compose healthcheck).
- No authentication or authorisation services are registered (REQ-003 AC2). There is no `app.UseAuthentication()` or `app.UseAuthorization()` call.

**Notes / assumptions:**
- Logging: default `Microsoft.Extensions.Logging` console provider; structured logs.
- Swagger/OpenAPI is enabled in `Development` env at `/swagger` for developer convenience; disabled in `Production`.

---

### TECH-005 — Frontend ↔ Backend Integration Contract

**Component:** Wire-format and error-handling contract shared between TECH-001 and TECH-002.
**Path:** Documented here; enforced via shared TypeScript and C# DTOs.
**Implements:** REQ-004 (AC1, AC2)

**Description:**
The frontend's `submitContact` function:
1. Sends `POST /api/contact` with `Content-Type: application/json`.
2. Treats `response.ok && status in {200, 201}` as success → renders confirmation banner (REQ-004 AC1, REQ-001 AC3).
3. Treats `status === 400` as validation error → maps `errors` map onto field-level inline messages.
4. Treats network errors (rejected `fetch`) and `status >= 500` as backend-unavailable → renders generic error banner (REQ-004 AC2).

The endpoint URL is configurable via `VITE_API_BASE_URL` (default `/api` so the Nginx/Vite proxy handles routing in compose).

---

## 5. Deployment Topology

All services are defined in a single `docker-compose.yml` at the repo root.

### Services

| Service name | Image / Build context           | Container port | Host port | Depends on        |
|--------------|---------------------------------|----------------|-----------|-------------------|
| `frontend`   | build `src/frontend/`           | 80             | 3000      | `backend`         |
| `backend`    | build `src/backend/`            | 8080           | 8080      | `db` (healthy)    |
| `db`         | `postgres:16-alpine`            | 5432           | 5432      | —                 |

### Environment variables

**`backend` service:**
| Variable                          | Example value                                                                  | Purpose                              |
|-----------------------------------|--------------------------------------------------------------------------------|--------------------------------------|
| `ASPNETCORE_ENVIRONMENT`          | `Production`                                                                   | Disables Swagger, dev exception page |
| `ASPNETCORE_URLS`                 | `http://+:8080`                                                                | Bind address inside container        |
| `ConnectionStrings__Default`      | `Host=db;Port=5432;Database=contactdb;Username=contact_user;Password=contact_pw` | EF Core connection string            |
| `FRONTEND_ORIGIN`                 | `http://localhost:3000`                                                        | CORS allow-origin                    |

**`db` service:**
| Variable             | Example value     |
|----------------------|-------------------|
| `POSTGRES_DB`        | `contactdb`       |
| `POSTGRES_USER`      | `contact_user`    |
| `POSTGRES_PASSWORD`  | `contact_pw`      |

**`frontend` service:**
| Variable             | Example value             | Purpose                                  |
|----------------------|---------------------------|------------------------------------------|
| `VITE_API_BASE_URL`  | `/api`                    | Baked into the bundle at build time      |

### Healthchecks

- `db`: `pg_isready -U contact_user -d contactdb`, interval 5s, retries 10.
- `backend`: HTTP GET `http://localhost:8080/healthz`, interval 10s, retries 6, start_period 20s.
- `frontend`: HTTP GET `http://localhost:80/`, interval 10s.

### Volumes

- `db_data:/var/lib/postgresql/data` — persistent database storage across compose restarts.

### Network

- Default user-defined bridge network `contact-net`. Service-to-service DNS uses Docker service names (`db`, `backend`).

---

## 6. Source Tree

```
/
├── docker-compose.yml
├── src/
│   ├── backend/
│   │   ├── Api/
│   │   │   ├── Program.cs
│   │   │   ├── Controllers/ContactController.cs
│   │   │   ├── Dtos/ContactSubmissionRequest.cs
│   │   │   ├── Validators/ContactSubmissionRequestValidator.cs
│   │   │   ├── Data/AppDbContext.cs
│   │   │   ├── Data/Entities/ContactSubmission.cs
│   │   │   └── Data/Migrations/
│   │   ├── Api.csproj
│   │   └── Dockerfile
│   └── frontend/
│       ├── src/
│       │   ├── App.tsx
│       │   ├── main.tsx
│       │   ├── pages/ContactUsPage.tsx
│       │   ├── components/ContactForm.tsx
│       │   ├── components/CharCounter.tsx
│       │   ├── api/contactClient.ts
│       │   └── validation/contactSchema.ts
│       ├── index.html
│       ├── package.json
│       ├── vite.config.ts
│       ├── tsconfig.json
│       ├── nginx.conf
│       └── Dockerfile
└── runs/run-2026-05-05-001/
    ├── req-spec.md
    └── tech-spec.md
```

---

## 7. Cross-Cutting Concerns

### 7.1 Security posture
- Per REQ-003, no authentication or authorisation is implemented. This is an explicit product decision.
- Inputs are length-bounded server-side to mitigate trivial abuse (REQ-002 AC4).
- TLS termination is out of scope of compose (handled by reverse proxy in real deployment).

### 7.2 Logging
- Backend logs at `Information` level by default; validation failures at `Warning`; unhandled exceptions at `Error` via global exception handler returning RFC 7807 problem details.

### 7.3 Error handling
- Global ASP.NET Core exception handler ensures any unhandled error returns `500` with a sanitized JSON body, never an HTML stack trace, so the frontend can reliably classify failures (REQ-004 AC2).

### 7.4 Testing strategy (planned, not implemented in this spec)
- Backend: xUnit unit tests for validator; integration test against Testcontainers PostgreSQL exercising POST /api/contact.
- Frontend: Vitest + React Testing Library unit tests for `ContactForm` covering all REQ-001 acceptance criteria; one Playwright smoke test against the running compose stack.

---

## 8. Requirement Traceability Matrix

| REQ-ID  | Implemented by                  |
|---------|---------------------------------|
| REQ-001 | TECH-001                        |
| REQ-002 | TECH-002, TECH-003              |
| REQ-003 | TECH-001, TECH-002, TECH-004    |
| REQ-004 | TECH-001, TECH-002, TECH-005    |

Reverse check (every TECH implements at least one REQ):

| TECH-ID  | Implements                    |
|----------|-------------------------------|
| TECH-001 | REQ-001, REQ-003, REQ-004     |
| TECH-002 | REQ-002, REQ-003, REQ-004     |
| TECH-003 | REQ-002                       |
| TECH-004 | REQ-003, REQ-004              |
| TECH-005 | REQ-004                       |

All four REQ-IDs are covered. All five TECH-IDs implement at least one REQ.

---

## 9. Open Technical Questions

None. All product-level open questions were resolved in req-spec v1.1; no further technical ambiguities remain for this scope.
