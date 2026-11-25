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
- Dashboard with a daily budget that keeps your calorie target on average over 1 to 5 weeks, calendar with week and month summaries, weight pace, TDEE estimate and goal projection
- TDEE estimation via linear regression on logged weight and calorie data
- Weight history chart (Chart.js)
- kg / lbs unit toggle — weight stored internally as kg, converted at display time
- User settings: height, preferred unit, cached TDEE
- Import calories and weight from FatSecret: every 12 hours, on demand, or over a period with a day-by-day review
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

### FatSecret

| Variable | Default | Description |
|---|---|---|
| `FATSECRET_CONSUMER_KEY` | — | OAuth 1.0 Consumer Key of your FatSecret developer account |
| `FATSECRET_CONSUMER_SECRET` | — | OAuth 1.0 Consumer Secret |
| `PUBLIC_BASE_URL` | the address the browser uses | Where FatSecret sends the browser back to, e.g. `https://weight.example.org`. Only needed when that can't be detected |
| `DATA_PROTECTION_KEYS_PATH` | `keys` next to the SQLite database; `keys` in the app folder with Postgres | Keys that encrypt the stored FatSecret token. Must be on a volume |
| `TZ` | `UTC` | Time zone that decides which day it is for the automatic sync, e.g. `Europe/Paris` |

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

How it imports:

- The last 3 months up to the end of this week, every 12 hours and with **Sync now**.
- Calories are written when the day has none, or when they were set in advance (before their own day). Weight is written when the day has none. Values imported earlier follow the diary.
- Anything else you logged is never replaced. **Sync a period** compares up to 12 months and lets you decide value by value.
- A day missing in FatSecret never deletes anything. Disconnecting keeps what was imported; "Clear data" also disconnects.
- Before connecting for the first time, export your data (Settings > Data): the first sync replaces calories whose last change is older than their day, which includes days the app filled in before the redesign.
- A value you clear in the app comes back with the next sync if FatSecret still has it.
- Settings in `appsettings.json` (or as environment variables, `Integrations__SyncIntervalHours`): `SyncIntervalHours` (0 turns the automatic sync off), `SyncMonths`, `ImportTodayCalories`. Today counts in the statistics as soon as it has calories, so a sync in the middle of the day counts a partial total until the next one. Set `ImportTodayCalories` to `false` to import a day's calories only once it is over.

Good to know:

- If a sync says the IP address is refused, add the server's **public** IP under "Manage API Keys" on platform.fatsecret.com.
- FatSecret's terms limit how long most of its data may be stored (24 hours), without an exception for personal use. This app keeps what it imports; using it this way is your decision.
- The token is stored encrypted. If the keys folder is lost (`DATA_PROTECTION_KEYS_PATH`), connect again.

`scripts/fatsecret_spike.py` talks to FatSecret without the app. To print every day of the last 12 months and save it as CSV in `data/`:

```bash
export FATSECRET_CONSUMER_KEY=... FATSECRET_CONSUMER_SECRET=...
python3 scripts/fatsecret_spike.py --year
python3 scripts/fatsecret_spike.py --from 2025-01-01 --to 2025-12-31   # any period
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
