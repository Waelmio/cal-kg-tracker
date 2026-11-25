<template>
  <section class="card" :class="goal ? tone : ''" aria-labelledby="goal-card">
    <div class="top">
      <h2 id="goal-card">{{ !goal ? 'No weight goal' : (goal.kind === 'Maintain' ? '' : 'Goal: ') + goalTitle(goal, unit) }}</h2>
      <StatusChip :kind="chip.kind">{{ chip.text }}</StatusChip>
    </div>

    <p v-if="!goal" class="text">Your weight is tracked without a target. Set a goal to see your progress and when you will get there.</p>
    <template v-else>
      <p class="text">{{ sentence }}</p>
      <p v-if="goal.state === 'datePassed'" class="text">Until you choose what is next, the dashboard runs as tracking only.</p>

      <div v-if="goal.progressPercent != null && goal.kind === 'Reach'">
        <div class="ends"><span>{{ weight(goal.startingWeightKg, unit, true) }}</span><strong>{{ weight(goal.currentKg, unit, true) }}, {{ Math.round(goal.progressPercent) }}%</strong><span>{{ weight(goal.targetWeightKg, unit, true) }}</span></div>
        <div class="track" role="progressbar" :aria-valuenow="Math.round(goal.progressPercent)" aria-valuemin="0" aria-valuemax="100" :aria-label="`${Math.round(goal.progressPercent)}% of the way`">
          <div :style="{ width: goal.progressPercent + '%' }" />
        </div>
      </div>

      <GoalChart v-if="series && goal.kind === 'Reach'" :goal="goal" :series="series" :today="today" :unit="unit" />
    </template>

    <RouterLink v-if="!goal || goal.state === 'reached' || goal.state === 'datePassed'" to="/goals" class="go">{{ goal ? 'Choose what is next' : 'Set a weight goal' }}</RouterLink>
  </section>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import StatusChip from '../ui/StatusChip.vue'
import GoalChart from './GoalChart.vue'
import { goalChip, goalSentence, goalTitle, type ChipKind } from '../../utils/goalText'
import { weight } from '../../utils/format'
import type { DashboardStats, GoalStatus, WeightUnit } from '../../types'

const props = defineProps<{ goal: GoalStatus | null; unit: WeightUnit; today: string; series?: DashboardStats['weightSeries'] }>()
const chip = computed<{ kind: ChipKind; text: string }>(() => (props.goal ? goalChip(props.goal, props.unit) : { kind: 'neutral', text: 'Tracking only' }))
const sentence = computed(() => (props.goal ? goalSentence(props.goal, props.unit) : ''))
const tone = computed(() => (props.goal?.state === 'reached' ? 'green' : props.goal?.state === 'datePassed' ? 'yellow' : ''))
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
.card.green { background: #ecfdf5; border-color: #a4f4cf; }
.card.yellow { background: #fffbeb; border-color: #fee685; }
.go { align-self: flex-start; display: inline-flex; align-items: center; min-height: 44px; padding: 0 18px; border-radius: 8px; background: #0369a1; color: #fff; font-weight: 600; text-decoration: none; }
.go:hover { background: #075985; }
.go:focus-visible { outline: 2px solid #0284c7; outline-offset: 2px; }
</style>
