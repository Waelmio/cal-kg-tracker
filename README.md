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

**Backend**

```bash
cd backend/WeightTracker.Api
dotnet run
# API at http://localhost:5118
# Swagger UI at http://localhost:5118/swagger
```

**Frontend**

```bash
cd frontend/weight-tracker-ui
npm install
npm run dev
# App at http://localhost:5173
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

## API Endpoints

| Method | Path | Notes |
|---|---|---|
| GET | `/api/daily-logs` | All daily logs |
| GET/PUT | `/api/daily-logs/{date}` | date = `yyyy-MM-dd` |
| DELETE | `/api/daily-logs/{date}` | Delete entire day |
| DELETE | `/api/daily-logs/{date}/weight` | Null weight only |
| DELETE | `/api/daily-logs/{date}/calories` | Null calories only |
| GET/POST | `/api/goals` | Goal history |
| GET | `/api/goals/active` | Most recent goal |
| DELETE | `/api/goals/{id}` | Delete a goal |
| GET/POST | `/api/calorie-goals` | Calorie goal history |
| GET | `/api/calorie-goals/active` | Most recent calorie goal |
| DELETE | `/api/calorie-goals/{id}` | Delete a calorie goal |
| GET | `/api/dashboard` | Aggregated stats |
| GET | `/api/tdee` | TDEE estimate |
| GET/PUT | `/api/settings` | Height, unit preference |

## Project Structure

```
backend/WeightTracker.Api/   # .NET Web API
frontend/weight-tracker-ui/  # Vue 3 SPA
```
