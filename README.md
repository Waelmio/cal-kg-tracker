# Weight Loss Tracker

A personal weight loss tracking app with a C# .NET backend and Vue 3 frontend.

## Stack

- **Backend**: .NET 10 Web API · Entity Framework Core · SQLite · Swagger
- **Frontend**: Vue 3 · TypeScript · Vite · Pinia · Tailwind CSS · Chart.js

## Running the app

### Docker (recommended)

```bash
docker compose up --build
# App at http://localhost:8080
```

The SQLite database is stored in `./data/weighttracker.db` on the host and persists across restarts. To use a different path:

```bash
SQL_DB_PATH=/your/path/weighttracker.db docker compose up
```

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
- SQLite database, auto-migrated on startup

## API

See [docs/API.md](docs/API.md) for the full endpoint reference. Swagger UI is also available at `http://localhost:5118/swagger` when running locally.

## Project Structure

```
backend/WeightTracker.Api/   # .NET Web API
frontend/weight-tracker-ui/  # Vue 3 SPA
```
