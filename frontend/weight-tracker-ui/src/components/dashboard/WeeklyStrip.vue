<template>
  <div class="bg-white rounded-xl border border-gray-200 p-4">
    <div class="mb-3 flex items-center justify-between">
      <div>
        <p class="text-xs font-medium text-gray-400 uppercase tracking-wide">Weekly Overview</p>
        <p v-if="selectedWeek" class="text-xs text-gray-400 mt-0.5">{{ selectedWeek.dateRange }}</p>
      </div>
      <button
        v-if="!selectedWeek?.isCurrent"
        @click="jumpToCurrent"
        class="text-xs font-medium text-blue-500 bg-blue-50 hover:bg-blue-100 px-2 py-0.5 rounded-full transition-colors"
      >↩ This week</button>
    </div>

    <div class="flex items-center gap-1.5">
      <button @click="goBack" class="nav-btn">‹</button>

      <div class="flex gap-2 flex-1 min-w-0">
        <button
          v-for="(w, i) in weekData"
          :key="w.key"
          @click="toggle(i)"
          :class="chipClass(w, i === selectedIndex)"
          class="relative flex flex-col items-center flex-1 min-w-0 py-2 rounded-xl text-xs font-semibold transition-all"
        >
          <span v-if="w.isCurrent" class="absolute -bottom-1.5 left-1/2 -translate-x-1/2 w-2 h-2 bg-blue-500 rounded-full ring-2 ring-white" />
          <span>W{{ w.week }}</span>
          <span class="font-normal text-[10px] leading-tight" :class="w.isEmpty ? 'opacity-25' : 'opacity-80'">
            {{ w.avgCalories != null ? w.avgCalories : '—' }}
          </span>
          <span class="font-normal text-[10px] leading-tight" :class="w.isEmpty ? 'opacity-20' : 'opacity-70'">
            {{ w.avgWeightDisplay ?? '·' }}
          </span>
        </button>
      </div>

      <button @click="goForward" class="nav-btn">›</button>
    </div>

    <Transition name="expand">
      <div v-if="selectedWeek" class="mt-3 border-t border-gray-100 pt-3">
        <div
          v-for="day in selectedWeek.days"
          :key="day.date"
          class="flex items-center justify-between py-1.5 text-xs sm:text-sm border-b border-gray-50 last:border-0 gap-1"
        >
          <span class="text-gray-500 w-24 sm:w-32 shrink-0 truncate">{{ day.label }}</span>
          <button
            @click="emit('edit-weight', day.date)"
            class="text-gray-700 w-16 sm:w-20 text-right hover:text-blue-500 hover:underline transition-colors shrink-0"
          >{{ day.weightDisplay }}</button>
          <button
            @click="emit('edit-calories', day.date)"
            class="flex-1 text-right hover:underline transition-colors"
            :class="calorieClass(day.calories, day.target)"
          >{{ day.calories != null ? day.calories + ' kcal' : '—' }}</button>
          <span class="w-10 sm:w-12 text-right font-light shrink-0" :class="calorieClass(day.calories, day.target)">
            {{ day.target != null ? `/ ${day.target}` : '' }}
          </span>
        </div>
      </div>
    </Transition>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import type { DailyLog, CalorieGoal, WeightUnit } from '../../types'
import { formatWeight } from '../../utils/units'
import { getISOWeek, getISOWeekYear, getWeekDates } from '../../utils/weeks'

const emit = defineEmits<{
  'need-from': [date: string]
  'edit-weight': [date: string]
  'edit-calories': [date: string]
}>()

const props = defineProps<{
  logs: DailyLog[]
  calorieGoals: CalorieGoal[]
  unit: WeightUnit
}>()

function getTargetForDate(date: string): number | null {
  const d = new Date(date)
  const effective = props.calorieGoals.find((g) => {
    const goalDay = new Date(g.createdAt)
    goalDay.setHours(0, 0, 0, 0)
    return goalDay <= d
  })
  return effective?.targetCalories ?? null
}

type Status = 'green' | 'red' | 'gray'

interface DayRow {
  date: string
  label: string
  weightDisplay: string
  calories: number | null
  target: number | null
}

interface WeekItem {
  year: number
  week: number
  key: string
  status: Status
  isEmpty: boolean
  isCurrent: boolean
  dateRange: string
  avgCalories: number | null
  avgWeightDisplay: string | null
  weekTarget: number | null
  days: DayRow[]
}

const today = new Date()
const currentISOWeek = getISOWeek(today)
const currentISOWeekYear = getISOWeekYear(today)

const offset = ref(-1) // -1 = current week at index 4, one future week at index 5
const selectedIndex = ref<number>(4)

function goBack() {
  offset.value += 1
  const d = new Date()
  d.setDate(d.getDate() - (5 + offset.value) * 7)
  const dayNum = d.getDay() || 7
  d.setDate(d.getDate() - (dayNum - 1))
  emit('need-from', d.toISOString().slice(0, 10))
}
function goForward() { offset.value -= 1 }

