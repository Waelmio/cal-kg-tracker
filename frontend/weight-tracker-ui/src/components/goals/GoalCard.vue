<template>
  <section class="card" :aria-labelledby="`goal-${goal.id}`">
    <div class="top">
      <h2 :id="`goal-${goal.id}`">{{ goal.kind === 'Maintain' ? '' : 'Goal: ' }}{{ goalTitle(goal, unit) }}</h2>
      <StatusChip :kind="chip.kind">{{ chip.text }}</StatusChip>
    </div>
    <p class="text">{{ sentence }}</p>

    <div v-if="goal.progressPercent != null && goal.kind === 'Reach'" class="progress">
      <div class="ends"><span>{{ weight(goal.startingWeightKg, unit, true) }}</span><strong>{{ weight(goal.currentKg, unit, true) }}, {{ Math.round(goal.progressPercent) }}%</strong><span>{{ weight(goal.targetWeightKg, unit, true) }}</span></div>
      <div class="track" role="progressbar" :aria-valuenow="Math.round(goal.progressPercent)" aria-valuemin="0" aria-valuemax="100" :aria-label="`${Math.round(goal.progressPercent)}% of the way`">
        <div :style="{ width: goal.progressPercent + '%' }" />
      </div>
    </div>

    <div class="kv" v-if="goal.kind === 'Reach' && goal.state === 'active'">
      <div v-if="goal.neededKgPerWeek != null"><InfoTip label="Needed" text="Weekly loss to reach your goal on time." /> <strong>{{ weight(goal.neededKgPerWeek, unit) }} {{ unit }} a week</strong></div>
      <div v-if="goal.paceKgPerWeek != null"><InfoTip label="Your pace" text="Trend of your weigh-ins over the last 28 days." /> <strong>{{ weight(Math.abs(goal.paceKgPerWeek), unit) }} {{ unit }} a week</strong></div>
    </div>

    <GoalChart v-if="series && goal.kind === 'Reach'" :goal="goal" :series="series" :today="today" :unit="unit" />
    <slot name="actions" />
  </section>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import StatusChip from '../ui/StatusChip.vue'
import InfoTip from '../ui/InfoTip.vue'
import GoalChart from './GoalChart.vue'
import { goalChip, goalSentence, goalTitle } from '../../utils/goalText'
import { weight } from '../../utils/format'
import type { DashboardStats, GoalStatus, WeightUnit } from '../../types'

const props = defineProps<{ goal: GoalStatus; unit: WeightUnit; today: string; series?: DashboardStats['weightSeries'] }>()
const chip = computed(() => goalChip(props.goal))
const sentence = computed(() => goalSentence(props.goal, props.unit))
</script>

<style scoped>
.card { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 18px; display: flex; flex-direction: column; gap: 12px; }
.top { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: center; gap: 8px 16px; }
h2 { margin: 0; font-size: 16px; font-weight: 600; }
.text { margin: 0; color: #374151; }
.ends { display: flex; justify-content: space-between; gap: 8px; font-size: 13px; color: #4b5563; }
.ends strong { color: #1f2937; }
.track { height: 8px; background: #e5e7eb; border-radius: 4px; overflow: hidden; margin-top: 4px; }
.track > div { height: 8px; background: #0284c7; }
.kv { display: flex; flex-wrap: wrap; gap: 4px 24px; color: #4b5563; }
.kv strong { color: #1f2937; }
</style>
