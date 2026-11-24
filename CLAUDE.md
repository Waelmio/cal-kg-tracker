# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

### Root (Makefile)
```bash
make dev             # Start backend + frontend in parallel
make test            # Run all tests in parallel
make dev-backend     # Backend only
make dev-frontend    # Frontend only
make test-backend    # Backend tests only (xUnit, via backend/WeightTracker.slnx)
make test-frontend   # Frontend tests only (Vitest)
```

### Backend (`backend/WeightTracker.Api/`)
```bash
dotnet build
dotnet run                              # API at http://localhost:5118, Swagger at /swagger
dotnet-ef migrations add <Name>         # Add EF Core migration
dotnet-ef migrations remove             # Remove last migration
```

### Frontend (`frontend/weight-tracker-ui/`)
```bash
pnpm run dev      # Dev server at http://localhost:5173
pnpm run build    # Type-check + production build
pnpm run preview  # Preview production build at http://localhost:4173
pnpm test         # Run Vitest tests
```

## Architecture

### Data Model

`DailyLog`: one row per calendar day with nullable `WeightKg` (2 decimals), `CaloriesKcal`, `Notes`, `IsCheatDay` and `CaloriesSetOn` (local date the calories value last changed). 0 kcal is a real value; empty means not logged. Nothing is stored for a day nobody logged: planned days are computed, never stored. `PUT` replaces the whole day; the row is removed when nothing is left.

`Goal` (weight): `Kind` (`Reach`/`Maintain`), nullable `TargetDate`, `EndedOn`/`EndReason`. Active = newest with `EndedOn == null`. Ended goals never revive.
`CalorieGoal`: `EffectiveFrom` date, nullable target (null = off). `TdeeEntry`: TDEE history, `EffectiveFrom`. `UserSettings`: singleton, `HeightCm`, `PreferredUnit`, `AveragingWeeks` (1 to 5).

The calorie target is met **on average** over `AveragingWeeks`, not daily.

### Backend

Controllers are thin. All statistics live in `Stats/`:
- `StatsCalculator.cs`: pure functions over `StatsData` (week/month figures, status colors, budget, streak, pace, TDEE estimate, diet effect). Rules and constants: `worktree/redesign/PLAN.md` section 3 (git-ignored, local).
- `GoalStatusCalculator.cs`, `StatsService.cs` (loads data, builds DTOs), `Controllers/StatsController.cs`.
- Tests: `WeightTracker.UTest/Fixtures/fixture.json` with today = 2026-11-24 (expected values in `StatsCalculatorTests`, repeated end to end in `WeightTracker.ITest/Tests/StatsEndpointTests.cs` on SQLite and Postgres).
- Migrations in `Migrations/Sqlite` and `Migrations/Postgres`; `MigrationTests` migrates a legacy-shaped database. Run automatically at startup.
- `DailyLogService.SetCaloriesAsync/SetWeightAsync` are single-field writes for imports.
- The target in force on a day (cheat day, TDEE) is only computed in `StatsCalculator.Target`.
- Weight is stored in kg. Unit conversion is display only. The client sends its local date as `today`; it is required wherever the date matters.

### Frontend

Vue 3 + Pinia. No statistic is computed in the frontend; it formats what `/api/stats/*` returns.
- `stores/app.ts`: settings, local `today`, and `version`; every mutation calls `refresh()` and views refetch via `composables/useAsyncData.ts`.
- `stores/logSheet.ts` + `components/daylog/LogSheet.vue`: the single log dialog (full screen below 640 px). Its data is one request per week (`/api/stats/log-week`), kept in `stores/logWeeks.ts` until anything changes (`composables/useKeyedCache.ts`, also behind `stores/calendarMonths.ts`, which preloads the months around the one shown). The "rest of the week" hint is the only figure computed in the UI (on request).
- `components/ui/`: `Tile`, `TileRow`, `StatusChip`, `InfoTip`, `Modal`, `ErrorNote`, `Skeleton`.
- Scoped class names must not be Tailwind utilities (`outline`, `label`, `table`...): Tailwind would style them too.
- `utils/dates.ts` (local dates), `utils/format.ts` (wording, colors), `utils/units.ts`, `utils/chart.ts` (time-axis geometry).

### Pages

| Route | View |
|---|---|
| `/` | `DashboardView`: tiles, calendar, average chart, goal card |
| `/history` | `HistoryView` (`/log` redirects here) |
| `/goals` | `GoalsView`: weight goal, calorie target, TDEE (`/goal` redirects here) |
| `/settings` | `SettingsView` |
