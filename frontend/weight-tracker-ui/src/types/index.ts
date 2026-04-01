export type WeightUnit = 'kg' | 'lbs'

export interface DailyLog {
  id: number
  date: string
  weightKg: number | null
  caloriesKcal: number | null
  notes: string | null
  calorieTarget: number | null
  isCheatDay: boolean
  createdAt: string
  updatedAt: string
}

export interface Goal {
  id: number
  targetWeightKg: number
  targetDate: string
  startingWeightKg: number | null
  startDate: string
  notes: string | null
  createdAt: string
}

export interface DashboardData {
  // Today
  currentWeightKg: number | null
  todayCaloriesKcal: number | null
  dailyCalorieTarget: number | null

  // Weekly
  weeklyAvgCalories: number | null
  weeklyCalorieTarget: number | null
  avgWeight7Days: number | null
  avgWeight7DaysTrend: number | null
  weightTrend30Days: number | null

  // Goal
  activeGoal: Goal | null
  goalProgressPercent: number | null
  kgToGoal: number | null
  projectedGoalDate: string | null

  // Extra
  bmi: number | null
  totalEntries: number
  estimatedTdeeKcal: number | null
  calorieStreakDays: number
  calorieStreakNextDays: number | null
  calorieStreakNextExcessKcal: number | null
  weightVolatilityKg: number | null
  weightChangeRateKgPerWeek: number | null
  weeklyCalorieDeficit: number | null
  weeklyCalorieDeficitDays: number
  weeklyCalorieDeficitVsTdee: number | null
  overallCalorieDeficit: number | null
  overallCalorieDeficitVsTarget: number | null
  overallCalorieDeficitDays: number
}

export interface CalorieGoal {
  id: number
  targetCalories: number
  createdAt: string
}

export interface UserSettings {
  heightCm: number | null
  preferredUnit: WeightUnit
  tdeeKcal: number | null
}

export interface TdeeComputation {
  estimatedTdeeKcal: number
  avgDailyCaloriesKcal: number
  weightTrendKgPerDay: number
  calorieDataPoints: number
  weightDataPoints: number
  periodDays: number
}

export interface UpsertDailyLogRequest {
  date: string
  weightKg?: number | null
  caloriesKcal?: number | null
  notes?: string | null
}

export interface CreateGoalRequest {
  targetWeightKg: number
  targetDate: string
  startDate?: string
  notes?: string
}
