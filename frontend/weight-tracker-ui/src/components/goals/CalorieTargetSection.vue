<template>
  <section class="sec" aria-labelledby="ct-h">
    <h2 id="ct-h">Calorie target</h2>
    <div class="card">
      <template v-if="current != null">
        <p class="big">{{ current }} <span class="unit">kcal a day, {{ app.settings.averagingWeeks }}-week average</span></p>
        <p class="text">The target is met on average over {{ app.settings.averagingWeeks }} {{ app.settings.averagingWeeks === 1 ? 'week' : 'weeks' }}, not every day.</p>
      </template>
      <p v-else class="big"><span class="unit">No calorie target</span></p>

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
import type { CalorieGoal } from '../../types'

const props = defineProps<{ current: number | null; goals: CalorieGoal[] }>()
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
.hist { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 4px; color: #4b5563; }
.hist li { display: flex; justify-content: space-between; gap: 12px; }
.hist strong { color: #1f2937; }
.error { margin: 0; color: #a50036; background: #ffe4e6; padding: 10px 12px; border-radius: 8px; }
</style>
