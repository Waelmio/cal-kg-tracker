<template>
  <div class="bg-white rounded-xl border border-gray-200 p-4">
    <!-- Header -->
    <div class="flex items-center justify-between mb-3">
      <p class="text-xs font-medium text-gray-400 uppercase tracking-wide">Calendar</p>
      <button
        :class="isOnToday ? 'invisible' : ''"
        @click="jumpToToday"
        class="text-xs font-medium text-blue-500 bg-blue-50 hover:bg-blue-100 px-2 py-0.5 rounded-full transition-colors"
      >↩ Today</button>
    </div>

    <!-- Month chip bar with its own ‹ › navigation -->
    <div class="flex items-center gap-1 mb-3">
      <button @click="chipBarPrev" class="nav-btn flex-shrink-0">‹</button>
      <div class="flex-1 overflow-hidden">
        <Transition name="fade" mode="out-in">
          <div :key="`chip-${selectedYear}-${selectedMonth}`" class="flex gap-1.5 py-1 px-0.5">
            <button
              v-for="m in recentMonthsData"
              :key="m.key"
              @click="selectMonth(m.year, m.month)"
              :class="monthChipClass(m)"
              class="flex-shrink-0 px-2.5 py-1 rounded-lg text-xs font-semibold transition-colors cursor-pointer"
            >{{ m.label }}</button>
          </div>
        </Transition>
      </div>
      <button @click="chipBarNext" class="nav-btn flex-shrink-0">›</button>
    </div>

    <!-- Month navigation -->
    <div class="flex items-center justify-between mb-2">
      <button @click="prevMonth" class="nav-btn">‹</button>
      <span class="text-sm font-semibold text-gray-700">{{ currentMonthLabel }}</span>
      <button @click="nextMonth" class="nav-btn">›</button>
    </div>

    <!-- Calendar grid + week detail slide on month change -->
    <Transition :name="'slide-' + navDirection" mode="out-in">
      <div :key="`${selectedYear}-${selectedMonth}`">

        <!-- Calendar grid -->
        <div class="cal-grid">
          <div />
          <div v-for="d in DAY_LABELS" :key="d" class="text-center text-[10px] font-medium text-gray-400 pb-1">{{ d }}</div>

          <template v-for="week in calendarWeeks" :key="week.key">
            <div :class="weekChipClass(week)" class="week-chip cursor-pointer" @dblclick.stop="openWeekModal(week)" title="Double-click for week details">
              <span class="text-xs">{{ week.week }}</span>
              <span v-if="week.avgWeightDisplay" class="text-[11px] opacity-80 leading-none mt-0.5">{{ week.avgWeightDisplay }}</span>
            </div>

            <button
              v-for="day in week.days"
              :key="day.date"
              @click="selectDay(day)"
              @dblclick.stop="emit('edit-calories', day.date)"
              :class="dayCellClass(day)"
              class="day-cell"
            >
              <div v-if="day.isCheatDay" class="absolute top-1 right-1 w-1.5 h-1.5 rounded-full bg-orange-400" />
              <span class="text-sm font-semibold leading-none">{{ day.dayNum }}</span>
              <span v-if="day.calories != null" class="text-xs leading-none opacity-90 mt-1">{{ day.calories }}</span>
            </button>
          </template>
        </div>

        <!-- Legend -->
        <div class="flex items-center gap-3 mt-3 text-[10px] text-gray-400">
          <span class="flex items-center gap-1"><span class="w-2.5 h-2.5 rounded-sm bg-emerald-400 inline-block" />Under target</span>
          <span class="flex items-center gap-1"><span class="w-2.5 h-2.5 rounded-sm bg-amber-400 inline-block" />Over target, avg ok</span>
          <span class="flex items-center gap-1"><span class="w-2.5 h-2.5 rounded-sm bg-rose-400 inline-block" />Over target</span>
        </div>

        <!-- Week detail -->
        <div v-if="selectedWeek" class="mt-3 border-t border-gray-100 pt-3">
          <div class="mb-2">
            <span class="text-sm font-semibold text-gray-600">Week {{ selectedWeek.week }}<template v-if="selectedWeek.avgWeightDisplay"> · {{ selectedWeek.avgWeightDisplay }} {{ unit }}</template></span>
          </div>
          <div
            v-for="day in selectedWeek.days"
            :key="day.date"
            class="flex items-center justify-between py-1.5 text-sm sm:text-base border-b border-gray-50 last:border-0 gap-1"
            :class="day.date === selectedDay ? 'bg-gray-50 -mx-1 px-1 rounded' : ''"
          >
            <span class="text-gray-500 w-24 sm:w-32 shrink-0 truncate">{{ day.shortLabel }}</span>
            <button
              @click="emit('edit-weight', day.date)"
              class="text-gray-700 w-16 sm:w-20 text-right hover:text-blue-500 hover:underline transition-colors shrink-0"
            >{{ day.weightDisplay }}</button>
            <button
              @click="emit('edit-calories', day.date)"
              class="flex-1 text-right hover:underline transition-colors"
              :class="calorieClass(day.calories, day.target)"
            >{{ day.calories != null ? day.calories.toLocaleString() + ' kcal' : '—' }}</button>
            <span class="w-14 sm:w-16 text-right font-light text-gray-400 shrink-0">
              {{ day.target != null ? '/ ' + day.target : '' }}
            </span>
          </div>
        </div>

      </div>
    </Transition>
  </div>

  <WeekDetailModal
    v-if="weekModalData"
    :week-num="weekModalData.week"
    :year="weekModalData.year"
    :days="weekModalData.days.map(d => ({ date: d.date, shortLabel: d.shortLabel, calories: d.calories, target: d.target }))"
    :avg-calories="weekModalData.avgCalories"
    :week-target="weekModalData.weekTarget"
    :avg-weight-kg="weekModalData.avgWeightKg"
    :unit="unit"
    :tdee="tdee ?? null"
    @close="weekModalData = null"
    @edit-calories="date => emit('edit-calories', date)"
  />
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import type { DailyLog, CalorieGoal, Goal, WeightUnit } from '../../types'
import { formatWeight } from '../../utils/units'
import { getISOWeek, getISOWeekYear, getWeekDates } from '../../utils/weeks'
import WeekDetailModal from './WeekDetailModal.vue'

