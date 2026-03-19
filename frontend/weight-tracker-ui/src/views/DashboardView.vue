<template>
  <div class="max-w-3xl mx-auto px-4 py-6 space-y-4">
    <div class="flex items-center justify-between">
      <h1 class="text-xl font-bold text-gray-800">Dashboard</h1>
      <div class="flex gap-2">
        <button @click="showWeight = true" class="btn-primary text-sm">+ Weight</button>
        <button @click="showCalories = true" class="bg-emerald-500 hover:bg-emerald-600 text-white font-medium text-sm px-4 py-2 rounded-lg transition-colors">+ Calories</button>
      </div>
    </div>

    <!-- Skeleton -->
    <div v-if="dashboard.loading" class="grid grid-cols-2 gap-3">
      <div v-for="i in 4" :key="i" class="bg-gray-100 animate-pulse h-20 rounded-xl" />
    </div>

    <template v-else-if="dashboard.data">
      <div class="grid grid-cols-2 sm:grid-cols-3 gap-3">
        <StatCard
          label="Weight"
          :value="dashboard.data.avgWeight7Days != null ? displayWeight(dashboard.data.avgWeight7Days, unit) : null"
          :unit="unit"
          :trailing-value="dashboard.data.avgWeight7DaysTrend != null ? `${trendArrow(dashboard.data.avgWeight7DaysTrend)} ${trendDisplay(dashboard.data.avgWeight7DaysTrend)} ${unit}` : null"
          :trailing-class="trendClass(dashboard.data.avgWeight7DaysTrend)"
          :sub="dashboard.data.weightVolatilityKg != null ? `± ${displayWeight(dashboard.data.weightVolatilityKg, unit).toFixed(2)} ${unit} volatility` : 'Not enough data'"
          :sub-class="volatilityClass(dashboard.data.weightVolatilityKg)"
          tooltip="7 days average, with the trend against the previous 7d." />
          <StatCard
          label="Weight Loss Rate"
          :value="dashboard.data.weightChangeRateKgPerWeek != null ? `${dashboard.data.weightChangeRateKgPerWeek > 0 ? '+' : ''}${displayWeight(Math.abs(dashboard.data.weightChangeRateKgPerWeek), unit).toFixed(2)}` : null"
          :unit="unit + '/week'"
          :value-class="trendClass(dashboard.data.weightChangeRateKgPerWeek)"
          sub="30-day trend" />
          <StatCard
          label="🔥 Calory Streak 🔥"
          :value="dashboard.data.calorieStreakDays > 0 ? dashboard.data.calorieStreakDays : null"
          :unit="dashboard.data.calorieStreakDays > 0 ? 'days' : undefined"
          :sub="dashboard.data.calorieStreakDays > 0 ? 'running avg streak' : 'no streak yet'"
          :sub-html="streakNextLabel(dashboard.data)"
          :value-class="dashboard.data.calorieStreakDays > 0 ? 'text-emerald-500' : 'text-gray-400'" />
          <StatCard
          label="Weekly Avg Calories"
          :value="dashboard.data.weeklyAvgCalories != null ? Math.round(dashboard.data.weeklyAvgCalories) : null"
          unit="kcal"
          :value-class="calorieAvgClass"
          :sub="dashboard.data.dailyCalorieTarget != null ? `       /${dashboard.data.dailyCalorieTarget} kcal` : undefined" />
          <StatCard
          label="Weekly Deficit"
          :value="tdeeWeeklyDeficit != null ? `${tdeeWeeklyDeficit > 0 ? '-' : '+'}${Math.abs(tdeeWeeklyDeficit)}` : (dashboard.data.weeklyCalorieDeficit != null ? `${dashboard.data.weeklyCalorieDeficit > 0 ? '-' : '+'}${Math.abs(dashboard.data.weeklyCalorieDeficit)}` : null)"
          :unit="tdeeWeeklyDeficit != null ? 'kcal vs TDEE' : 'kcal vs target'"
          :value-class="deficitClass(tdeeWeeklyDeficit ?? dashboard.data.weeklyCalorieDeficit)"
          :sub-html="weeklyDeficitSubHtml" />
          <!-- <StatCard
          label="Est. TDEE"
          :value="dashboard.data.estimatedTdeeKcal"
          unit="kcal"
          sub="Not used in calculations"
          tooltip="Needs 15+ days with both weight and calories logged in the last 30 days"
          :only-when-empty="true" /> -->
          <StatCard
          label="Overall Deficit"
          :value="overallGoalDeficit != null ? `${(overallGoalDeficit.vsTdee ?? overallGoalDeficit.vsTarget) >= 0 ? '-' : '+'}${Math.abs(overallGoalDeficit.vsTdee ?? overallGoalDeficit.vsTarget).toLocaleString()}` : null"
          :unit="overallGoalDeficit?.vsTdee != null ? 'kcal vs TDEE' : 'kcal vs target'"
          :value-class="deficitClass(overallGoalDeficit != null ? (overallGoalDeficit.vsTdee ?? overallGoalDeficit.vsTarget) : null)"
          :sub-html="overallDeficitSubHtml"
          tooltip="Cumulative calorie deficit since the current goal started" />
        </div>

      <CalorieBar
        :calories="dashboard.data.todayCaloriesKcal"
        :target="dashboard.data.dailyCalorieTarget" />

      <WeeklyStrip
        :logs="logStore.logs"
        :calorie-goals="calorieGoalsStore.goals"
        :unit="unit"
        @need-from="onNeedFrom"
        @edit-weight="date => { editDate = date; showWeight = true }"
        @edit-calories="date => { editDate = date; showCalories = true }" />

      <ProgressCard
        :goal="dashboard.data.activeGoal"
        :progress-percent="dashboard.data.goalProgressPercent"
        :kg-to-goal="dashboard.data.kgToGoal"
        :projected-date="dashboard.data.projectedGoalDate"
        :unit="unit"
        :current-weight-kg="dashboard.data.avgWeight7Days" />

      <WeightChart
        :logs="logStore.logs"
        :unit="unit"
        :goal="dashboard.data.activeGoal"
        :tdee-kcal="settingsStore.settings.tdeeKcal"
        :daily-calorie-target="dashboard.data.dailyCalorieTarget"
        @need-from="onChartNeedFrom" />
    </template>

    <p v-else-if="dashboard.error" class="text-red-500 text-sm">{{ dashboard.error }}</p>

    <WeightLogModal v-if="showWeight" :initial-date="editDate" @close="showWeight = false; editDate = undefined" @saved="onSaved" />
    <CaloriesLogModal v-if="showCalories" :initial-date="editDate" @close="showCalories = false; editDate = undefined" @saved="onSaved" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useDashboardStore } from '../stores/dashboard'
