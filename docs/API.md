# API Reference

Base URL: `http://localhost:5118` (local) — all endpoints are prefixed with `/api`.

Swagger UI is available at `/swagger` when running locally.

## Daily Logs

| Method | Path | Notes |
|---|---|---|
| GET | `/api/daily-logs` | All daily logs |
| GET/PUT | `/api/daily-logs/{date}` | date = `yyyy-MM-dd` |
| DELETE | `/api/daily-logs/{date}` | Delete entire day |
| DELETE | `/api/daily-logs/{date}/weight` | Null weight only |
| DELETE | `/api/daily-logs/{date}/calories` | Null calories only |

## Goals

| Method | Path | Notes |
|---|---|---|
| GET/POST | `/api/goals` | Goal history |
| GET | `/api/goals/active` | Most recent goal |
| DELETE | `/api/goals/{id}` | Delete a goal |

## Calorie Goals

| Method | Path | Notes |
|---|---|---|
| GET/POST | `/api/calorie-goals` | Calorie goal history |
| GET | `/api/calorie-goals/active` | Most recent calorie goal |
| DELETE | `/api/calorie-goals/{id}` | Delete a calorie goal |

## Other

| Method | Path | Notes |
|---|---|---|
| GET | `/api/dashboard` | Aggregated stats |
| GET | `/api/tdee` | TDEE estimate via linear regression |
| GET/PUT | `/api/settings` | Height, unit preference |