const emit = defineEmits<{
  'need-from': [date: string]
  'edit-weight': [date: string]
  'edit-calories': [date: string]
}>()

const props = defineProps<{
  logs: DailyLog[]
  calorieGoals: CalorieGoal[]
  unit: WeightUnit
  goal?: Goal | null
  tdee?: number | null
}>()

const DAY_LABELS = ['Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa', 'Su']

const today = new Date(new Date().toLocaleDateString('en-CA') + 'T00:00:00Z')
const todayStr = today.toISOString().slice(0, 10)

const selectedYear = ref(today.getUTCFullYear())
const selectedMonth = ref(today.getUTCMonth()) // 0-indexed

const isCurrentMonth = computed(() =>
  selectedYear.value === today.getUTCFullYear() &&
  selectedMonth.value === today.getUTCMonth()
)

const isOnToday = computed(() =>
  isCurrentMonth.value && selectedDay.value === todayStr
)

const currentMonthLabel = computed(() =>
  new Date(Date.UTC(selectedYear.value, selectedMonth.value, 1))
    .toLocaleDateString('en', { month: 'long', year: 'numeric', timeZone: 'UTC' })
)

const selectedDay    = ref<string>(todayStr)
const navDirection   = ref<'forward' | 'backward'>('forward')

// Chip bar center — updated directly by all navigation functions
const chipYear  = ref(today.getUTCFullYear())
const chipMonth = ref(today.getUTCMonth())

