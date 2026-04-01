# Weight Loss Tracker

A personal weight loss tracking app with a C# .NET backend and Vue 3 frontend.

## Running the app

### Docker (recommended)

#### SQLite

```bash
docker compose up --build
# App at http://localhost:8080
```

#### PostgreSQL

```bash
docker compose -f docker-compose.postgres.yml up --build
# App at http://localhost:8080
```

Database provider is chosen at startup in this order: `DATABASE_URL` → `POSTGRES_HOST` → SQLite.

### Local development

Run both backend and frontend together from the root:

```bash
make dev
```

Or individually:

```bash
make dev-backend   # API at http://localhost:5118, Swagger at /swagger
make dev-frontend  # App at http://localhost:5173
```

### Running tests

```bash
make test          # Run all tests in parallel
make test-backend  # Backend only (xUnit)
make test-frontend # Frontend only (Vitest)
```

## Features

- Log daily weight and calories independently (one row per day, both fields nullable)
- Set weight goals — active goal is always the most recent entry
- Set daily calorie targets — same history pattern as weight goals
- Dashboard with 7-day weight trend, weekly avg calories, goal progress, and projected goal date
- TDEE estimation via linear regression on logged weight and calorie data
- Weight history chart (Chart.js)
- kg / lbs unit toggle — weight stored internally as kg, converted at display time
- User settings: height, preferred unit, cached TDEE
- SQLite (default) or PostgreSQL — provider chosen via env vars, auto-migrated on startup


## Environment Variables

### Database

The backend selects a database provider at startup using the following priority order:

1. **`DATABASE_URL`** — full PostgreSQL connection string (DSN format); takes precedence over all other options
2. **`POSTGRES_HOST`** — PostgreSQL hostname; triggers individual-component config below
3. Fallback: SQLite using `SQL_DB_PATH`

| Variable | Default | Description |
|---|---|---|
| `DATABASE_URL` | — | Full PostgreSQL DSN (e.g. `postgres://user:pass@host/db`) |
| `POSTGRES_HOST` | — | PostgreSQL server hostname |
| `POSTGRES_DB` | `weighttracker` | PostgreSQL database name |
| `POSTGRES_USER` | `postgres` | PostgreSQL username |
| `POSTGRES_PASSWORD` | *(empty)* | PostgreSQL password |
| `SQL_DB_PATH` | `weighttracker.db` | SQLite database file path |

### Server

| Variable | Default | Description |
|---|---|---|
| `ASPNETCORE_HTTP_PORTS` | `8080` | HTTP port the backend listens on |
| `ASPNETCORE_ENVIRONMENT` | — | Runtime environment (`Development` / `Production`) |

## Project Infos

### Stack

- **Backend**: .NET 10 Web API · Entity Framework Core · SQLite / PostgreSQL · Swagger
- **Frontend**: Vue 3 · TypeScript · Vite · Pinia · Tailwind CSS · Chart.js

### API

See [docs/API.md](docs/API.md) for the full endpoint reference. Swagger UI is also available at `http://localhost:5118/swagger` when running locally.

### Project Structure

```
backend/WeightTracker.Api/   # .NET Web API
frontend/weight-tracker-ui/  # Vue 3 SPA
```