const logMap = computed(() => {
  const m = new Map<string, DailyLog>()
  for (const log of props.logs) m.set(log.date, log)
  return m
})

const dayFmt = new Intl.DateTimeFormat('en', { weekday: 'short', day: 'numeric', month: 'short' })
const monthDayFmt = new Intl.DateTimeFormat('en', { month: 'short', day: 'numeric' })

const weekData = computed<WeekItem[]>(() => {
  const now = new Date()
  const weeks = Array.from({ length: 6 }, (_, i) => {
    const d = new Date(now)
    d.setDate(now.getDate() - (5 - i + offset.value) * 7)
    return { year: getISOWeekYear(d), week: getISOWeek(d) }
  })

  return weeks.map(({ year, week }) => {
    const key = `${year}-W${String(week).padStart(2, '0')}`
    const dates = getWeekDates(year, week)

    const days: DayRow[] = dates.map((date) => {
      const log = logMap.value.get(date)
      const calories = log?.caloriesKcal ?? null
      const target = getTargetForDate(date)
      return {
        date,
        label: dayFmt.format(new Date(date + 'T00:00:00')),
        weightDisplay: log?.weightKg != null ? formatWeight(log.weightKg, props.unit) : '—',
        calories,
        target,
      }
    })

    const isEmpty = days.every((d) => d.calories == null && d.weightDisplay === '—')
    const isCurrent = year === currentISOWeekYear && week === currentISOWeek

    const calorieDays = days.filter((d) => d.calories != null)
    const avgCalories = calorieDays.length > 0
      ? calorieDays.reduce((sum, d) => sum + d.calories!, 0) / calorieDays.length
      : null

    const weightDays = days.filter((d) => d.weightDisplay !== '—')
    let avgWeightDisplay: string | null = null
    if (weightDays.length > 0) {
      const avgKg = weightDays.reduce((sum, d) => {
        const log = logMap.value.get(d.date)
        return sum + (log?.weightKg ?? 0)
      }, 0) / weightDays.length
      const value = props.unit === 'lbs' ? avgKg * 2.20462 : avgKg
      avgWeightDisplay = `${value.toFixed(2)} ${props.unit}`
    }

    const weekMidDate = dates[3] ?? dates[0]
    const weekTarget = getTargetForDate(weekMidDate)
    let status: Status = 'gray'
    if (avgCalories != null && weekTarget != null) {
      status = avgCalories <= weekTarget ? 'green' : 'red'
    }

    const firstDate = new Date(dates[0] + 'T00:00:00')
    const lastDate = new Date(dates[6] + 'T00:00:00')
    const startStr = monthDayFmt.format(firstDate)
    const endStr = firstDate.getMonth() === lastDate.getMonth()
      ? lastDate.getDate().toString()
      : monthDayFmt.format(lastDate)
    const dateRange = `${startStr}–${endStr}, ${lastDate.getFullYear()}`

    return {
      year,
      week,
      key,
      status,
      isEmpty,
      isCurrent,
      dateRange,
      avgCalories: avgCalories != null ? Math.round(avgCalories) : null,
      avgWeightDisplay,
      weekTarget,
      days,
    }
  })
})

const selectedWeek = computed(() => weekData.value[selectedIndex.value] ?? null)

function toggle(index: number) {
  selectedIndex.value = index
}

function jumpToCurrent() {
  offset.value = -1
  selectedIndex.value = 4
}

function chipClass(w: WeekItem, active: boolean): string {
  if (w.isEmpty) {
    return active
      ? 'bg-gray-100 text-gray-500 border-2 border-gray-400'
      : 'bg-gray-50 text-gray-400 border border-dashed border-gray-200'
  }

  const color = w.status === 'green'
    ? 'bg-emerald-600 hover:bg-emerald-700 text-white'
    : w.status === 'red'
    ? 'bg-rose-500 hover:bg-rose-600 text-white'
    : 'bg-slate-400 hover:bg-slate-500 text-white'

  return active ? `${color} ring-[3px] ring-offset-1 ring-gray-800` : color
}

function calorieClass(calories: number | null, target: number | null): string {
  if (calories == null || target == null) return 'text-gray-500'
  return calories > target ? 'text-red-500' : 'text-emerald-500'
}
</script>

<style scoped>
@reference "../../style.css";
.nav-btn {
  @apply text-xl text-gray-400 hover:text-gray-700 w-7 h-7 flex items-center justify-center rounded-full hover:bg-gray-100 transition-colors shrink-0;
}

.expand-enter-active,
.expand-leave-active {
  transition: opacity 0.15s ease, transform 0.15s ease;
}
.expand-enter-from,
.expand-leave-to {
  opacity: 0;
  transform: translateY(-4px);
}
</style>
