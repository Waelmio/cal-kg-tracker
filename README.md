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

- Log daily weight and calories independently
- Set weight goals
- Set daily calorie targets
- Dashboard with a daily budget that keeps your calorie target, aiming at an average over 1 to 5 weeks, 
- Calendar with week and month summaries, weight pace, TDEE estimate and goal projection
- TDEE estimation via linear regression on logged weight and calorie data
- Weight history chart
- User settings: height, preferred unit, TDEE
- Integrations:
  - Import calories and weight from FatSecret: every 12 hours, on demand, or over a period with a day-by-day review


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

### FatSecret

| Variable | Default | Description |
|---|---|---|
| `FATSECRET_CONSUMER_KEY` | — | OAuth 1.0 Consumer Key of your FatSecret developer account |
| `FATSECRET_CONSUMER_SECRET` | — | OAuth 1.0 Consumer Secret |
| `PUBLIC_BASE_URL` | the address the browser uses. | Where FatSecret sends the browser back in the callback for login-in, generally your website. Default is fine. |
| `DATA_PROTECTION_KEYS_PATH` | `keys` next to the SQLite database; `keys` in the app folder with Postgres | Keys that encrypt the stored FatSecret token. Must be on a volume. Default is fine. |

See [FatSecret](#fatsecret-1) for the setup.

### Server

| Variable | Default | Description |
|---|---|---|
| `ASPNETCORE_HTTP_PORTS` | `8080` | HTTP port the backend listens on |
| `ASPNETCORE_ENVIRONMENT` | — | Runtime environment (`Development` / `Production`) |

## FatSecret

Imports calories and weight from your FatSecret diary into the daily logs.

1. Register at [platform.fatsecret.com](https://platform.fatsecret.com) (the free tier is enough).
2. Copy the **OAuth 1.0 Consumer Key and Consumer Secret**. The OAuth 2.0 Client ID and Secret don't work here ("Invalid signature").
3. Set `FATSECRET_CONSUMER_KEY` and `FATSECRET_CONSUMER_SECRET`. With Docker, put them in a `.env` file next to the compose file (git-ignored). Then restart the app.
4. Open Settings > Integrations > **Connect** and approve. If FatSecret can't send you back to the app, use the code instead.

It imports the current week, the 3 before and the 2 after, every 12 hours and with **Sync now**. `Logged` day are not overwritten `Planned` and `Logged Ahead` days are.

Good to know:

- FatSecret's terms limit how long most of its data may be stored (24 hours), without an exception for personal use. This app keeps what it imports; using it this way is your decision.

`scripts/fatsecret_spike.py` can be used for testing or exporting your data from FatSecret:

```bash
export FATSECRET_CONSUMER_KEY=... FATSECRET_CONSUMER_SECRET=...
python3 scripts/fatsecret_spike.py --year
python3 scripts/fatsecret_spike.py --from 2025-01-01 --to 2025-12-31   # any period, days to come included
```

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
