<template>
  <section class="sec" aria-labelledby="ct-h">
    <h2 id="ct-h">Calorie target</h2>
    <div class="card">
      <template v-if="current != null">
        <p class="big">{{ current }} <span class="unit">kcal a day, {{ app.settings.averagingWeeks }}-week average</span></p>
        <p class="text">The target is met on average over {{ app.settings.averagingWeeks }} {{ app.settings.averagingWeeks === 1 ? 'week' : 'weeks' }}, not every day.</p>
      </template>
      <p v-else class="big"><span class="unit">No calorie target</span></p>

      <dl v-if="comparison && comparison.tdee != null" class="cmp">
        <div v-if="comparison.targetDeficit != null"><dt>At target</dt><dd>{{ deficitText(comparison.targetDeficit, comparison.targetKgPerWeek) }}</dd></div>
        <div v-if="comparison.last4Avg != null && comparison.last4Deficit != null"><dt>Last 4 weeks, {{ comparison.last4Avg }}</dt><dd>{{ deficitText(comparison.last4Deficit, comparison.last4KgPerWeek) }}</dd></div>
        <div v-if="comparison.onTimeKcal != null"><dt>On time for the goal</dt><dd>about {{ comparison.onTimeKcal }} a day</dd></div>
      </dl>

      <form class="row" @submit.prevent="set">
        <div class="f">
          <label for="ct">New target</label>
          <input id="ct" v-model="input" class="input" inputmode="numeric" placeholder="kcal a day" />
        </div>
        <button type="submit" class="btn-primary" :disabled="busy || !valid">Set from today</button>
        <button v-if="current != null" type="button" class="btn-secondary" :disabled="busy" @click="$emit('turn-off')">Turn off target</button>
      </form>
      <p v-if="error" role="alert" class="error">{{ error }}</p>

      <ul v-if="history.length" class="hist">
        <li v-for="h in history" :key="h.id"><span>{{ h.since }}</span><strong>{{ h.kcal }}</strong></li>
      </ul>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useAppStore } from '../../stores/app'
import * as api from '../../api/calorieGoals'
import { shortDate } from '../../utils/format'
import { weightChange } from '../../utils/format'
import type { CalorieGoal, DashboardStats } from '../../types'

const props = defineProps<{ current: number | null; goals: CalorieGoal[]; comparison?: DashboardStats['targetComparison'] | null }>()
const emit = defineEmits<{ changed: []; 'turn-off': [] }>()

const app = useAppStore()
const input = ref('')
const busy = ref(false)
const error = ref('')
const valid = computed(() => /^\d{3,5}$/.test(input.value.trim()))

const history = computed(() =>
  [...props.goals].sort((a, b) => b.effectiveFrom.localeCompare(a.effectiveFrom)).map((g) => ({
    id: g.id, since: `Since ${shortDate(g.effectiveFrom, true)}`, kcal: g.targetCalories == null ? 'Off' : String(g.targetCalories),
  })),
)

function deficitText(deficit: number, kgPerWeek: number | null) {
  const side = deficit >= 0 ? 'below' : 'above'
  const pace = kgPerWeek == null ? '' : `, ${weightChange(-kgPerWeek, app.unit, 2)} ${app.unit} a week`
  return `${Math.abs(deficit)} ${side} TDEE${pace}`
}

async function set() {
  busy.value = true
  error.value = ''
  try {
    await api.create(Number(input.value.trim()))
    input.value = ''
    app.refresh()
    emit('changed')
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busy.value = false
  }
}
</script>

<style scoped>
.sec { display: flex; flex-direction: column; gap: 12px; border-left: 4px solid #ea580c; padding-left: 14px; }
h2 { margin: 0; font-size: 18px; color: #9a3412; }
.card { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 18px; display: flex; flex-direction: column; gap: 10px; }
.big { margin: 0; font-size: 28px; font-weight: 700; }
.unit { font-size: 14px; font-weight: 400; color: #4b5563; }
.text { margin: 0; color: #374151; }
.row { display: flex; flex-wrap: wrap; align-items: flex-end; gap: 8px; }
.f { display: flex; flex-direction: column; gap: 4px; }
label { font-weight: 600; }
.input { min-height: 44px; width: 160px; }
.row button { min-height: 44px; }
.cmp { margin: 0; display: flex; flex-direction: column; gap: 4px; }
.cmp div { display: flex; justify-content: space-between; gap: 12px; }
.cmp dt { color: #4b5563; }
.cmp dd { margin: 0; font-weight: 600; text-align: right; }
.hist { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 4px; color: #4b5563; }
.hist li { display: flex; justify-content: space-between; gap: 12px; }
.hist strong { color: #1f2937; }
.error { margin: 0; color: #a50036; background: #ffe4e6; padding: 10px 12px; border-radius: 8px; }
</style>
