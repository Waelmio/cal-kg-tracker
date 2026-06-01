<template>
  <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/40" @mousedown.self="$emit('close')">
    <div class="bg-white rounded-xl shadow-xl w-full max-w-sm p-6 max-h-[90vh] overflow-y-auto">

      <!-- Header -->
      <div class="flex items-center justify-between mb-4">
        <div>
          <h2 class="text-lg font-semibold text-gray-800">{{ monthLabel }}</h2>
          <p class="text-xs text-gray-400">{{ loggedDays }} day{{ loggedDays !== 1 ? 's' : '' }} logged</p>
        </div>
        <button @click="$emit('close')" class="text-gray-400 hover:text-gray-600 text-xl leading-none cursor-pointer">✕</button>
      </div>

      <!-- Stat cards -->
      <div class="grid grid-cols-2 gap-2.5 mb-4">
        <div class="bg-gray-50 rounded-lg p-3">
          <p class="text-[10px] text-gray-400 uppercase font-medium tracking-wide">Avg Calories</p>
          <p class="text-xl font-bold text-gray-800 mt-0.5">
            {{ avgCalories != null ? avgCalories.toLocaleString() : '—' }}
          </p>
          <p class="text-xs text-gray-400">
            {{ monthTarget != null ? '/ ' + monthTarget.toLocaleString() + ' kcal' : 'no target' }}
          </p>
        </div>

        <div v-if="avgWeightKg != null" class="bg-gray-50 rounded-lg p-3">
          <p class="text-[10px] text-gray-400 uppercase font-medium tracking-wide">Avg Weight</p>
          <p class="text-xl font-bold text-gray-800 mt-0.5">{{ avgWeightDisplay }}</p>
          <p class="text-xs text-gray-400">{{ unit }}</p>
        </div>

        <div class="rounded-lg p-3" :class="cheatDayBgClass">
          <p class="text-[10px] uppercase font-medium tracking-wide" :class="cheatDaySubClass">Cheat Days</p>
          <p class="text-xl font-bold mt-0.5" :class="cheatDayTextClass">
            {{ cheatDays }}
          </p>
          <p class="text-xs" :class="cheatDaySubClass">
            {{ loggedDays > 0 ? Math.round(cheatDays / loggedDays * 100) + '% of logged days' : 'no data' }}
          </p>
        </div>

        <div v-if="estimatedMonthlyTdee != null" class="bg-gray-50 rounded-lg p-3">
          <p class="text-[10px] text-gray-400 uppercase font-medium tracking-wide">Est. Monthly TDEE</p>
          <p class="text-xl font-bold mt-0.5" :class="tdeeDiffClass">
            {{ estimatedMonthlyTdee.toLocaleString() }}
          </p>
          <p class="text-xs text-gray-400">
            {{ tdee != null ? 'setting: ' + tdee.toLocaleString() + ' kcal' : 'kcal/day' }}
          </p>
        </div>
      </div>

      <!-- Monthly totals -->
      <div v-if="loggedDays > 0" class="border border-gray-100 rounded-lg p-3 mb-4 space-y-2">
        <p class="text-[10px] text-gray-400 uppercase font-medium tracking-wide mb-1">Monthly Totals</p>
        <div v-if="totalDeficitVsTarget != null" class="flex justify-between text-sm">
          <span class="text-gray-500">Deficit vs target</span>
          <span class="font-semibold" :class="signClass(totalDeficitVsTarget)">
            {{ totalDeficitVsTarget >= 0 ? '−' : '+' }}{{ Math.abs(totalDeficitVsTarget).toLocaleString() }} kcal
          </span>
        </div>
        <div v-if="totalDeficitVsTdee != null" class="flex justify-between text-sm">
          <span class="text-gray-500">Deficit vs TDEE</span>
          <span class="font-semibold" :class="signClass(totalDeficitVsTdee)">
            {{ totalDeficitVsTdee >= 0 ? '−' : '+' }}{{ Math.abs(totalDeficitVsTdee).toLocaleString() }} kcal
          </span>
        </div>
        <div v-if="estimatedFatKg != null" class="flex justify-between text-sm pt-1 border-t border-gray-100">
          <span class="text-gray-500">Est. fat {{ estimatedFatKg >= 0 ? 'loss' : 'gain' }}</span>
          <span class="font-semibold" :class="estimatedFatKg >= 0 ? 'text-emerald-500' : 'text-rose-500'">
            ≈ {{ estimatedFatDisplay }} {{ unit }}
          </span>
        </div>
        <div class="pt-1 border-t border-gray-100 space-y-2">
          <div class="flex justify-between text-sm">
            <span class="text-gray-500">Start weight</span>
            <span class="font-semibold text-gray-700">
              {{ startWeightKg != null ? displayWeight(startWeightKg, unit).toFixed(1) + ' ' + unit : '?' }}
            </span>
          </div>
          <div class="flex justify-between text-sm">
            <span class="text-gray-500">End weight <span class="text-gray-400 font-normal">(7d avg)</span></span>
            <span class="font-semibold text-gray-700">
              {{ endWeightKg != null ? displayWeight(endWeightKg, unit).toFixed(1) + ' ' + unit : '?' }}
            </span>
          </div>
          <div class="flex justify-between text-sm">
            <span class="text-gray-500">Real {{ weightChangeKg != null && weightChangeKg > 0 ? 'gain' : 'loss' }}</span>
            <span class="font-semibold" :class="weightChangeKg != null ? (weightChangeKg <= 0 ? 'text-emerald-500' : 'text-rose-500') : 'text-gray-400'">
              {{ weightChangeKg != null ? (weightChangeKg <= 0 ? '−' : '+') + displayWeight(Math.abs(weightChangeKg), unit).toFixed(2) + ' ' + unit : '?' }}
            </span>
          </div>
        </div>
      </div>

      <!-- Week breakdown -->
      <div>
        <p class="text-[10px] text-gray-400 uppercase font-medium tracking-wide mb-2">Week Breakdown</p>
        <div
          v-for="week in weeks"
          :key="week.weekNum"
          class="flex items-center gap-1 py-1.5 text-sm border-b border-gray-50 last:border-0"
        >
          <span class="text-gray-500 w-10 shrink-0 font-medium">W{{ week.weekNum }}</span>
          <span class="flex-1 text-right font-medium" :class="weekCalClass(week)">
            {{ week.avgCalories != null ? week.avgCalories.toLocaleString() : '—' }}
          </span>
          <span class="text-gray-400 text-xs w-16 text-right shrink-0">
            {{ week.avgWeightKg != null ? weekWeightDisplay(week.avgWeightKg) + ' ' + unit : '' }}
          </span>
          <span class="text-xs font-medium w-20 text-right shrink-0" :class="signClass(weekDeficit(week))">
            {{ weekDeficitLabel(week) }}
          </span>
        </div>
      </div>

    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { WeightUnit } from '../../types'
