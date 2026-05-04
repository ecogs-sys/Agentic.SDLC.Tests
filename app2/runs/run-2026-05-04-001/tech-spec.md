# Technical Specification

| Field       | Value                               |
|-------------|-------------------------------------|
| Run ID      | run-2026-05-04-001                  |
| Version     | 1.0                                 |
| Status      | draft                               |
| Date        | 2026-05-04                          |
| Author      | Architect Agent (claude-opus-4-7)   |
| Source Spec | runs/run-2026-05-04-001/req-spec.md |

---

## 1. Overview

This technical specification describes the implementation of a publicly accessible "Contact Us" web application. The system is composed of a React 18 + Vite + TypeScript single-page application (frontend) and a .NET 8 Web API (backend), with PostgreSQL for persistence. All components are containerised and orchestrated via docker-compose. No authentication or authorisation is implemented anywhere in the system, in line with REQ-004.

The frontend renders a Contact Us form, validates input client-side, and POSTs JSON to the backend. The backend validates server-side, persists submissions to PostgreSQL, and returns structured responses (success or descriptive error). Health endpoints expose readiness for orchestration.

---

## 2. Architecture Summary

```
+-----------------+         HTTPS/HTTP           +----------------------+        TCP/5432         +-------------+
|  Browser (SPA)  |  ------------------------->  |  .NET 8 Web API      |  -------------------->  | PostgreSQL  |
|  React 18 +     |   GET / (static assets)      |  (ASP.NET Core)      |   Npgsql / EF Core      |   16        |
|  Vite + TS      |   POST /api/contact          |                      |                         |             |
+-----------------+                              +----------------------+                         +-------------+
        ^                                                  ^
        |  served by Nginx (static)                        |  /health, /api/contact
        |                                                  |
   port 5173 (dev) / 8080 (container)              port 8080 (container)
```

- Single-page application served as static assets by an Nginx container in production; Vite dev server in development.
- Backend is a stateless ASP.NET Core Web API. CORS is enabled for the frontend origin.
- PostgreSQL stores contact submissions in a single `contact_submissions` table.
- All three services are wired together by `docker-compose.yml`.

---

## 3. Tech Stack

| Layer            | Technology                                  |
|------------------|---------------------------------------------|
| Frontend         | React 18, Vite 5, TypeScript 5              |
| Frontend HTTP    | Native `fetch` API                          |
| Frontend forms   | React Hook Form + Zod (schema validation)   |
| Frontend styling | Plain CSS modules (no UI framework)         |
| Backend          | .NET 8 ASP.NET Core Web API                 |
| ORM              | Entity Framework Core 8 with Npgsql         |
| Validation       | FluentValidation 11                         |
| Database         | PostgreSQL 16                               |
| Container        | Docker, docker-compose v2                   |
| Web server (FE)  | Nginx 1.27 (alpine) for static hosting      |

---

## 4. Component Breakdown

### TECH-001 — React SPA Shell and Routing
**Implements:** REQ-001 (AC-001-1, AC-001-2, AC-001-3), REQ-004 (AC-004-1)

**Description:**
A React 18 + Vite + TypeScript single-page application bootstrapped with `npm create vite@latest` (template `react-ts`). The shell uses `react-router-dom` v6 with a single route `/` rendering the Contact Us page. No login route, no auth provider, no protected routes. The app is accessible to any visitor without prompts.

**Location:** `src/frontend/`

**Key files:**
- `src/frontend/index.html` — Vite entry HTML.
- `src/frontend/src/main.tsx` — React root, sets up `BrowserRouter`.
- `src/frontend/src/App.tsx` — Router and layout shell (header with "Contact Us" link).
- `src/frontend/src/pages/ContactUs.tsx` — implemented in TECH-002.
- `src/frontend/vite.config.ts` — Vite config including dev proxy for `/api` to backend.

**Browser support:** Chrome, Firefox, Edge (latest stable). Vite default `esbuild` target `es2020` is sufficient. No polyfills needed.

---

