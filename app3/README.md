# Contact Us Application

A minimal single-page Contact Us form. A visitor fills in their name, email,
phone, subject, and message; the form posts to a .NET Web API that validates
and persists the submission to PostgreSQL, then returns a success or error
response back to the SPA.

## Stack

| Layer       | Technology                                    |
|-------------|-----------------------------------------------|
| Frontend    | React 18 + Vite + TypeScript (served by Nginx) |
| Backend API | ASP.NET Core (.NET 10) Web API                 |
| Database    | PostgreSQL 16                                  |
| Orchestration | Docker Compose v2                            |

## Prerequisites

| Tool            | Minimum version | Notes                                      |
|-----------------|-----------------|--------------------------------------------|
| Docker Desktop  | 4.x             | Must have Compose v2 (`docker compose`)    |
| .NET SDK        | 10.0            | Only needed for local backend development  |
| Node.js         | 20 LTS          | Only needed for local frontend development |

## Quick start (Docker)

```bash
# 1. Copy the environment template
cp .env.example .env

# 2. (Optional) Edit .env — the defaults work for local development

# 3. Build images and start all services
docker compose up --build

# 4. Open the application
#    http://localhost:3000
```

### Service ports

| Service  | URL                     | Notes                              |
|----------|-------------------------|------------------------------------|
| Frontend | http://localhost:3000   | React SPA served by Nginx          |
| Backend  | http://localhost:8080   | ASP.NET Core API                   |
| Database | localhost:5432          | PostgreSQL (contactdb)             |

API health endpoint: http://localhost:8080/healthz

## Environment variables

All variables are documented in `.env.example`. Copy it to `.env` before
running. The key variables are:

| Variable                 | Default              | Description                          |
|--------------------------|----------------------|--------------------------------------|
| `POSTGRES_PASSWORD`      | `contact_pw`         | PostgreSQL password for contact_user |
| `ASPNETCORE_ENVIRONMENT` | `Production`         | ASP.NET Core environment name        |
| `FRONTEND_ORIGIN`        | `http://localhost:3000` | CORS allow-origin for the API     |
| `VITE_API_BASE_URL`      | `/api`               | API base URL baked into the bundle   |

## Local development (without Docker)

### Backend

```bash
cd src/backend

# Set the connection string for a running Postgres instance
export ConnectionStrings__Default="Host=localhost;Port=5432;Database=contactdb;Username=contact_user;Password=contact_pw"

dotnet run --project Api/ContactApp.Api.csproj
# API listens on http://localhost:8080
```

### Frontend

```bash
cd src/frontend
npm install
npm run dev
# Dev server on http://localhost:3000
# /api requests are proxied to http://localhost:8080 via vite.config.ts
```

## Running tests

### Backend unit and integration tests

```bash
cd src/backend
dotnet test
```

### Frontend unit tests (Vitest + React Testing Library)

```bash
cd src/frontend
npm run test
```

### Frontend E2E tests (Playwright)

The full Docker Compose stack must be running before executing E2E tests.

```bash
# Start the stack (if not already running)
docker compose up --build -d

# Install the Playwright browser (first time only)
cd src/frontend
npm run playwright:install

# Run E2E tests
npm run test:e2e
```

## Project structure

```
/
├── docker-compose.yml          # Orchestrates db, backend, frontend
├── .env.example                # Environment variable template
├── src/
│   ├── backend/
│   │   ├── Api/                # ASP.NET Core project
│   │   │   ├── Controllers/
│   │   │   ├── Data/           # EF Core DbContext + migrations
│   │   │   ├── Dtos/
│   │   │   ├── Validators/
│   │   │   └── Program.cs
│   │   ├── Api.Tests/          # xUnit test project
│   │   ├── ContactApp.sln
│   │   └── Dockerfile
│   └── frontend/
│       ├── src/                # React + TypeScript source
│       ├── tests/              # Playwright E2E tests
│       ├── nginx.conf          # Nginx config (SPA routing + /api proxy)
│       └── Dockerfile
└── runs/run-2026-05-05-001/    # Architect and BA specs (frozen)
```