import { displayWeight } from '../../utils/units'

export interface MonthWeekSummary {
  weekNum: number
  avgCalories: number | null
  avgWeightKg: number | null
  loggedDays: number
  deficitVsTarget: number | null
  deficitVsTdee: number | null
}

const props = defineProps<{
  year: number
  month: number
  weeks: MonthWeekSummary[]
  avgCalories: number | null
  monthTarget: number | null
  avgWeightKg: number | null
  loggedDays: number
  totalDeficitVsTarget: number | null
  totalDeficitVsTdee: number | null
  cheatDays: number
  estimatedMonthlyTdee: number | null
  startWeightKg: number | null
  endWeightKg: number | null
  unit: WeightUnit
  tdee: number | null
}>()

defineEmits<{ close: [] }>()

const monthLabel = computed(() =>
  new Date(Date.UTC(props.year, props.month, 1))
    .toLocaleDateString('en', { month: 'long', year: 'numeric', timeZone: 'UTC' })
)

const estimatedFatKg = computed(() => {
  const deficit = props.totalDeficitVsTdee ?? props.totalDeficitVsTarget
  if (deficit == null) return null
  return deficit / 7700
})

const estimatedFatDisplay = computed(() => {
  if (estimatedFatKg.value == null) return '—'
  return displayWeight(Math.abs(estimatedFatKg.value), props.unit).toFixed(2)
})

const avgWeightDisplay = computed(() => {
  if (props.avgWeightKg == null) return '—'
  return displayWeight(props.avgWeightKg, props.unit).toFixed(1)
})

const cheatDayBgClass = computed(() => {
  if (props.cheatDays > 10) return 'bg-rose-50 border border-rose-200'
  if (props.cheatDays > 5)  return 'bg-orange-50 border border-orange-200'
  return 'bg-gray-50'
})

const cheatDayTextClass = computed(() => {
  if (props.cheatDays > 10) return 'text-rose-600'
  if (props.cheatDays > 5)  return 'text-orange-600'
  return 'text-gray-800'
})

const cheatDaySubClass = computed(() => {
  if (props.cheatDays > 10) return 'text-rose-400'
  if (props.cheatDays > 5)  return 'text-orange-400'
  return 'text-gray-400'
})

const weightChangeKg = computed(() => {
  if (props.startWeightKg == null || props.endWeightKg == null) return null
  return props.endWeightKg - props.startWeightKg
})

const tdeeDiffClass = computed(() => {
  if (props.estimatedMonthlyTdee == null || props.tdee == null) return 'text-gray-800'
  const diff = Math.abs(props.estimatedMonthlyTdee - props.tdee) / props.tdee
  return diff > 0.15 ? 'text-orange-500' : 'text-gray-800'
})

function signClass(n: number | null | undefined): string {
  if (n == null) return 'text-gray-400'
  return n >= 0 ? 'text-emerald-500' : 'text-rose-500'
}

function weekDeficit(week: MonthWeekSummary): number | null {
  return week.deficitVsTdee ?? week.deficitVsTarget ?? null
}

function weekCalClass(week: MonthWeekSummary): string {
  if (week.avgCalories == null) return 'text-gray-300'
  const deficit = weekDeficit(week)
  if (deficit == null) return 'text-gray-700'
  return deficit >= 0 ? 'text-emerald-600' : 'text-rose-500'
}

function weekDeficitLabel(week: MonthWeekSummary): string {
  const d = weekDeficit(week)
  if (d == null) return ''
  return `${d >= 0 ? '−' : '+'}${Math.abs(d).toLocaleString()}`
}

function weekWeightDisplay(avgWeightKg: number): string {
  return displayWeight(avgWeightKg, props.unit).toFixed(1)
}
</script>
