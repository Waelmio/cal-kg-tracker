export type WeightUnit = 'kg' | 'lbs'

export type Status = 'under' | 'balanced' | 'over'

export interface DailyLog {
  id: number
  date: string
  weightKg: number | null
  caloriesKcal: number | null
  notes: string | null
  isCheatDay: boolean
  createdAt: string
  updatedAt: string
  caloriesSetOn: string | null
}

/** Body of PUT /daily-logs/{date}: a full replace of the day. */
export interface SaveDayRequest {
  weightKg: number | null
  caloriesKcal: number | null
  notes: string | null
  isCheatDay: boolean
}

export interface UserSettings {
  heightCm: number | null
  preferredUnit: WeightUnit
  averagingWeeks: number
}

export interface CalorieGoal {
  id: number
  targetCalories: number | null
  createdAt: string
  effectiveFrom: string
}

export type GoalKind = 'Reach' | 'Maintain'
export type GoalEndReason = 'Ended' | 'Reached' | 'Replaced' | 'Maintain'

export interface Goal {
  id: number
  targetWeightKg: number
  targetDate: string | null
  startingWeightKg: number | null
  startDate: string
  notes: string | null
  createdAt: string
  kind: GoalKind
  endedOn: string | null
  endReason: GoalEndReason | null
  /** Ended goals: the weight around the end date minus the starting weight. */
  changeKg?: number
}

export interface CreateGoalRequest {
  targetWeightKg: number
  targetDate: string
  startDate?: string
  startingWeightKg?: number
  notes?: string
}

export interface UpdateGoalRequest {
  targetWeightKg: number
  targetDate: string | null
  startDate: string
  startingWeightKg: number | null
  notes: string | null
}

export interface TdeeEntry {
  id: number
  kcal: number
  effectiveFrom: string
  source: 'Manual' | 'Estimate'
  createdAt: string
}

// ── Stats (all computed server-side) ──────────────────────────────────────

export interface Period {
  from: string
  to: string
  loggedDays: number
  avgCalories: number | null
  avgTarget: number | null
  gap: number
  deficitKcal: number
  deficitKg: number
  avgWeightKg: number | null
  weightChangeKg: number | null
  weighIns: number
  status: Status | null
  inGoal: boolean
}

export interface PlannedDay {
  date: string
  kcal: number
  userLogged: boolean
}

export interface Budget {
  todayBudget: number
  eatenToday: number | null
  todayLeft: number | null
  averageSoFar: number | null
  avgTargetSoFar: number | null
  expectedAverage: number
  expectedDeficitKcal: number | null
  /** False when even this plan leaves the averaging window over target. */
  reachesTarget: boolean
  days: PlannedDay[]
  /** The next weeks, planned the same way (as far as the averaging window reaches). */
  futureDays: PlannedDay[]
}

export interface StreakRow {
  days: number
  avgCalories: number | null
  avgGap: number | null
}

export interface GoalStatus {
  id: number
  kind: GoalKind
  state: 'none' | 'active' | 'reached' | 'datePassed'
  pace: 'ahead' | 'onTrack' | 'behind' | null
  losing: boolean
  targetWeightKg: number
  startingWeightKg: number | null
  startDate: string
  targetDate: string | null
  endedOn: string | null
  endReason: GoalEndReason | null
  currentKg: number | null
  progressPercent: number | null
  remainingKg: number | null
  neededKgPerWeek: number | null
  paceKgPerWeek: number | null
  projectedDate: string | null
  lateWeeks: number | null
  reachedOn: string | null
  remainingAtTargetDateKg: number | null
  maintainBandKg: number
  withinBand: boolean | null
}

export interface DashboardStats {
  today: string
  /** The oldest day with anything logged. */
  firstLog: string | null
  averagingWeeks: number
  calorieTarget: number | null
  tdee: number | null
  budget: Budget | null
  week: Period
  streak: { days: number; nextGap: number | null; rows: StreakRow[] }
  deficits: {
    weekly: { kcal: number; kg: number; expectedKcal: number | null; expectedKg: number | null; loggedDays: number }
    overall: { kcal: number; kg: number; gapKcal: number; loggedDays: number; totalDays: number; since: string; sinceGoalStart: boolean }
  }
  weight: { kg: number | null; isAverage: boolean; date: string | null; paceKgPerWeek: number | null; paceWeighIns: number; bmi: number | null; bmiAtGoal: number | null }
  goal: GoalStatus | null
  weightSeries: { date: string; kg: number; avg7: number }[]
  targetComparison: {
    target: number | null; tdee: number | null
    targetDeficit: number | null; targetKgPerWeek: number | null
    last4Avg: number | null; last4Deficit: number | null; last4KgPerWeek: number | null
    onTimeKcal: number | null
  }
}

export interface CalendarDay {
  date: string
  calories: number | null
  target: number | null
  status: Status | null
  planned: number | null
  plannedLogged: boolean
  inGoal: boolean
  weightKg: number | null
  isCheatDay: boolean
  notes: string | null
}

/** One day as the log sheet edits it. */
export interface LogDay {
  date: string
  calories: number | null
  weightKg: number | null
  notes: string | null
  isCheatDay: boolean
  status: Status | null
  planned: number | null
  /** The plain target, whether or not the day is a cheat day. */
  calorieTarget: number | null
  tdee: number | null
  /** The latest weigh-in before this day, however long ago. */
  lastWeightKg: number | null
}

export interface LogWeek {
  monday: string
  days: LogDay[]
  /** Only for the current week: what today and the other free days share. */
  thisWeek: { todayBudget: number; pool: number; otherDays: number } | null
}

export interface CalendarWeek {
  monday: string
  isoWeek: number
  isoYear: number
  figures: Period
}

export interface Calendar {
  today: string
  days: CalendarDay[]
  weeks: CalendarWeek[]
}

export interface TdeeEstimate {
  kcal: number
  avgCalories: number
  weightTrendGramsPerDay: number
  adjustment: number
  weighIns: number
  loggedDays: number
  spanDays: number
  first: string
  last: string
  periodDays: number
}

export interface TdeeStats {
  days: number
  estimate: TdeeEstimate | null
  missing: string | null
  weighIns: number
  spanDays: number
  loggedDays: number
  currentTdee: number | null
  suggestUpdate: boolean
  note: string | null
  diet: { lostKg: number; rows: { key: string; label: string; kcal: number }[]; footer: string } | null
}

export interface MonthSummary {
  month: string
  figures: Period
  daysInMonth: number
  cheatDays: number
  previousAvgWeightKg: number | null
  startWeightKg: number | null
  endWeightKg: number | null
  weeks: CalendarWeek[]
  tdee: TdeeEstimate | null
}
