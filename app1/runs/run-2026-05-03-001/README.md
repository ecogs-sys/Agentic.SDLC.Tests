# Contact Us App

A React 18 + Vite single-page application backed by a .NET 10 Web API and PostgreSQL. Anonymous visitors can submit contact enquiries through the form; submissions are persisted and readable directly from the database.

---

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or Docker Engine + Docker Compose plugin v2)
- Docker Compose v2 (`docker compose` — note: no hyphen)

---

## Quick start

1. Change into the dotnet directory (where `docker-compose.yml` lives):

   ```bash
   cd runs/run-2026-05-03-001/dotnet
   ```

2. Copy the environment template and set your passwords:

   ```bash
   cp .env.example .env
   # Open .env in your editor and replace the placeholder values.
   ```

3. Build images and start all services:

   ```bash
   docker compose up --build
   ```

4. Once all three services are healthy, open the app:

   ```
   http://localhost:5173
   ```

---

## Port summary

| Service | Host port | Purpose                        |
|---------|-----------|--------------------------------|
| web     | 5173      | React SPA (nginx)              |
| api     | 8080      | .NET REST API (`/api/*`)       |
| db      | 5432      | PostgreSQL wire protocol       |

---

## Running tests

### Backend (xUnit)

```bash
cd runs/run-2026-05-03-001/dotnet
dotnet test
```

### Frontend (Vitest)

```bash
cd runs/run-2026-05-03-001/react
npm test
```

---

## Tearing down

Stop all containers and keep the database volume (data survives a restart):

```bash
docker compose down
```

Stop all containers and delete all data (including the `pgdata` volume):

```bash
docker compose down -v
```
