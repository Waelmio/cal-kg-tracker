<template>
  <div class="bg-white rounded-xl border border-gray-200 p-4">
    <div class="flex items-center justify-between mb-3">
      <p class="text-xs font-medium text-gray-400 uppercase tracking-wide">Weight History</p>
      <div class="flex items-center gap-2">
        <div class="flex rounded-lg border border-gray-200 overflow-hidden text-xs">
          <button
            v-for="r in ranges" :key="r.value"
            @click="setRange(r.value)"
            class="px-2.5 py-1 transition-colors"
            :class="range === r.value
              ? 'bg-sky-500 text-white'
              : 'text-gray-400 hover:text-gray-600 hover:bg-gray-50'"
          >{{ r.label }}</button>
        </div>
        <button
          v-if="goal"
          @click="showProjection = !showProjection"
          title="Linear projection from goal start to target weight."
          class="text-xs px-2.5 py-1 rounded-full border transition-colors"
          :class="showProjection
            ? 'bg-amber-50 border-amber-300 text-amber-600'
            : 'border-gray-200 text-gray-400 hover:text-gray-600 hover:border-gray-300'"
        >Goal projection</button>
        <button
          v-if="goal && canShowTdeeProjection"
          @click="showTdeeProjection = !showTdeeProjection"
          :title="`TDEE-based projection: ${props.dailyCalorieTarget} kcal target vs ${props.tdeeKcal} kcal TDEE = ${((props.dailyCalorieTarget! - props.tdeeKcal!) / 7700 * 7).toFixed(3)} kg/week.`"
          class="text-xs px-2.5 py-1 rounded-full border transition-colors"
          :class="showTdeeProjection
            ? 'bg-emerald-50 border-emerald-300 text-emerald-600'
            : 'border-gray-200 text-gray-400 hover:text-gray-600 hover:border-gray-300'"
        >TDEE projection</button>
      </div>
    </div>
    <div v-if="weightLogs.length > 1" class="h-44">
      <Line ref="chartRef" :data="chartData" :options="chartOptions" />
    </div>
    <p v-else class="text-sm text-gray-400 text-center py-8">Log at least 2 weight entries to see the chart.</p>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { Line } from 'vue-chartjs'
import {
  Chart as ChartJS, CategoryScale, LinearScale,
  PointElement, LineElement, Tooltip, Filler,
} from 'chart.js'
import type { ChartDataset, TooltipItem } from 'chart.js'
import ZoomPlugin from 'chartjs-plugin-zoom'
import AnnotationPlugin from 'chartjs-plugin-annotation'
import type { DailyLog, Goal, WeightUnit } from '../../types'
import { displayWeight } from '../../utils/units'

ChartJS.register(CategoryScale, LinearScale, PointElement, LineElement, Tooltip, Filler, ZoomPlugin, AnnotationPlugin)

const props = defineProps<{
  logs: DailyLog[]
  unit: WeightUnit
  goal?: Goal | null
  tdeeKcal?: number | null
  dailyCalorieTarget?: number | null
}>()

const emit = defineEmits<{
  'need-from': [date: string | null]
}>()

type Range = '7d' | '30d' | '180d' | 'all'

const ranges: { label: string; value: Range }[] = [
  { label: '7D',  value: '7d' },
  { label: '30D', value: '30d' },
  { label: '6M',  value: '180d' },
  { label: 'All', value: 'all' },
]

const rangeDays: Record<Range, number | null> = { '7d': 7, '30d': 30, '180d': 180, 'all': null }

const range = ref<Range>('30d')
const showProjection = ref(true)
const showTdeeProjection = ref(true)
const chartRef = ref<{ chart: ChartJS } | null>(null)

// kg lost (or gained) per day based on calorie deficit vs TDEE
// 7700 kcal ≈ 1 kg of body fat
const tdaeDailyKgChange = computed(() => {
  if (props.tdeeKcal == null || props.dailyCalorieTarget == null) return null
  return (props.dailyCalorieTarget - props.tdeeKcal) / 7700
})

