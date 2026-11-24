# API Reference

Base URL: `http://localhost:5118` (local). All endpoints are prefixed with `/api`. Swagger UI is at `/swagger` when running locally.

Every statistic is computed server-side. Everything that depends on the date requires `?today=yyyy-MM-dd`, the client's **local** date (400 without it). The server never uses UTC "today". Errors are ProblemDetails with a readable `title`.

## Daily logs

| Method | Path | Notes |
|---|---|---|
| GET | `/api/daily-logs?from&to&limit` | Logs, newest first |
| GET | `/api/daily-logs/{date}` | One day |
| PUT | `/api/daily-logs/{date}?today=` | **Full replace** of the day: null clears a field, 0 kcal is kept, note and cheat day included. 204 when nothing is left and the row was removed |
| DELETE | `/api/daily-logs/{date}` | Delete entry: the whole day, note and cheat mark included |

`caloriesSetOn` is the local date the calories value last changed (tells a value logged in advance from one logged that day).

## Stats

| Method | Path | Notes |
|---|---|---|
| GET | `/api/stats/dashboard?today=` | Tiles, budget, streak, deficits, weight, goal status, weight series |
| GET | `/api/stats/calendar?from&to&today=` | Days (calories, target, status, planned, inGoal, notes), weeks and whole months, plus today. Up to ten years |
| GET | `/api/stats/log-week?date&today=` | The week around a date, with everything the log sheet shows and edits |
| GET | `/api/stats/budget-by-window?today=` | Today's budget for each averaging window (Settings preview) |
| GET | `/api/stats/periods?unit=week\|month&count&today=` | Average calories chart |
| GET | `/api/stats/month/{yyyy-MM}?today=` | Month summary |
| GET | `/api/stats/weights?from&today=` | Weigh-ins with their 7-day average |
| GET | `/api/stats/tdee?days=30\|60\|90&today=` | Estimate, data used, diet effect, or what is missing |

## Goals

| Method | Path | Notes |
|---|---|---|
| GET | `/api/goals` | History, newest first. `changeKg`: weight change of goals that ended |
| GET | `/api/goals/active` | Newest goal that has not ended, else 404 |
| POST | `/api/goals?today=` | Creates a goal, ends the active one as `Replaced` (`Reached` or `Ended` if it was reached or its date had passed) |
| PUT | `/api/goals/{id}` | Edit in place (not an ended goal) |
| POST | `/api/goals/{id}/end?today=` | Body `{ "reason": "Ended" \| "Reached" }` |
| POST | `/api/goals/maintain?today=` | Ends the goal as `Maintain`, keeps the target of a reached goal (the current weight otherwise), sets the calorie target |
| DELETE | `/api/goals/{id}` | Never revives an older goal |

## Calorie goals and TDEE

| Method | Path | Notes |
|---|---|---|
| GET/POST | `/api/calorie-goals` | History. POST `{ targetCalories }`, null turns the target off from `today` |
| DELETE | `/api/calorie-goals/{id}` | |
| GET/POST | `/api/tdee-entries` | TDEE history. A new entry counts from `today`; past days keep theirs. The first one covers the days logged before it |
| DELETE | `/api/tdee-entries/{id}` | Its days fall back to the entry before it |

## Other

| Method | Path | Notes |
|---|---|---|
| GET/PUT | `/api/settings` | Height, unit, `averagingWeeks` (1 to 5) |
| GET/POST/DELETE | `/api/data/export`, `/api/data/import`, `/api/data` | Export is `version: 2`. Import also accepts the older format. DELETE clears all data and resets settings |

Removed in the redesign: `prefill-week`, `/cheat-day`, `/weight` and `/calories` deletes, `/api/dashboard`, `/api/tdee`.