function setMonth(year: number, month: number) {
  navDirection.value = (year * 12 + month) >= (selectedYear.value * 12 + selectedMonth.value) ? 'forward' : 'backward'
  selectedYear.value = year
  selectedMonth.value = month
  chipYear.value = year
  chipMonth.value = month
  const currentDay = parseInt(selectedDay.value.slice(8, 10), 10)
  const daysInMonth = new Date(Date.UTC(year, month + 1, 0)).getUTCDate()
  const day = Math.min(currentDay, daysInMonth)
  selectedDay.value = `${year}-${String(month + 1).padStart(2, '0')}-${String(day).padStart(2, '0')}`
  let ey = year, em = month - 2
  while (em < 0) { em += 12; ey-- }
  emitNeedFrom(`${ey}-${String(em + 1).padStart(2, '0')}-01`)
}

function chipBarPrev() {
  let m = chipMonth.value - 1, y = chipYear.value
  if (m < 0) { m = 11; y-- }
  chipYear.value = y; chipMonth.value = m
  setMonth(y, m)
}

function chipBarNext() {
  let m = chipMonth.value + 1, y = chipYear.value
  if (m > 11) { m = 0; y++ }
  chipYear.value = y; chipMonth.value = m
  setMonth(y, m)
}

function prevMonth() {
  let m = selectedMonth.value - 1, y = selectedYear.value
  if (m < 0) { m = 11; y-- }
  setMonth(y, m)
}

function nextMonth() {
  let m = selectedMonth.value + 1, y = selectedYear.value
  if (m > 11) { m = 0; y++ }
  setMonth(y, m)
}

function selectMonth(year: number, month: number) {
  setMonth(year, month)
}

function jumpToToday() {
  setMonth(today.getUTCFullYear(), today.getUTCMonth())
  selectedDay.value = todayStr
}

function emitNeedFrom(date: string) {
  const oldest = props.logs.length > 0 ? props.logs[props.logs.length - 1].date : null
  if (oldest == null || date < oldest) emit('need-from', date)
}

// ── Data helpers ────────────────────────────────────────────────────────────

const logMap = computed(() => {
  const m = new Map<string, DailyLog>()
  for (const log of props.logs) m.set(log.date, log)
  return m
})

function getTargetForDate(date: string): number | null {
  const d = new Date(date + 'T00:00:00Z')
  const goal = props.calorieGoals.find((g) => {
    const gDay = new Date(g.createdAt)
    gDay.setUTCHours(0, 0, 0, 0)
    return gDay <= d
  })
  return goal?.targetCalories ?? null
}

// ── Status types & computation ───────────────────────────────────────────────

type MonthStatus = 'green' | 'red' | 'gray'
type WeekStatus  = 'green' | 'orange' | 'red' | 'gray'
type DayStatus   = 'green' | 'orange' | 'red' | 'gray'

function computeMonthStatus(year: number, month: number, goalFiltered = false): MonthStatus {
  const daysInMonth = new Date(Date.UTC(year, month + 1, 0)).getUTCDate()
  const goal = props.goal
  let totalCalories = 0, totalTarget = 0, count = 0
  let goalCalories = 0, goalTarget = 0, goalCount = 0
  for (let d = 1; d <= daysInMonth; d++) {
    const date = `${year}-${String(month + 1).padStart(2, '0')}-${String(d).padStart(2, '0')}`
    const log = logMap.value.get(date)
    if (log?.caloriesKcal != null) {
      const target = log.calorieTarget ?? getTargetForDate(date)
      if (target != null) {
        totalCalories += log.caloriesKcal; totalTarget += target; count++
        if (goal && date >= goal.startDate && date <= goal.targetDate) {
          goalCalories += log.caloriesKcal; goalTarget += target; goalCount++
        }
      }
    }
  }
  const useGoal = goalFiltered && goalCount > 0
  const [cal, tgt, cnt] = useGoal ? [goalCalories, goalTarget, goalCount] : [totalCalories, totalTarget, count]
  if (cnt === 0) return 'gray'
  return cal <= tgt ? 'green' : 'red'
}

// ── Recent months chips ──────────────────────────────────────────────────────

interface MonthChip {
  year: number; month: number; label: string; key: string
  status: MonthStatus; isSelected: boolean
}

