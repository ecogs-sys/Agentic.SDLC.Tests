# Technical Specification

| Field       | Value                    |
|-------------|--------------------------|
| Run ID      | run-2026-05-03-001       |
| Version     | 1.0                      |
| Status      | draft                    |
| Author      | Architect Agent          |
| Date        | 2026-05-03               |
| Source Spec | req-spec.md v1.0         |

---

## Overview

This system is a small two-tier web application with a React 18 + Vite + TypeScript single-page frontend and a .NET 8 Web API backend, persisting data to PostgreSQL. The application's only user-facing feature is a public "Contact Us" page that allows anonymous visitors to submit contact enquiries. There is no authentication, authorization, session management, or user model anywhere in the system. All three tiers run as separate services orchestrated by docker-compose.

Key architectural decisions:
- Stateless backend; PostgreSQL is the single source of truth.
- CORS is permissively configured for the frontend origin only.
- Client-side validation is mirrored on the server (defensive validation per REQ-003 AC2).
- Database schema is created via EF Core migrations applied at backend startup.
- No auth middleware is registered on the API pipeline (per REQ-005).

---

## Stack

| Layer       | Technology                                         |
|-------------|----------------------------------------------------|
| Frontend    | React 18, Vite, TypeScript, Fetch API              |
| Backend     | .NET 8 Web API (ASP.NET Core), C#, EF Core 8       |
| Database    | PostgreSQL 16                                      |
| Orchestration | docker-compose v2                                |

---

## Components

### TECH-001 — React Contact Us Page (Frontend Route + View)

**Implements:** REQ-001, REQ-005

**Description**
A React route at `/contact` that renders the Contact Us page. The page is reachable directly via URL or via a header navigation link from the application root. No route guard, auth check, or redirect logic is applied — the route is rendered unconditionally (REQ-005). The page hosts the `<ContactForm>` component (TECH-002) and surfaces top-level submission state: a success confirmation banner on a successful POST and a non-destructive error banner on submission failure. On error, the form's input state is preserved so the visitor does not lose their data (REQ-001 AC5).

**Design Notes**
- Route defined in `App.tsx` using `react-router-dom` v6.
- A simple landing page at `/` includes a link to `/contact` so the page is discoverable.
- Confirmation message is rendered in place of the form after success (with a "Send another message" link to reset).
- Error message is a banner above the form; form state is retained.

---

### TECH-002 — Contact Form Component with Client-Side Validation

**Implements:** REQ-001, REQ-002

**Description**
A controlled React component `<ContactForm>` that renders three fields — `fullName` (text), `email` (text), `message` (textarea) — each marked visually as required (asterisk and `aria-required`). Validation runs both on blur and on submit; on submit, the component blocks the network call if any field is invalid and renders field-level error messages adjacent to each invalid field.

**Validation Rules**
- `fullName`: non-empty after trim; max length 200.
- `email`: non-empty after trim; matches the regex `^[^\s@]+@[^\s@]+\.[^\s@]+$`; max length 320.
- `message`: non-empty after trim; max length 5000.

**Submission Behavior**
- On valid submit, performs `POST {VITE_API_BASE_URL}/api/contact` with JSON body `{ fullName, email, message }`.
- On HTTP 200/201: clears form state and signals success to the parent page.
- On HTTP 400: parses the `errors` object from the response (TECH-004) and renders server-side field errors next to the matching fields.
- On network failure or HTTP 5xx: signals a generic error to the parent page; form state is preserved (REQ-001 AC5).

**Design Notes**
- Disable the submit button while a request is in flight to prevent double submission.
- Each error message is uniquely worded (e.g., "Please enter a valid email address.", "Message cannot be empty.").

---

### TECH-003 — API Client Module (Frontend)

**Implements:** REQ-001, REQ-003

**Description**
A small TypeScript module `src/api/contactApi.ts` that exposes `submitContact(payload): Promise<SubmitResult>`. It encapsulates the fetch call, JSON serialization, response parsing, and error normalization (network error vs. validation error vs. server error). The base URL is read from `import.meta.env.VITE_API_BASE_URL` so the module is environment-agnostic.

**Returned Shape**
```ts
type SubmitResult =
  | { kind: 'ok' }
  | { kind: 'validation'; fieldErrors: Record<string, string[]> }
  | { kind: 'error'; message: string };
```

---

### TECH-004 — Contact Submission API Endpoint (Backend)

**Implements:** REQ-003, REQ-005

**Description**
ASP.NET Core minimal API endpoint `POST /api/contact` exposed by the .NET 8 Web API project. The endpoint is registered without `[Authorize]` and the application pipeline does not register any authentication or authorization middleware (REQ-005, REQ-003 AC3). It accepts a JSON body, validates it, persists a record via TECH-005, and returns a structured response.

**Request DTO**
```csharp
public record ContactSubmissionRequest(string FullName, string Email, string Message);
```

