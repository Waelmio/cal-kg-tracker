# Weight Loss Tracker

A weight loss tracking app with a C# .NET 10 backend and Vue 3 frontend.

## Stack

- **Backend**: .NET 10 Web API · Entity Framework Core · SQLite · Swagger
- **Frontend**: Vue 3 · TypeScript · Vite · Pinia · Tailwind CSS · Chart.js

## Running the app

### Backend

```bash
cd backend/WeightTracker.Api
dotnet run
# API runs at http://localhost:5000
# Swagger UI at http://localhost:5000/swagger
```

### Frontend

```bash
cd frontend/weight-tracker-ui
npm run dev
# App runs at http://localhost:5173
```

## API Endpoints

| Method | Path | Description |
|---|---|---|
| GET/POST | `/api/weight-entries` | List / create entries |
| GET/PUT/DELETE | `/api/weight-entries/{id}` | Get / update / delete entry |
| GET/POST | `/api/goals` | List / create goals |
| GET | `/api/goals/active` | Get active goal |
| GET/PUT/DELETE | `/api/goals/{id}` | Get / update / delete goal |
| GET | `/api/dashboard` | Aggregated stats |
| GET/PUT | `/api/settings` | User settings (height, unit) |

## Features

- Log daily weight entries with optional notes
- Set weight loss goals with target date and weight
- Dashboard: current weight, BMI, 7-day trend, goal progress bar
- Weight history chart (Chart.js line chart)
- kg / lbs unit toggle (stored internally as kg)
- SQLite database, auto-migrated on first run