import { useDailyLogStore } from '../stores/dailyLog'
import { useSettingsStore } from '../stores/settings'
import { useCalorieGoalsStore } from '../stores/calorieGoals'
import { displayWeight } from '../utils/units'
import type { DashboardData } from '../types'
import StatCard from '../components/dashboard/StatCard.vue'
import CalorieBar from '../components/dashboard/CalorieBar.vue'
import ProgressCard from '../components/dashboard/ProgressCard.vue'
import WeightChart from '../components/dashboard/WeightChart.vue'
import WeeklyStrip from '../components/dashboard/WeeklyStrip.vue'
import WeightLogModal from '../components/log/WeightLogModal.vue'
import CaloriesLogModal from '../components/log/CaloriesLogModal.vue'

const dashboard = useDashboardStore()
const logStore = useDailyLogStore()
const settingsStore = useSettingsStore()
const calorieGoalsStore = useCalorieGoalsStore()
const unit = computed(() => settingsStore.settings.preferredUnit)

const showWeight = ref(false)
const showCalories = ref(false)
const editDate = ref<string | undefined>(undefined)

const tdeeWeeklyDeficit = computed(() => {
  const d = dashboard.data
  if (d == null) return null
  const { weeklyCalorieDeficit, weeklyCalorieDeficitDays, dailyCalorieTarget } = d
  const tdee = settingsStore.settings.tdeeKcal
  if (weeklyCalorieDeficit == null || !weeklyCalorieDeficitDays || dailyCalorieTarget == null || tdee == null) return null
  const weeklySum = weeklyCalorieDeficitDays * dailyCalorieTarget - weeklyCalorieDeficit
  return weeklyCalorieDeficitDays * tdee - weeklySum
})