**Server-Side Validation (mirrors TECH-002)**
- `FullName`: required, trimmed length 1..200.
- `Email`: required, trimmed length 1..320, must match `^[^\s@]+@[^\s@]+\.[^\s@]+$`.
- `Message`: required, trimmed length 1..5000.

**Responses**
- `201 Created` with body `{ "id": "<guid>", "receivedAt": "<ISO-8601>" }` on success.
- `400 Bad Request` with body `{ "errors": { "<fieldName>": ["<message>"] } }` on validation failure (REQ-003 AC2).
- `500 Internal Server Error` with body `{ "error": "An unexpected error occurred." }` on persistence failure.

**Other Endpoints**
- `GET /api/health` — returns `200 { "status": "ok" }` for liveness checks (used by docker-compose healthchecks).

**Design Notes**
- CORS policy named `frontend` allows origin `http://localhost:5173` (configurable via env var) with methods `GET, POST, OPTIONS` and `Content-Type` header.
- JSON serialization uses default System.Text.Json with camelCase property naming.
- No session, cookie, or token middleware is added to the pipeline.

---

### TECH-005 — Contact Submission Persistence (EF Core + PostgreSQL)

**Implements:** REQ-004

**Description**
An EF Core 8 `DbContext` (`ContactDbContext`) with a single `DbSet<ContactSubmission> Submissions`. The entity is mapped to a PostgreSQL table `contact_submissions`. Each insert produces a new row; there are no updates or deletes performed by the application (REQ-004 AC2 — append-only). The server assigns the `Id` and `ReceivedAt` values; the request payload's fields are stored verbatim (after trim).

**Entity**
```csharp
public class ContactSubmission
{
    public Guid Id { get; set; }            // server-assigned (Guid.NewGuid)
    public string FullName { get; set; }    // varchar(200), not null
    public string Email { get; set; }       // varchar(320), not null
    public string Message { get; set; }     // text, not null
    public DateTime ReceivedAt { get; set; }// timestamptz, not null, server-set UTC
}
```

**Schema (PostgreSQL DDL, generated by EF Core migration `InitialCreate`)**
```sql
CREATE TABLE contact_submissions (
    id            uuid        PRIMARY KEY,
    full_name     varchar(200) NOT NULL,
    email         varchar(320) NOT NULL,
    message       text         NOT NULL,
    received_at   timestamptz  NOT NULL
);
CREATE INDEX ix_contact_submissions_received_at ON contact_submissions (received_at);
```

**Migration Application**
On backend startup, `db.Database.Migrate()` is invoked inside a retry loop (5 attempts, 2s backoff) to tolerate the database not being immediately ready in docker-compose.

---

### TECH-006 — Application Composition & Configuration (Backend)

**Implements:** REQ-003, REQ-004, REQ-005

**Description**
The `Program.cs` of the .NET 8 Web API wires together: `AddDbContext<ContactDbContext>` using Npgsql with the connection string from `ConnectionStrings__DefaultConnection`; the `frontend` CORS policy from `Cors__AllowedOrigin`; the minimal API endpoints from TECH-004; and the startup migration runner from TECH-005. The pipeline order is: `UseCors("frontend")` → `MapEndpoints()`. No `UseAuthentication()` or `UseAuthorization()` is called (REQ-005).

**Configuration Sources**
- `appsettings.json` for defaults.
- Environment variables override (standard ASP.NET Core convention).

---

### TECH-007 — Frontend Build & Static Hosting

**Implements:** REQ-001

**Description**
The React app is built with Vite (`vite build`) producing static assets in `dist/`. In docker-compose, the frontend is served by an `nginx:alpine` container with a minimal config that (a) serves `dist/` at `/` and (b) falls back to `index.html` for SPA routing. The base API URL is injected at build time via `VITE_API_BASE_URL`.

**Design Note**
For local development outside docker, `npm run dev` starts the Vite dev server on port 5173 and proxies are unnecessary because the frontend calls the backend directly via the configured base URL.

---

## Deployment Topology

All three services run via `docker-compose.yml` at the repository root. Service names below are also their DNS hostnames on the compose network `appnet`.

### Service: `db` (PostgreSQL)
- Image: `postgres:16-alpine`
- Container port: `5432`
- Host port mapping: `5432:5432`
- Volume: `pgdata:/var/lib/postgresql/data`
- Environment variables:
  - `POSTGRES_DB=contactdb`
  - `POSTGRES_USER=appuser`
  - `POSTGRES_PASSWORD=apppassword`
- Healthcheck: `pg_isready -U appuser -d contactdb` every 5s.

### Service: `api` (.NET 8 Web API)
- Build context: `./backend`
- Container port: `8080`
- Host port mapping: `8080:8080`
- Depends on: `db` (condition: `service_healthy`)
- Environment variables:
  - `ASPNETCORE_ENVIRONMENT=Development`
  - `ASPNETCORE_URLS=http://+:8080`
  - `ConnectionStrings__DefaultConnection=Host=db;Port=5432;Database=contactdb;Username=appuser;Password=apppassword`
  - `Cors__AllowedOrigin=http://localhost:5173`