const canShowTdeeProjection = computed(() =>
  tdaeDailyKgChange.value != null && weightLogs.value.length > 0
)

function fromDateForRange(r: Range): string | null {
  const days = rangeDays[r]
  if (days == null) return null
  const d = new Date(new Date().toLocaleDateString('en-CA') + 'T00:00:00Z')
  d.setUTCDate(d.getUTCDate() - days)
  return d.toISOString().slice(0, 10)
}

function setRange(r: Range) {
  range.value = r
  emit('need-from', fromDateForRange(r))
}

watch(range, () => chartRef.value?.chart?.resetZoom())

const weightLogs = computed(() => {
  const from = fromDateForRange(range.value)
  return [...props.logs]
    .filter((l) => l.weightKg != null && (from == null || l.date >= from))
    .sort((a, b) => a.date.localeCompare(b.date))
})

const logMap = computed(() => {
  const m = new Map<string, number>()
  for (const l of weightLogs.value) m.set(l.date, l.weightKg!)
  return m
})

// All weight logs regardless of range, for 7-day lookback
const allWeightMap = computed(() => {
  const m = new Map<string, number>()
  for (const l of props.logs) if (l.weightKg != null) m.set(l.date, l.weightKg)
  return m
})

function sliding7DayAvgKg(date: string): number | null {
  const end = new Date(date + 'T00:00:00Z')
  let sum = 0, count = 0
  for (let i = 0; i < 7; i++) {
    const d = new Date(end)
    d.setUTCDate(end.getUTCDate() - i)
    const w = allWeightMap.value.get(d.toISOString().slice(0, 10))
    if (w != null) { sum += w; count++ }
  }
  return count > 0 ? sum / count : null
}

function weeklyDates(from: string, to: string): string[] {
  const result: string[] = []
  const d = new Date(from + 'T00:00:00Z')
  const end = new Date(to + 'T00:00:00Z')
  while (d <= end) {
    result.push(d.toISOString().slice(0, 10))
    d.setUTCDate(d.getUTCDate() + 7)
  }
  if (result[result.length - 1] !== to) result.push(to)
  return result
}

const projectionStartWeight = computed(() => {
  if (!props.goal) return null
  if (props.goal.startingWeightKg != null) return props.goal.startingWeightKg
  return props.logs.find((l) => l.date === props.goal!.startDate)?.weightKg ?? null
})

const allDates = computed(() => {
  const dates = new Set(weightLogs.value.map((l) => l.date))
  const lastLogDate = weightLogs.value.at(-1)?.date
  const targetDate = props.goal?.targetDate

  const needsWaypoints =
    lastLogDate != null && targetDate != null && (
      (showProjection.value && projectionStartWeight.value != null) ||
      (showTdeeProjection.value && tdaeDailyKgChange.value != null)
    )

  if (needsWaypoints) {
    for (const d of weeklyDates(lastLogDate!, targetDate!)) dates.add(d)
  }
  if (showProjection.value && props.goal) {
    dates.add(props.goal.startDate)
  }

  return Array.from(dates).sort()
})

const spansMultipleYears = computed(() => {
  if (allDates.value.length < 2) return false
  return allDates.value[0].slice(0, 4) !== allDates.value[allDates.value.length - 1].slice(0, 4)
})

const fmt = (d: string) =>
  new Intl.DateTimeFormat('en-GB', {
    day: '2-digit',
    month: '2-digit',
    timeZone: 'UTC',
    ...(spansMultipleYears.value ? { year: '2-digit' } : {}),
  }).format(new Date(d + 'T00:00:00Z'))

