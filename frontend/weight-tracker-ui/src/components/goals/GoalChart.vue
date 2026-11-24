<template>
  <div class="wrap">
    <svg :viewBox="`0 0 ${g.width} ${g.height}`" role="img" :aria-label="label" font-family="ui-sans-serif, system-ui, sans-serif" font-size="12">
      <path :d="gridPath" stroke="#e5e7eb" stroke-width="1" fill="none" />
      <text v-for="t in g.yTicks" :key="t.kg" x="36" :y="t.y + 4" text-anchor="end" fill="#4b5563">{{ tick(t.kg) }}</text>
      <text v-for="t in g.xTicks" :key="t.date" :x="t.x" :y="g.height - 8" text-anchor="middle" fill="#4b5563">{{ shortDate(t.date) }}</text>
      <path :d="g.goalLine" stroke="#6b7280" stroke-width="1.5" fill="none" />
      <circle v-for="(d, i) in g.dots" :key="i" :cx="d.x" :cy="d.y" r="2.5" fill="#9ca3af" />
      <path :d="g.trend" stroke="#0284c7" stroke-width="2" stroke-linejoin="round" stroke-linecap="round" fill="none" />
      <path v-if="g.projection" :d="g.projection" stroke="#0284c7" stroke-width="2" stroke-dasharray="5 5" fill="none" />
      <template v-if="g.current">
        <circle :cx="g.current.x" :cy="g.current.y" r="5" fill="#0284c7" stroke="#fff" stroke-width="2" />
        <text :x="g.current.x + 9" :y="g.current.y - 8" fill="#1f2937" font-weight="600">{{ tick(g.current.kg) }}</text>
      </template>
      <text :x="g.width - 74" :y="g.y(goal.targetWeightKg) + 4" fill="#1f2937">{{ tick(goal.targetWeightKg) }} goal</text>
    </svg>
    <ul class="legend">
      <li><i class="dot" />Weigh-in</li>
      <li><i class="line" />7-day average</li>
      <li v-if="g.projection"><i class="dashed" />Current pace</li>
      <li><i class="goal" />Goal line</li>
    </ul>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { buildGoalChart } from '../../utils/chart'
import { addDays } from '../../utils/dates'
import { shortDate } from '../../utils/format'
import { displayWeight } from '../../utils/units'
import type { DashboardStats, GoalStatus, WeightUnit } from '../../types'

const props = defineProps<{ goal: GoalStatus; series: DashboardStats['weightSeries']; today: string; unit: WeightUnit }>()

const g = computed(() => {
  const goal = props.goal
  const target = goal.targetDate ?? addDays(props.today, 90)
  const end = goal.projectedDate && goal.projectedDate > target ? goal.projectedDate : target
  return buildGoalChart({
    series: props.series.map((p) => ({ ...p, kg: displayWeight(p.kg, props.unit), avg7: displayWeight(p.avg7, props.unit) })),
    today: props.today,
    start: goal.startDate,
    end,
    targetKg: displayWeight(goal.targetWeightKg, props.unit),
    startKg: goal.startingWeightKg == null ? null : displayWeight(goal.startingWeightKg, props.unit),
    projection: goal.projectedDate ? { date: goal.projectedDate, kg: displayWeight(goal.targetWeightKg, props.unit) } : null,
  })
})

const gridPath = computed(() => g.value.yTicks.map((t) => `M44 ${t.y}H${g.value.width - 80}`).join(''))
const tick = (v: number) => String(Math.round(v * 10) / 10)
const label = computed(() => `Weight since ${shortDate(props.goal.startDate)} with the goal line${props.goal.projectedDate ? ' and the projection' : ''}`)
</script>

<style scoped>
.wrap { overflow-x: auto; }
svg { display: block; width: 100%; min-width: 620px; height: auto; }
.legend { display: flex; flex-wrap: wrap; gap: 4px 18px; font-size: 13px; color: #4b5563; list-style: none; margin: 8px 0 0; padding: 0; }
.legend li { display: inline-flex; align-items: center; gap: 6px; }
.dot { width: 7px; height: 7px; border-radius: 50%; background: #9ca3af; display: inline-block; }
.line { width: 18px; height: 2px; background: #0284c7; display: inline-block; }
.dashed { width: 18px; border-top: 2px dashed #0284c7; display: inline-block; }
.goal { width: 18px; height: 2px; background: #6b7280; display: inline-block; }
</style>