- Healthcheck: HTTP `GET /api/health` every 10s.

### Service: `web` (React static site via nginx)
- Build context: `./frontend`
- Build args:
  - `VITE_API_BASE_URL=http://localhost:8080`
- Container port: `80`
- Host port mapping: `5173:80`
- Depends on: `api` (condition: `service_started`)

### Network & Volumes
- Network: `appnet` (bridge driver, default for compose).
- Volume: `pgdata` (named, persistent across `docker compose down`; removed only with `-v`).

### Port Summary

| Service | Container Port | Host Port | Purpose                       |
|---------|----------------|-----------|-------------------------------|
| db      | 5432           | 5432      | PostgreSQL wire protocol      |
| api     | 8080           | 8080      | HTTP API (`/api/*`)           |
| web     | 80             | 5173      | Static SPA + SPA fallback     |

### Required Environment Variables (consolidated)

| Variable                                  | Service | Purpose                                            |
|-------------------------------------------|---------|----------------------------------------------------|
| `POSTGRES_DB`                             | db      | Database name                                      |
| `POSTGRES_USER`                           | db      | Database user                                      |
| `POSTGRES_PASSWORD`                       | db      | Database password                                  |
| `ASPNETCORE_ENVIRONMENT`                  | api     | ASP.NET Core environment selector                  |
| `ASPNETCORE_URLS`                         | api     | Bind URL/port inside the container                 |
| `ConnectionStrings__DefaultConnection`    | api     | EF Core / Npgsql connection string                 |
| `Cors__AllowedOrigin`                     | api     | Origin allowed by the `frontend` CORS policy       |
| `VITE_API_BASE_URL` (build-arg)           | web     | Base URL the SPA uses to call the API              |

---

## Cross-Cutting Concerns

### Logging
- Backend uses the default ASP.NET Core `ILogger` with console output. Each contact submission logs an info entry: `Contact submission accepted: {Id}` (no PII fields are logged beyond the generated id).
- Validation failures log at `Warning` level with the offending field names (not values).

### Error Handling
- Global `UseExceptionHandler` middleware in the API converts unhandled exceptions to a generic `500` JSON response shape `{ "error": "An unexpected error occurred." }`.
- Frontend treats any non-2xx, non-400 response as a generic submission failure and surfaces the banner described in TECH-001.

### Security Posture
- No authentication is required by design (REQ-005). The `/api/contact` endpoint is intentionally anonymous.
- CORS is restricted to a single allowed origin to prevent third-party sites from posting via a victim's browser.
- No PII is exposed in HTTP responses beyond the echoed `id` and `receivedAt`.
- Connection string uses a least-privilege application user; the database superuser is not used by the API.

### Testability
- Backend solution includes an xUnit project covering: validation rules (TECH-004), persistence happy-path (TECH-005, using a Testcontainers PostgreSQL instance), and CORS configuration smoke test.
- Frontend includes Vitest + React Testing Library tests covering: required-field validation (TECH-002), email-format validation (TECH-002), success path (TECH-002 + mocked TECH-003), error path with state preservation (TECH-001).

---

## Traceability Matrix

| REQ-ID  | Implemented By                              |
|---------|---------------------------------------------|
| REQ-001 | TECH-001, TECH-002, TECH-003, TECH-007      |
| REQ-002 | TECH-002                                    |
| REQ-003 | TECH-003, TECH-004, TECH-006                |
| REQ-004 | TECH-005, TECH-006                          |
| REQ-005 | TECH-001, TECH-004, TECH-006                |

Self-check: every REQ-ID from `req-spec.md` v1.0 (REQ-001..REQ-005) appears in at least one TECH's Implements list above, and every TECH-ID (TECH-001..TECH-007) implements at least one REQ.

---

## Open Questions / Assumptions

1. **Notification behavior** — req-spec.md notes notification rules are not finalized. This tech-spec assumes no email notification to the recipient team is sent on submission; the team reads submissions directly from the database. If notification becomes a requirement, a new TECH-008 (e.g., outbox + SMTP worker) will be added without altering existing TECH IDs.
2. **Data retention** — req-spec.md notes retention rules are not finalized. This tech-spec assumes indefinite retention (REQ-004 AC2 — append-only). Any future purge policy can be implemented as a separate scheduled job without changing the storage schema.
3. **Rate limiting / spam protection** — Not in scope of req-spec.md. The anonymous endpoint is therefore unprotected against abuse. This is flagged as a known gap; if added later it would not change existing TECHs (would be a new middleware TECH).
4. **Internationalization** — req-spec.md does not specify localization. UI strings are English-only and validation regex/length limits assume Latin-script names; this is a reasonable assumption documented here.
