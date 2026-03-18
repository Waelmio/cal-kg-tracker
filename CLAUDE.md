# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

### Backend (`backend/WeightTracker.Api/`)
```bash
dotnet build
dotnet run                              # API at http://localhost:5118, Swagger at /swagger
dotnet-ef migrations add <Name>         # Add EF Core migration
dotnet-ef migrations remove             # Remove last migration
```

### Frontend (`frontend/weight-tracker-ui/`)
```bash
npm run dev      # Dev server at http://localhost:5173
npm run build    # Type-check + production build
npm run preview  # Preview production build at http://localhost:4173
```

There are no tests in this project (neither backend nor frontend test frameworks are configured).

## Architecture

### Data Model

The core entity is `DailyLog` — one row per calendar day with nullable `WeightKg` and `CaloriesKcal`. Both fields are independent: you can log only weight, only calories, or both. Deleting a field nulls it; the row is automatically removed when both become null.

`Goal` holds a single weight target. The active goal is always the most recent row (ordered by `CreatedAt` desc) — no `IsActive` flag. Creating a new goal does not remove the old one; they coexist in history.

`CalorieGoal` tracks daily calorie targets as a history (same pattern as `Goal` — active = most recent by `CreatedAt` desc, old entries coexist).

`UserSettings` is a singleton (always `Id = 1`) seeded at migration time. It holds `HeightCm`, `PreferredUnit` (`kg`/`lbs`), and a cached `Tdee` value.

### Backend

Service-controller pattern. Controllers are thin wrappers; all logic lives in services.

- `Data/AppDbContext.cs` — EF Core context with unique index on `DailyLog.Date`. Auto-migrates at startup via `db.Database.Migrate()` in `Program.cs`.
- `Services/DailyLogService.cs` — `UpsertAsync` creates or updates the day's row, only touching fields that are provided. `DeleteWeightAsync` / `DeleteCaloriesAsync` null the respective field and remove the row if both are null.
- `Services/DashboardService.cs` — all aggregation happens server-side: 7-day weight trend, weekly avg calories, goal progress %, projected goal date (extrapolated from 7-day weight rate).
- `Services/TdeeComputationService.cs` — linear regression on weight vs. time; requires ≥7 calorie logs and ≥3 weight logs. Uses 7700 kcal/kg conversion factor.
- Weight is always stored and transmitted in kg. Unit conversion is a display concern only.
- SQLite database file: `backend/WeightTracker.Api/weighttracker.db` (created at runtime).
- CORS allows `http://localhost:5173` and `http://localhost:4173` (configured via `AllowedOrigins` in `appsettings.json`).

### API Endpoints

| Method | Path | Notes |
|---|---|---|
| GET/PUT | `/api/daily-logs` / `/api/daily-logs/{date}` | date = `yyyy-MM-dd` |
| DELETE | `/api/daily-logs/{date}` | whole day |
| DELETE | `/api/daily-logs/{date}/weight` | weight only |
| DELETE | `/api/daily-logs/{date}/calories` | calories only |
| GET/POST/DELETE | `/api/goals/active`, `/api/goals`, `/api/goals/{id}` | |
| GET/POST/DELETE | `/api/calorie-goals/active`, `/api/calorie-goals`, `/api/calorie-goals/{id}` | |
| GET | `/api/dashboard` | single aggregated response |
| GET | `/api/tdee` | TDEE estimate via linear regression |
| GET/PUT | `/api/settings` | |

### Frontend

Pinia stores mediate all API calls. Views call store actions; components read from store state. The dashboard store is refreshed after any mutation via `await dashboard.fetch()`.

- `src/api/dailyLog.ts` — mirrors the backend's independent weight/calorie delete endpoints.
- `src/stores/dailyLog.ts` — `upsert` keeps the local list sorted by date desc after every write.
- `src/utils/units.ts` — all weight conversion logic (`displayWeight`, `toKg`, `formatWeight`, `bmiLabel`). Weight travels as kg everywhere; convert only at the UI boundary.
- `src/style.css` — global Tailwind `@layer components` defines `.input`, `.btn-primary`, `.btn-secondary`, `.label`.
- Vite proxies `/api` → `http://localhost:5118` in dev (configured in `vite.config.ts`).

### Pages

| Route | View | Purpose |
|---|---|---|
| `/` | `DashboardView` | Stats, calorie bar, weight chart, goal progress |
| `/log` | `LogView` | Log/edit weight + calories for any date |
| `/history` | `HistoryView` | Full log table + chart, inline edit |
| `/goal` | `GoalView` | Set/replace weight goal |