// Show a window of 5 months centred on chipYear/chipMonth
const recentMonthsData = computed<MonthChip[]>(() => {
  const result: MonthChip[] = []
  for (let i = -2; i <= 2; i++) {
    let year  = chipYear.value
    let month = chipMonth.value + i
    while (month < 0)  { month += 12; year-- }
    while (month > 11) { month -= 12; year++ }
    result.push({
      year, month,
      label: new Date(Date.UTC(year, month, 1)).toLocaleDateString('en', { month: 'short', timeZone: 'UTC' }),
      key: `${year}-${month}`,
      status: computeMonthStatus(year, month, true),
      isSelected: year === selectedYear.value && month === selectedMonth.value,
    })
  }
  return result
})

// ── Calendar weeks ───────────────────────────────────────────────────────────

interface DayInfo {
  date: string; dayNum: number; isOtherMonth: boolean; isToday: boolean
  calories: number | null; target: number | null
  hasWeight: boolean; weightDisplay: string; weightShort: string | null
  shortLabel: string; fullLabel: string; status: DayStatus; isCheatDay: boolean
}


interface WeekData {
  year: number; week: number; key: string; days: DayInfo[]
  avgCalories: number | null; weekTarget: number | null; status: WeekStatus
  avgWeightDisplay: string | null; avgWeightKg: number | null
}

const fullDayFmt  = new Intl.DateTimeFormat('en', { weekday: 'long', day: 'numeric', month: 'long', timeZone: 'UTC' })
const shortDayFmt = new Intl.DateTimeFormat('en', { weekday: 'short', day: 'numeric', timeZone: 'UTC' })


const calendarWeeks = computed<WeekData[]>(() => {
  const year = selectedYear.value
  const month = selectedMonth.value
  const monthStatus = computeMonthStatus(year, month)

  const firstDay = new Date(Date.UTC(year, month, 1))

  // Find the Monday of the first partial week
  const firstDow = firstDay.getUTCDay() || 7
  const cursor = new Date(firstDay)
  cursor.setUTCDate(firstDay.getUTCDate() - (firstDow - 1))

  const weeks: WeekData[] = []

  // Always render exactly 6 rows so the component height never changes
  for (let _row = 0; _row < 6; _row++) {
    const wYear = getISOWeekYear(cursor)
    const wNum  = getISOWeek(cursor)
    const key   = `${wYear}-W${String(wNum).padStart(2, '0')}`
    const dates = getWeekDates(wYear, wNum)

    // Week averages: track all days + goal-range days separately.
    // Goal-range subset is used for the chip colour; all days drive the orange/red logic (via monthStatus).
    let calSum = 0, calCount = 0, wgtSum = 0, wgtCount = 0
    let goalCalSum = 0, goalCalCount = 0
    let tgtSum = 0, tgtCount = 0
    for (const date of dates) {
      const log = logMap.value.get(date)
      if (log?.caloriesKcal != null) {
        calSum += log.caloriesKcal; calCount++
        const g = props.goal
        if (g && date >= g.startDate && date <= g.targetDate) { goalCalSum += log.caloriesKcal; goalCalCount++ }
      }
      if (log?.weightKg != null) { wgtSum += log.weightKg; wgtCount++ }
      const t = log?.calorieTarget ?? getTargetForDate(date)
      if (t != null) { tgtSum += t; tgtCount++ }
    }
    const chipCalSum   = goalCalCount > 0 ? goalCalSum   : calSum
    const chipCalCount = goalCalCount > 0 ? goalCalCount : calCount
    const avgCalories = chipCalCount > 0 ? chipCalSum / chipCalCount : null
    const weekTarget  = tgtCount > 0 ? Math.round(tgtSum / tgtCount) : null
    const avgWeightDisplay = wgtCount > 0
      ? (props.unit === 'lbs' ? wgtSum / wgtCount * 2.20462 : wgtSum / wgtCount).toFixed(1)
      : null

    let wStatus: WeekStatus = 'gray'
    if (avgCalories != null && weekTarget != null) {
      if (avgCalories <= weekTarget) wStatus = 'green'
      else wStatus = monthStatus === 'green' ? 'orange' : 'red'
    }

    const days: DayInfo[] = dates.map(date => {
      const d = new Date(date + 'T00:00:00Z')
      const isOtherMonth = d.getUTCMonth() !== month || d.getUTCFullYear() !== year
      const log      = logMap.value.get(date)
      const calories = log?.caloriesKcal ?? null
      const target   = log?.calorieTarget ?? getTargetForDate(date)

      let dayStatus: DayStatus = 'gray'
      if (calories != null && target != null) {
        if (calories <= target) dayStatus = 'green'
        else dayStatus = (wStatus === 'green' || wStatus === 'orange') ? 'orange' : 'red'
      }

      return {
        date, dayNum: d.getUTCDate(), isOtherMonth, isToday: date === todayStr,
        calories, target,
        hasWeight: log?.weightKg != null,
        weightDisplay: log?.weightKg != null ? formatWeight(log.weightKg, props.unit) : '—',
        weightShort: log?.weightKg != null
          ? (props.unit === 'lbs' ? (log.weightKg * 2.20462) : log.weightKg).toFixed(1)
          : null,
        shortLabel: shortDayFmt.format(d),
        fullLabel: fullDayFmt.format(d),
        status: dayStatus,
        isCheatDay: log?.isCheatDay ?? false,
      }
    })

    weeks.push({ year: wYear, week: wNum, key, days, avgCalories: avgCalories != null ? Math.round(avgCalories) : null, weekTarget, status: wStatus, avgWeightDisplay, avgWeightKg: wgtCount > 0 ? wgtSum / wgtCount : null })
    cursor.setUTCDate(cursor.getUTCDate() + 7)
  } // end for _row

  return weeks
})