### TECH-002 — Contact Us Page (Form UI + Client Validation)
**Implements:** REQ-003 (AC-003-1, AC-003-3, AC-003-4, AC-003-5)

**Description:**
React component rendering a form with three fields: Full Name (text), Email (text, type=email), Message (textarea). Uses React Hook Form with a Zod schema for client-side validation. Field-level inline error messages are shown beneath each field. The submit button is disabled while the request is in flight. On backend error, the entered values are retained in the form state so the visitor can retry.

**Location:** `src/frontend/src/pages/ContactUs.tsx`

**Validation rules (Zod):**
- `fullName`: required, trimmed, min length 1, max length 200.
- `email`: required, must match standard email regex (Zod's `.email()`).
- `message`: required, trimmed, min length 1, max length 5000.

**UI states:**
- `idle` — form ready for input.
- `submitting` — submit button disabled, spinner shown.
- `success` — success banner shown ("Thanks, your message has been received."), form reset.
- `error` — error banner shown with backend message; form values preserved.

**Note (open question 1):** The req-spec asks what fields beyond name/email/message should be collected. Assumption: only the three required fields are implemented; design is open for adding optional fields (e.g., phone, subject) in a future iteration without schema migration impact beyond adding nullable columns.

---

### TECH-003 — Frontend API Client for Contact Submission
**Implements:** REQ-003 (AC-003-2, AC-003-4)

**Description:**
A thin TypeScript module wrapping `fetch` to POST the contact form payload to the backend. Translates non-2xx responses into typed error objects so TECH-002 can render user-friendly messages. Reads the API base URL from the build-time environment variable `VITE_API_BASE_URL` (defaults to `/api` so that the Nginx reverse-proxy or Vite dev proxy can route requests).

**Location:** `src/frontend/src/api/contactClient.ts`

**Public API:**
```ts
export type ContactPayload = { fullName: string; email: string; message: string };
export type ContactSuccess = { id: string; receivedAt: string };
export type ContactError = { status: number; message: string; fieldErrors?: Record<string, string> };
export async function submitContact(payload: ContactPayload): Promise<ContactSuccess>;
```

`submitContact` throws a `ContactError` on non-2xx; resolves to `ContactSuccess` on 201.

---

### TECH-004 — Frontend Container (Nginx static hosting + reverse proxy)
**Implements:** REQ-001 (AC-001-1, AC-001-2)

**Description:**
A multi-stage Dockerfile builds the SPA with Node 20 and serves the resulting `dist/` from an Nginx 1.27-alpine container. Nginx config serves `index.html` for all unmatched routes (SPA fallback) and proxies `/api/*` to the backend service so the SPA does not need to know the backend URL at runtime.

**Location:**
- `src/frontend/Dockerfile`
- `src/frontend/nginx.conf`

**Container port:** 8080 (Nginx listens on 8080 inside the container).
**Host port mapping (dev):** `5173:8080` for parity with Vite dev convention; production host can be any.

**Nginx behaviour:**
- `location /` → try_files `$uri /index.html`.
- `location /api/` → `proxy_pass http://api:8080/api/;`.

---

### TECH-005 — .NET 8 Web API Project Skeleton and Health Endpoint
**Implements:** REQ-002 (AC-002-1), REQ-004 (AC-004-2)

**Description:**
ASP.NET Core 8 Minimal API project. No authentication middleware is registered. CORS is configured to allow the frontend origin (`http://localhost:5173` in dev; configurable via env var `Cors__AllowedOrigins`). Exposes `GET /health` returning `200 OK` with body `{ "status": "healthy" }` for container orchestration health checks.

**Location:** `src/backend/`

**Key files:**
- `src/backend/ContactApi.csproj`
- `src/backend/Program.cs` — service registration, middleware, route group `/api`.
- `src/backend/appsettings.json` — default config.
- `src/backend/appsettings.Development.json` — dev overrides.

**Routes registered in this TECH:**
- `GET /health` — liveness/readiness probe.

**Auth:** No `AddAuthentication` / `AddAuthorization` calls. No `[Authorize]` attributes anywhere. No 401/403 paths exist for the contact flow.

---

### TECH-006 — Contact Submission Endpoint and DTOs
**Implements:** REQ-002 (AC-002-2, AC-002-3, AC-002-4), REQ-003 (AC-003-2), REQ-004 (AC-004-2)

**Description:**
Implements `POST /api/contact` accepting JSON. No auth header required. Validates payload using FluentValidation; on validation failure returns `400 Bad Request` with a problem-details body identifying which fields failed. On success, persists the submission via TECH-007 and returns `201 Created` with body `{ "id": "<guid>", "receivedAt": "<iso8601>" }`.

**Location:**
- `src/backend/Features/Contact/ContactEndpoints.cs`
- `src/backend/Features/Contact/SubmitContactRequest.cs`
- `src/backend/Features/Contact/SubmitContactRequestValidator.cs`
- `src/backend/Features/Contact/ContactResponse.cs`

**Request DTO:**
```csharp
public record SubmitContactRequest(string FullName, string Email, string Message);
```

**Validation rules (FluentValidation):**
- `FullName`: NotEmpty, MaximumLength(200).
- `Email`: NotEmpty, EmailAddress, MaximumLength(320).
- `Message`: NotEmpty, MaximumLength(5000).

**Error response shape (400):**
```json
{
  "type": "validation_error",
  "message": "One or more fields are invalid.",
  "fieldErrors": { "email": "A valid email address is required." }
}
```

**Success response shape (201):**
```json
{ "id": "0f3...", "receivedAt": "2026-05-04T10:15:30Z" }
```

---

### TECH-007 — Persistence Layer (EF Core + PostgreSQL)
**Implements:** REQ-002 (AC-002-4)

**Description:**
EF Core 8 `DbContext` with one entity, `ContactSubmission`, persisted to PostgreSQL. A migration creates the `contact_submissions` table on first startup via `db.Database.MigrateAsync()` invoked from `Program.cs` so an operator can later read the rows directly from the database.

**Location:**
- `src/backend/Persistence/AppDbContext.cs`
- `src/backend/Persistence/ContactSubmission.cs`
- `src/backend/Persistence/Migrations/*` (generated)

**Entity:**
```csharp
public class ContactSubmission {
    public Guid Id { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime ReceivedAt { get; set; }
}
```

**Table `contact_submissions`:**
| Column       | Type                       | Notes              |
|--------------|----------------------------|--------------------|
| id           | uuid PRIMARY KEY           | server-generated   |
| full_name    | varchar(200) NOT NULL      |                    |
| email        | varchar(320) NOT NULL      | indexed (btree)    |
| message      | text NOT NULL              |                    |
| received_at  | timestamptz NOT NULL       | default `now()`    |

**Connection string source:** environment variable `ConnectionStrings__Default`.

**Note (open question 2):** The req-spec leaves the persistence target ambiguous (DB, file, email, third-party). Assumption: PostgreSQL, because the stack mandate is .NET + React + PostgreSQL + docker-compose. An operator can `psql` into the DB to review submissions; no admin UI is built (out of scope per req-spec).

---

### TECH-008 — CORS, Error Handling, and Logging
**Implements:** REQ-002 (AC-002-1, AC-002-3), REQ-004 (AC-004-2)

**Description:**
- CORS policy `frontend` allows configured origins, methods `GET, POST, OPTIONS`, headers `Content-Type`. Applied globally before route handlers.
- Global exception handler middleware converts unhandled exceptions to `500 Internal Server Error` with body `{ "type": "internal_error", "message": "An unexpected error occurred." }` and logs the exception with a correlation id.
- Logging via the built-in `Microsoft.Extensions.Logging` to stdout (captured by Docker).
- Explicitly does NOT register authentication/authorization middleware, ensuring no request path can produce 401/403.

**Location:** `src/backend/Program.cs`, `src/backend/Middleware/ExceptionHandlingMiddleware.cs`.

---

### TECH-009 — Backend Container
**Implements:** REQ-002 (AC-002-1)

**Description:**
Multi-stage Dockerfile using `mcr.microsoft.com/dotnet/sdk:8.0` to publish and `mcr.microsoft.com/dotnet/aspnet:8.0` as runtime. The runtime image installs `curl` for the docker-compose healthcheck. The container listens on port 8080 (`ASPNETCORE_URLS=http://+:8080`).

**Location:** `src/backend/Dockerfile`

**Healthcheck:** `curl -f http://localhost:8080/health || exit 1`.

---

### TECH-010 — Database Container
**Implements:** REQ-002 (AC-002-4)

**Description:**
`postgres:16-alpine` container in docker-compose, with a named volume for data persistence and a healthcheck using `pg_isready`.

**Location:** Defined inline in `docker-compose.yml`; no separate Dockerfile.

---

### TECH-011 — docker-compose Orchestration
**Implements:** REQ-001, REQ-002, REQ-003, REQ-004 (deployment glue for all)

**Description:**
`docker-compose.yml` at the repo root ties the three services together: `db` (PostgreSQL), `api` (.NET backend), `web` (Nginx + React build). The `api` depends on `db` being healthy; `web` depends on `api` being healthy. A single private bridge network `appnet` lets services reach each other by service name.

**Location:** `docker-compose.yml` (repo root).

See section 6 (Deployment Topology) for full port and env-var details.

---

## 5. Data Flow

### 5.1 Successful submission
1. Visitor navigates to `http://<host>:5173/` (or production host) — TECH-001 renders shell, TECH-002 renders form.
2. Visitor fills form. Zod (TECH-002) validates on blur and on submit.
3. On submit, TECH-003 POSTs JSON to `/api/contact`.
4. Nginx (TECH-004) proxies to `api:8080/api/contact`.
5. .NET endpoint (TECH-006) deserialises, FluentValidation passes.
6. EF Core (TECH-007) inserts a row into `contact_submissions`.
7. API returns `201 Created` with `{id, receivedAt}`.
8. TECH-002 shows success banner and resets form.

### 5.2 Validation failure (server)
- TECH-006 returns `400` with `fieldErrors`. TECH-003 throws `ContactError`. TECH-002 displays the message and preserves entered values (AC-003-4).

### 5.3 Network/server failure
- TECH-003 catches `fetch` errors and throws `ContactError` with status `0` and a generic message; TECH-002 shows "Something went wrong, please try again." Form values preserved.

---

## 6. Deployment Topology

### 6.1 Services and ports

| Service name (compose) | Image / build context     | Container port | Host port | Purpose                          |
|------------------------|---------------------------|----------------|-----------|----------------------------------|
| `db`                   | `postgres:16-alpine`      | 5432           | 5432      | PostgreSQL data store            |
| `api`                  | build `./src/backend`     | 8080           | 8080      | .NET 8 Web API                   |
| `web`                  | build `./src/frontend`    | 8080           | 5173      | Nginx serving SPA + reverse-proxy|

All services share network `appnet` (bridge). Internal hostnames are `db`, `api`, `web`.

### 6.2 Environment variables

**`db` service:**
| Variable             | Value (default)   | Notes                          |
|----------------------|-------------------|--------------------------------|
| `POSTGRES_DB`        | `contactdb`       | Database name                  |
| `POSTGRES_USER`      | `contactuser`     | DB user                        |
| `POSTGRES_PASSWORD`  | `contactpass`     | DB password (override in prod) |

**`api` service:**
| Variable                          | Value (default)                                                                            | Notes                              |
|-----------------------------------|--------------------------------------------------------------------------------------------|------------------------------------|
| `ASPNETCORE_ENVIRONMENT`          | `Production`                                                                               |                                    |
| `ASPNETCORE_URLS`                 | `http://+:8080`                                                                            | Kestrel binding                    |
| `ConnectionStrings__Default`      | `Host=db;Port=5432;Database=contactdb;Username=contactuser;Password=contactpass`           | Read by EF Core                    |
| `Cors__AllowedOrigins`            | `http://localhost:5173`                                                                    | Comma-separated list               |
| `Logging__LogLevel__Default`      | `Information`                                                                              |                                    |

**`web` service:**
| Variable               | Value (default) | Notes                                                       |
|------------------------|-----------------|-------------------------------------------------------------|
| `VITE_API_BASE_URL`    | `/api`          | Build-time (baked into bundle by Vite)                      |

(Build args are passed to the frontend Dockerfile via `args:` in compose.)

### 6.3 Volumes

| Volume name | Mounted at (in `db`) | Purpose                |
|-------------|----------------------|------------------------|
| `pgdata`    | `/var/lib/postgresql/data` | Persist DB contents |

### 6.4 Healthchecks

| Service | Test                                                | Interval | Retries |
|---------|-----------------------------------------------------|----------|---------|
| `db`    | `pg_isready -U contactuser -d contactdb`            | 10s      | 5       |
| `api`   | `curl -f http://localhost:8080/health`              | 15s      | 5       |
| `web`   | `wget -q --spider http://localhost:8080/`           | 15s      | 5       |

### 6.5 Startup order
- `api` `depends_on: { db: { condition: service_healthy } }`
- `web` `depends_on: { api: { condition: service_healthy } }`

---

## 7. API Contract Summary

### `GET /health`
- 200 OK → `{ "status": "healthy" }`

### `POST /api/contact`
- Request body:
```json
{ "fullName": "Jane Doe", "email": "jane@example.com", "message": "Hello." }
```
- 201 Created → `{ "id": "<guid>", "receivedAt": "<iso8601 utc>" }`
- 400 Bad Request → `{ "type": "validation_error", "message": "...", "fieldErrors": { "<field>": "<msg>" } }`
- 500 Internal Server Error → `{ "type": "internal_error", "message": "An unexpected error occurred." }`
- No 401 / 403 paths exist (REQ-004 / AC-004-2).

---

## 8. Cross-cutting Concerns

- **Security:** No auth by design (REQ-004). Mitigations against abuse: server-side input length caps (TECH-006), parameterised SQL via EF Core (TECH-007), and CORS origin allow-list (TECH-008). Rate-limiting is out of scope of the req-spec but flagged below.
- **Observability:** stdout logging only; no APM/metrics integration (out of scope).
- **Internationalisation:** copy is English-only (req-spec is silent; assumption).
- **Accessibility:** form fields use `<label>` association and ARIA attributes for error messages (good practice; satisfies AC-003-3 inline feedback semantics).

---

## 9. Mapping: REQ → TECH

| REQ-ID  | Implemented by                                          |
|---------|---------------------------------------------------------|
| REQ-001 | TECH-001, TECH-004                                      |
| REQ-002 | TECH-005, TECH-006, TECH-007, TECH-008, TECH-009, TECH-010 |
| REQ-003 | TECH-002, TECH-003, TECH-006                            |
| REQ-004 | TECH-001, TECH-005, TECH-006, TECH-008                  |

Every REQ-ID from the req-spec is covered by at least one TECH-ID. Every TECH-ID lists at least one REQ-ID under its Implements line.

---

## 10. Assumptions and Open Items (carried from req-spec)

| # | Assumption / Note | Source open question |
|---|-------------------|----------------------|
| 1 | Only `fullName`, `email`, `message` are collected; schema is extensible for future optional fields. | OQ #1 |
| 2 | Submissions are persisted to PostgreSQL; operators can review via direct DB query. No admin UI. | OQ #2 |
| 3 | No confirmation email is sent (out of scope per req-spec section 4). | OQ #3 |
| 4 | Browser support targets latest Chrome, Firefox, Edge on desktop. Layout is responsive but no specific mobile target is verified. | OQ #4 |

## 11. Risks and Conflicts

- **No rate limiting / CAPTCHA:** Because there is no auth, the public `POST /api/contact` endpoint is susceptible to spam. The req-spec does not require mitigations, so none are implemented in v1. Flag for future iteration.
- **No conflicts** between REQs were detected during design.
