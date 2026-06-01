import type { CalorieGoal, DailyLog } from '../types'

// Returns the active calorie goal target for a given date by walking the goal history.
// Assumes calorieGoals is sorted newest-first by createdAt (matching the store).
export function getTargetForDate(date: string, calorieGoals: CalorieGoal[]): number | null {
  const d = new Date(date + 'T00:00:00Z')
  const goal = calorieGoals.find(g => {
    const gDay = new Date(g.createdAt)
    gDay.setUTCHours(0, 0, 0, 0)
    return gDay <= d
  })
  return goal?.targetCalories ?? null
}

// Returns the average weight over the 7-day window [anchor-6 .. anchor], ignoring days with no
// weight entry. Returns null when no weight is logged in the window.
export function avg7DayWeight(anchor: Date, logMap: Map<string, DailyLog>): number | null {
  let sum = 0, count = 0
  for (let i = 6; i >= 0; i--) {
    const d = new Date(anchor)
    d.setUTCDate(anchor.getUTCDate() - i)
    const log = logMap.get(d.toISOString().slice(0, 10))
    if (log?.weightKg != null) { sum += log.weightKg; count++ }
  }
  return count > 0 ? sum / count : null
}

// Estimates TDEE from average daily calories adjusted for the measured weight change rate.
// Uses the first and last entries in weightEntries (sorted by date) to derive kg/day.
// Returns null when there are fewer than 5 calorie days, fewer than 2 weight entries,
// or the weight entries span fewer than 3 days.
export function estimateMonthlyTdee(
  avgCaloriesPerDay: number,
  calorieDays: number,
  weightEntries: { date: string; weightKg: number }[],
): number | null {
  if (calorieDays < 5 || weightEntries.length < 2) return null
  const sorted = [...weightEntries].sort((a, b) => a.date.localeCompare(b.date))
  const first = sorted[0]
  const last  = sorted[sorted.length - 1]
  const daysBetween =
    (new Date(last.date + 'T00:00:00Z').getTime() - new Date(first.date + 'T00:00:00Z').getTime()) / 86400000
  if (daysBetween < 3) return null
  const weightChangePerDay = (last.weightKg - first.weightKg) / daysBetween
  return Math.round(avgCaloriesPerDay - weightChangePerDay * 7700)
}