// ── Week detail modal ─────────────────────────────────────────────────────────

const weekModalData = ref<WeekData | null>(null)

function openWeekModal(week: WeekData) {
  weekModalData.value = week
}

// ── Day / week selection ─────────────────────────────────────────────────────

function selectDay(day: DayInfo) {
  const d = new Date(day.date + 'T00:00:00Z')
  const y = d.getUTCFullYear(), m = d.getUTCMonth()
  if (y !== selectedYear.value || m !== selectedMonth.value) {
    setMonth(y, m)
  }
  selectedDay.value = day.date
}

const selectedWeek = computed(() => {
  if (!selectedDay.value) return null
  return calendarWeeks.value.find(w => w.days.some(d => d.date === selectedDay.value)) ?? null
})

// ── CSS class helpers ────────────────────────────────────────────────────────

function isOutsideGoal(date: string): boolean {
  if (!props.goal) return false
  return date < props.goal.startDate || date > props.goal.targetDate
}

function isMonthOutsideGoal(year: number, month: number): boolean {
  if (!props.goal) return false
  const firstDay = `${year}-${String(month + 1).padStart(2, '0')}-01`
  const lastDay  = `${year}-${String(month + 1).padStart(2, '0')}-${new Date(Date.UTC(year, month + 1, 0)).getUTCDate()}`
  return lastDay < props.goal.startDate || firstDay > props.goal.targetDate
}

function isWeekOutsideGoal(w: WeekData): boolean {
  if (!props.goal) return false
  return w.days[6].date < props.goal.startDate || w.days[0].date > props.goal.targetDate
}

function monthChipClass(m: MonthChip): string {
  const sel   = m.isSelected ? 'ring-2 ring-offset-1 ring-gray-700 ' : ''
  const faded = isMonthOutsideGoal(m.year, m.month)
  if (m.status === 'green') return sel + (faded ? 'bg-emerald-200 text-emerald-400 hover:bg-emerald-300' : 'bg-emerald-500 text-white hover:bg-emerald-600')
  if (m.status === 'red')   return sel + (faded ? 'bg-rose-200 text-rose-400 hover:bg-rose-300'         : 'bg-rose-500 text-white hover:bg-rose-600')
  return sel + 'bg-gray-50 text-gray-400 border border-dashed border-gray-300 hover:bg-gray-100'
}

