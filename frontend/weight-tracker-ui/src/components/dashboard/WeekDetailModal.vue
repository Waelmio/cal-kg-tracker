<template>
  <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/40" @mousedown.self="$emit('close')">
    <div class="bg-white rounded-xl shadow-xl w-full max-w-sm p-6 max-h-[90vh] overflow-y-auto">

      <!-- Header -->
      <div class="flex items-center justify-between mb-4">
        <div>
          <h2 class="text-lg font-semibold text-gray-800">Week {{ weekNum }}</h2>
          <p class="text-xs text-gray-400">{{ dateRange }}</p>
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
            {{ weekTarget != null ? '/ ' + weekTarget.toLocaleString() + ' kcal' : 'no target' }}
          </p>
        </div>

        <div v-if="avgWeightKg != null" class="bg-gray-50 rounded-lg p-3">
          <p class="text-[10px] text-gray-400 uppercase font-medium tracking-wide">Avg Weight</p>
          <p class="text-xl font-bold text-gray-800 mt-0.5">{{ avgWeightDisplay }}</p>
          <p class="text-xs text-gray-400">{{ unit }}</p>
        </div>
      </div>

      <!-- Weekly totals -->
      <div v-if="loggedDays > 0" class="border border-gray-100 rounded-lg p-3 mb-4 space-y-2">
        <p class="text-[10px] text-gray-400 uppercase font-medium tracking-wide mb-1">
          Weekly Totals · {{ loggedDays }} day{{ loggedDays !== 1 ? 's' : '' }} logged
        </p>
        <div v-if="weeklyDeficitVsTarget != null" class="flex justify-between text-sm">
          <span class="text-gray-500">Deficit vs target</span>
          <span class="font-semibold" :class="signClass(weeklyDeficitVsTarget)">
            {{ weeklyDeficitVsTarget >= 0 ? '−' : '+' }}{{ Math.abs(weeklyDeficitVsTarget).toLocaleString() }} kcal
          </span>
        </div>
        <div v-if="weeklyDeficitVsTdee != null" class="flex justify-between text-sm">
          <span class="text-gray-500">Deficit vs TDEE</span>
          <span class="font-semibold" :class="signClass(weeklyDeficitVsTdee)">
            {{ weeklyDeficitVsTdee >= 0 ? '−' : '+' }}{{ Math.abs(weeklyDeficitVsTdee).toLocaleString() }} kcal
          </span>
        </div>
        <div v-if="estimatedFatKg != null" class="flex justify-between text-sm pt-1 border-t border-gray-100">
          <span class="text-gray-500">Est. fat {{ estimatedFatKg >= 0 ? 'loss' : 'gain' }}</span>
          <span class="font-semibold" :class="estimatedFatKg >= 0 ? 'text-emerald-500' : 'text-rose-500'">
            ≈ {{ estimatedFatDisplay }} {{ unit }}
          </span>
        </div>
      </div>

      <!-- Day breakdown -->
      <div>
        <p class="text-[10px] text-gray-400 uppercase font-medium tracking-wide mb-2">Day Breakdown</p>
        <button
          v-for="day in days"
          :key="day.date"
          @click="$emit('edit-calories', day.date)"
          class="w-full flex items-center gap-1 py-1.5 text-sm border-b border-gray-50 last:border-0 hover:bg-gray-50 -mx-1 px-1 rounded transition-colors cursor-pointer"
        >
          <span class="text-gray-500 w-16 shrink-0 truncate text-left">{{ day.shortLabel }}</span>
          <span class="flex-1 text-right font-medium" :class="dayCalClass(day)">
            {{ day.calories != null ? day.calories.toLocaleString() : '—' }}
          </span>
          <span class="text-gray-400 text-xs w-4 text-center shrink-0">
            {{ day.calories != null && day.target != null ? '/' : '' }}
          </span>
          <span class="text-gray-400 text-xs w-12 text-right shrink-0">
            {{ day.target != null ? day.target.toLocaleString() : '' }}
          </span>
          <span class="text-xs font-medium w-14 text-right shrink-0" :class="dayDeltaClass(day)">
            {{ dayDeltaLabel(day) }}
          </span>
        </button>
      </div>

    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { WeightUnit } from '../../types'
import { displayWeight } from '../../utils/units'

interface ModalDay {
  date: string
  shortLabel: string
  calories: number | null
  target: number | null
}

const props = defineProps<{
  weekNum: number
  year: number
  days: ModalDay[]
  avgCalories: number | null
  weekTarget: number | null
  avgWeightKg: number | null
  unit: WeightUnit
  tdee: number | null
}>()

defineEmits<{ close: []; 'edit-calories': [date: string] }>()

// ── Date range label ──────────────────────────────────────────────────────────

const dateRange = computed(() => {
  if (props.days.length === 0) return ''
  const fmt = new Intl.DateTimeFormat('en', { day: 'numeric', month: 'short', timeZone: 'UTC' })
  const fmtYear = new Intl.DateTimeFormat('en', { day: 'numeric', month: 'short', year: 'numeric', timeZone: 'UTC' })
  const start = new Date(props.days[0].date + 'T00:00:00Z')
  const end   = new Date(props.days[props.days.length - 1].date + 'T00:00:00Z')
  return `${fmt.format(start)} – ${fmtYear.format(end)}`
})

// ── Derived stats ─────────────────────────────────────────────────────────────

const loggedDays = computed(() => props.days.filter(d => d.calories != null).length)

const weeklyDeficitVsTarget = computed(() => {
  if (props.weekTarget == null) return null
  let total = 0
  for (const d of props.days) {
    if (d.calories != null) total += (d.target ?? props.weekTarget) - d.calories
  }
  return loggedDays.value > 0 ? Math.round(total) : null
})

const weeklyDeficitVsTdee = computed(() => {
  if (props.tdee == null) return null
  let total = 0
  for (const d of props.days) {
    if (d.calories != null) total += props.tdee - d.calories
  }
  return loggedDays.value > 0 ? Math.round(total) : null
})

// Use TDEE deficit if available, otherwise target deficit
const estimatedFatKg = computed(() => {
  const deficit = weeklyDeficitVsTdee.value ?? weeklyDeficitVsTarget.value
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

// ── Formatting helpers ────────────────────────────────────────────────────────

// positive deficit = green (good for weight loss)
function signClass(n: number | null): string {
  if (n == null) return 'text-gray-400'
  return n >= 0 ? 'text-emerald-500' : 'text-rose-500'
}

function dayCalClass(day: ModalDay): string {
  if (day.calories == null) return 'text-gray-300'
  if (day.calories != null && day.target != null) {
    return day.calories <= day.target ? 'text-emerald-600' : 'text-rose-500'
  }
  return 'text-gray-700'
}

function dayDeltaClass(day: ModalDay): string {
  if (day.calories == null || day.target == null) return 'text-gray-300'
  return day.calories <= day.target ? 'text-emerald-500' : 'text-rose-400'
}

function dayDeltaLabel(day: ModalDay): string {
  if (day.calories == null || day.target == null) return ''
  const diff = day.target - day.calories
  return `${diff >= 0 ? '−' : '+'}${Math.abs(diff).toLocaleString()}`
}
</script>