const chartData = computed(() => {
  const labels = allDates.value.map(fmt)

  const weightData = allDates.value.map((date) => {
    if (!logMap.value.has(date)) return null
    const avg = sliding7DayAvgKg(date)
    return avg != null ? displayWeight(avg, props.unit) : null
  })

  const datasets: ChartDataset<'line'>[] = [{
    data: weightData,
    borderColor: '#0ea5e9',
    backgroundColor: 'rgba(14,165,233,0.07)',
    fill: true,
    tension: 0.35,
    pointRadius: 3,
    pointBackgroundColor: '#0ea5e9',
    spanGaps: true,
  }]

  if (showProjection.value && props.goal && projectionStartWeight.value != null) {
    const startDate = props.goal.startDate
    const targetDate = props.goal.targetDate
    const startW = displayWeight(projectionStartWeight.value, props.unit)
    const targetW = displayWeight(props.goal.targetWeightKg, props.unit)
    const startMs = new Date(startDate).getTime()
    const totalMs = new Date(targetDate).getTime() - startMs

    const projData = allDates.value.map((date) => {
      if (date < startDate || date > targetDate) return null
      const t = totalMs > 0 ? (new Date(date).getTime() - startMs) / totalMs : 0
      return startW + (targetW - startW) * t
    })

    const pointRadii = allDates.value.map((date) =>
      date === startDate || date === targetDate || logMap.value.has(date) ? 5 : 0
    )

    datasets.push({
      data: projData,
      borderColor: '#f59e0b',
      borderDash: [6, 4],
      borderWidth: 2,
      backgroundColor: 'transparent',
      fill: false,
      tension: 0,
      pointRadius: pointRadii,
      pointHitRadius: pointRadii,
      pointBackgroundColor: '#f59e0b',
      spanGaps: false,
    })
  }

  if (showTdeeProjection.value && props.goal && tdaeDailyKgChange.value != null) {
    const lastLog = weightLogs.value.at(-1)!
    const startDate = lastLog.date
    const targetDate = props.goal.targetDate
    const startW = displayWeight(sliding7DayAvgKg(startDate) ?? lastLog.weightKg!, props.unit)
    const displayKgPerDay = props.unit === 'lbs' ? tdaeDailyKgChange.value * 2.20462 : tdaeDailyKgChange.value
    const startMs = new Date(startDate).getTime()

    const tdeeData = allDates.value.map((date) => {
      if (date < startDate || date > targetDate) return null
      const days = (new Date(date).getTime() - startMs) / 86400000
      return startW + displayKgPerDay * days
    })

    const pointRadii = allDates.value.map((date) =>
      date === startDate || date === targetDate || logMap.value.has(date) ? 5 : 0
    )

    datasets.push({
      data: tdeeData,
      borderColor: '#10b981',
      borderDash: [6, 4],
      borderWidth: 2,
      backgroundColor: 'transparent',
      fill: false,
      tension: 0,
      pointRadius: pointRadii,
      pointHitRadius: pointRadii,
      pointBackgroundColor: '#10b981',
      spanGaps: false,
    })
  }

  return { labels, datasets }
})

const goalAnnotations = computed(() => {
  if (!props.goal) return {}
  const startLabel = fmt(props.goal.startDate)
  const endLabel = fmt(props.goal.targetDate)
  const line = (label: string, color: string) => ({
    type: 'line' as const,
    scaleID: 'x',
    value: label,
    borderColor: color,
    borderWidth: 1.5,
    borderDash: [4, 4],
  })
  return {
    goalStart: line(startLabel, '#f59e0b'),
    goalEnd:   line(endLabel,   '#f59e0b'),
  }
})

const chartOptions = computed(() => ({
  responsive: true,
  maintainAspectRatio: false,
  plugins: {
    legend: { display: false },
    annotation: { annotations: goalAnnotations.value },
    tooltip: {
      callbacks: {
        label: (ctx: TooltipItem<'line'>) =>
          `${ctx.parsed.y?.toFixed(2) ?? ''} ${props.unit}`,
      },
    },
    zoom: {
      pan: { enabled: true, mode: 'x' as const },
      zoom: {
        wheel: { enabled: true },
        pinch: { enabled: true },
        mode: 'x' as const,
      },
    },
  },
  scales: {
    x: { grid: { display: false }, ticks: { maxTicksLimit: 8 } },
    y: { grid: { color: '#f1f5f9' } },
  },
}))
</script>