function weekChipClass(w: WeekData): string {
  const faded = isWeekOutsideGoal(w)
  if (w.status === 'green')  return faded ? 'bg-emerald-200 text-emerald-400' : 'bg-emerald-500 text-white'
  if (w.status === 'orange') return faded ? 'bg-amber-200 text-amber-400'     : 'bg-amber-400 text-white'
  if (w.status === 'red')    return faded ? 'bg-rose-200 text-rose-400'       : 'bg-rose-500 text-white'
  return faded ? 'bg-gray-50 text-gray-300' : 'bg-gray-100 text-gray-400'
}

function dayCellClass(day: DayInfo): string {
  const isSelected = selectedDay.value === day.date
  const ring       = isSelected ? 'ring-2 ring-offset-1 ring-gray-700 relative z-10 ' : ''
  const todayRing  = day.isToday && !isSelected ? 'ring-2 ring-blue-400 ' : ''

  if (day.isOtherMonth) {
    if (day.status === 'green')  return ring + 'bg-emerald-50 text-emerald-300 hover:bg-emerald-100'
    if (day.status === 'orange') return ring + 'bg-amber-50 text-amber-400 hover:bg-amber-100'
    if (day.status === 'red')    return ring + 'bg-rose-50 text-rose-300 hover:bg-rose-100'
    return ring + 'bg-transparent text-gray-300 hover:bg-gray-100'
  }

  const faded = isOutsideGoal(day.date)
  if (day.status === 'green')  return ring + todayRing + (faded ? 'bg-emerald-50 text-emerald-300 hover:bg-emerald-100' : 'bg-emerald-100 text-emerald-800 hover:bg-emerald-200')
  if (day.status === 'orange') return ring + todayRing + (faded ? 'bg-amber-50 text-amber-300 hover:bg-amber-100'       : 'bg-amber-100 text-amber-800 hover:bg-amber-200')
  if (day.status === 'red')    return ring + todayRing + (faded ? 'bg-rose-50 text-rose-300 hover:bg-rose-100'           : 'bg-rose-100 text-rose-800 hover:bg-rose-200')
  if (faded) return ring + todayRing + 'bg-gray-50 text-gray-400 hover:bg-gray-100'
  return ring + todayRing + (day.isToday ? 'bg-blue-50 ' : 'bg-gray-50 ') + 'text-gray-600 hover:bg-gray-100'
}

function calorieClass(calories: number | null, target: number | null): string {
  if (calories == null || target == null) return 'text-gray-500'
  return calories > target ? 'text-rose-500' : 'text-emerald-500'
}
</script>

<style scoped>
@reference "../../style.css";

.nav-btn {
  @apply text-xl text-gray-400 hover:text-gray-700 w-7 h-7 flex items-center justify-center rounded-full hover:bg-gray-100 transition-colors shrink-0 cursor-pointer;
}

.cal-grid {
  display: grid;
  grid-template-columns: 2.75rem repeat(7, 1fr);
  gap: 3px;
}

.week-chip {
  @apply text-[10px] font-bold rounded-md flex flex-col items-center justify-center py-2 min-h-[4rem] select-none;
}

.day-cell {
  @apply relative flex flex-col items-center justify-center rounded-lg transition-all cursor-pointer select-none py-2 min-h-[4rem];
}

.no-scrollbar::-webkit-scrollbar { display: none; }
.no-scrollbar { -ms-overflow-style: none; scrollbar-width: none; }

.fade-enter-active, .fade-leave-active { transition: opacity 0.15s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }

.slide-forward-enter-active,
.slide-forward-leave-active,
.slide-backward-enter-active,
.slide-backward-leave-active {
  transition: transform 0.2s ease, opacity 0.2s ease;
}
.slide-forward-enter-from  { transform: translateX(28px);  opacity: 0; }
.slide-forward-leave-to    { transform: translateX(-28px); opacity: 0; }
.slide-backward-enter-from { transform: translateX(-28px); opacity: 0; }
.slide-backward-leave-to   { transform: translateX(28px);  opacity: 0; }
</style>