const overallGoalDeficit = computed(() => {
  const goal = dashboard.data?.activeGoal
  if (!goal) return null
  const tdee = settingsStore.settings.tdeeKcal
  const startDate = goal.startDate
  const logsFromGoal = logStore.logs.filter(l => l.date >= startDate && l.caloriesKcal != null)
  if (logsFromGoal.length === 0) return null
  let vsTarget = 0
  let vsTdee = 0
  for (const log of logsFromGoal) {
    const target = calorieGoalsStore.getTargetForDate(log.date)
    if (target != null) vsTarget += log.caloriesKcal! - target
    if (tdee != null) vsTdee += log.caloriesKcal! - tdee
  }
  // Negate so positive = deficit = good (same convention as weeklyCalorieDeficit)
  return {
    vsTarget: Math.round(-vsTarget),
    vsTdee: tdee != null ? Math.round(-vsTdee) : null,
  }
})

const overallDeficitSubHtml = computed(() => {
  const d = overallGoalDeficit.value
  if (d == null) return undefined
  const primary = d.vsTdee ?? d.vsTarget
  const kgEquiv = displayWeight(Math.abs(primary) / 7700, unit.value).toFixed(2)
  const kgPart = `≈ <span class='${deficitClass(primary)}'>${kgEquiv} ${unit.value} ${primary >= 0 ? 'loss' : 'gain'}</span>`
  const targetPart = d.vsTdee != null
    ? ` or <span class='${deficitClass(d.vsTarget)}'>${d.vsTarget >= 0 ? '-' : '+'}${Math.abs(d.vsTarget).toLocaleString()} kcal</span> vs target`
    : ''
  return kgPart + targetPart
})

const weeklyDeficitSubHtml = computed(() => {
  const wcd = dashboard.data?.weeklyCalorieDeficit
  const tdeeD = tdeeWeeklyDeficit.value
  if (tdeeD == null || wcd == null) return undefined
  const kgEquiv = displayWeight(Math.abs(tdeeD) / 7700, unit.value).toFixed(2)
  return `≈ <span class='${deficitClass(tdeeD)}'>${kgEquiv} ${unit.value} ${tdeeD > 0 ? 'loss' : 'gain'}</span>`
    + ` or <span class='${deficitClass(wcd)}'>${wcd > 0 ? '-' : '+'}${Math.abs(wcd)} kcal</span> vs target`
})

const calorieAvgClass = computed(() => {
  const avg = dashboard.data?.weeklyAvgCalories
  const target = dashboard.data?.dailyCalorieTarget
  if (avg == null || target == null) return 'text-gray-800'
  return avg > target ? 'text-red-500' : 'text-emerald-500'
})

function volatilityClass(volatilityKg: number | null): string {
  if (volatilityKg == null) return 'text-gray-400'
  if (volatilityKg < 0.5) return 'text-emerald-500'
  if (volatilityKg < 1.0) return 'text-orange-400'
  return 'text-red-500'
}

function trendArrow(trend: number | null): string {
  if (trend == null) return ''
  return trend < 0 ? '↓' : '↑'
}

function trendDisplay(trend: number | null): string | null {
  if (trend == null) return null
  return displayWeight(Math.abs(trend), unit.value).toFixed(2)
}

function trendClass(trend: number | null): string {
  if (trend == null) return 'text-gray-800'
  return trend < 0 ? 'text-emerald-500' : 'text-red-500'
}

// Positive = good (e.g. calorie deficit vs target)
function deficitClass(n: number | null): string {
  if (n == null) return ''
  return n >= 0 ? 'text-emerald-500' : 'text-red-500'
}

function streakNextLabel(data: DashboardData): string | undefined {
  if (data.calorieStreakNextDays == null || data.calorieStreakNextExcessKcal == null) return undefined
  return `<strong>−${data.calorieStreakNextExcessKcal} kcal</strong> from ${data.calorieStreakNextDays}-day streak`
}

async function onSaved() {
  await Promise.all([logStore.fetchAll({ limit: 70 }), dashboard.fetch()])
}

async function onChartNeedFrom(from: string | null) {
  if (from === null) {
    await logStore.fetchAll()
    return
  }
  const oldest = logStore.logs.length > 0 ? logStore.logs[logStore.logs.length - 1].date : null
  if (oldest == null || from < oldest) {
    await logStore.fetchAll({ from })
  }
}

async function onNeedFrom(from: string) {
  const oldest = logStore.logs.length > 0 ? logStore.logs[logStore.logs.length - 1].date : null
  if (oldest == null || from < oldest) {
    const d = new Date(from)
    d.setDate(d.getDate() - 21) // fetch 3 extra weeks proactively
    await logStore.fetchAll({ from: d.toISOString().slice(0, 10) })
  }
}

onMounted(async () => {
  await Promise.all([
    dashboard.fetch(),
    logStore.fetchAll({ limit: 70 }),
  ])
})
</script>
