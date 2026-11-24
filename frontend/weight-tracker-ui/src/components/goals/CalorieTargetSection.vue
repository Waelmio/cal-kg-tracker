<template>
  <section class="panel" aria-labelledby="ct-h">
    <div>
      <h2 id="ct-h">Calorie target</h2>
      <div v-if="current != null" class="big">{{ current }} <span class="by">kcal a day, {{ app.settings.averagingWeeks }}-week average</span></div>
      <div v-else class="big"><span class="by">No calorie target</span></div>
    </div>

    <div v-if="comparison && comparison.tdee != null" class="rows">
      <div v-if="comparison.targetDeficit != null"><span>At target</span><strong>{{ deficitText(comparison.targetDeficit, comparison.targetKgPerWeek) }}</strong></div>
      <div v-if="comparison.last4Avg != null && comparison.last4Deficit != null"><span>Last 4 weeks, {{ comparison.last4Avg }}</span><strong>{{ deficitText(comparison.last4Deficit, comparison.last4KgPerWeek) }}</strong></div>
      <div v-if="comparison.onTimeKcal != null"><span>On time for the goal</span><strong>about {{ comparison.onTimeKcal }} a day</strong></div>
    </div>

    <form class="form" @submit.prevent="set">
      <div class="f"><label for="ct">New target</label><input id="ct" v-model="input" inputmode="numeric" :placeholder="current != null ? String(current) : 'kcal a day'" /></div>
      <button type="submit" class="go" :disabled="busy || !valid">Set from today</button>
    </form>
    <p v-if="error" role="alert" class="error">{{ error }}</p>

    <div v-if="history.length" class="hist">
      <div v-for="h in history" :key="h.id"><span>{{ h.range }}</span><strong>{{ h.kcal }}</strong></div>
    </div>

    <button v-if="current != null" type="button" class="off" :disabled="busy" @click="$emit('turn-off')">Turn off target</button>
  </section>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useAppStore } from '../../stores/app'
import * as api from '../../api/calorieGoals'
import { addDays } from '../../utils/dates'
import { shortDate, weightChange } from '../../utils/format'
import type { CalorieGoal, DashboardStats } from '../../types'

const props = defineProps<{ current: number | null; goals: CalorieGoal[]; comparison?: DashboardStats['targetComparison'] | null }>()
const emit = defineEmits<{ changed: []; 'turn-off': [] }>()

const app = useAppStore()
const input = ref('')
const busy = ref(false)
const error = ref('')
const valid = computed(() => /^\d{3,5}$/.test(input.value.trim()))

// "Since 15 Oct 2026" for the target in force, "1 Jun to 14 Oct 2026" for earlier ones.
const history = computed(() => {
  const sorted = [...props.goals].sort((a, b) => b.effectiveFrom.localeCompare(a.effectiveFrom))
  return sorted.map((g, i) => {
    const next = sorted[i - 1]
    return {
      id: g.id,
      range: i === 0 ? `Since ${shortDate(g.effectiveFrom, true)}` : `${shortDate(g.effectiveFrom)} to ${shortDate(addDays(next.effectiveFrom, -1), true)}`,
      kcal: g.targetCalories == null ? 'Off' : String(g.targetCalories),
    }
  })
})

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
.panel { background: #fff7ed; border: 1px solid #fed7aa; border-radius: 12px; padding: 20px; display: flex; flex-direction: column; gap: 14px; }
h2 { margin: 0; font-size: 14px; font-weight: 600; color: #9a3412; }
.big { font-size: 30px; font-weight: 700; line-height: 1.2; }
.by { font-size: 15px; font-weight: 400; color: #374151; }
.rows { display: flex; flex-direction: column; gap: 6px; }
.rows div, .hist div { display: flex; justify-content: space-between; gap: 12px; }
.rows span { white-space: nowrap; }
.rows strong { text-align: right; }
.form { display: grid; grid-template-columns: minmax(0, 1fr) auto; gap: 8px; align-items: end; }
.f { display: flex; flex-direction: column; gap: 6px; }
label { font-weight: 600; }
input { box-sizing: border-box; width: 100%; height: 46px; border: 1px solid #9ca3af; border-radius: 8px; padding: 0 12px; font: inherit; font-weight: 600; }
.go { min-height: 46px; padding: 0 18px; border: 0; border-radius: 8px; background: #9a3412; color: #fff; font: inherit; font-weight: 600; }
.go:disabled { opacity: 0.5; }
.hist { border-top: 1px solid #fed7aa; padding-top: 10px; display: flex; flex-direction: column; gap: 4px; }
.off { align-self: flex-start; min-height: 44px; padding: 0 16px; border: 1px solid #fdba74; border-radius: 8px; background: #fff; color: #9a3412; font: inherit; font-weight: 600; }
.error { margin: 0; color: #a50036; background: #ffe4e6; padding: 10px 12px; border-radius: 8px; }
button:focus-visible, input:focus-visible { outline: 2px solid #0284c7; outline-offset: 2px; }
</style>
