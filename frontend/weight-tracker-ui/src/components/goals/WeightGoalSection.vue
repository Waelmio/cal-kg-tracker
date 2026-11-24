<template>
  <section class="sec" aria-labelledby="wg">
    <h2 id="wg">Weight goal</h2>
    <ErrorNote v-if="actionError" :message="actionError" />

    <template v-if="!status || status.state === 'none'">
      <div class="card none">
        <div class="top"><h3>No weight goal</h3><StatusChip>Tracking only</StatusChip></div>
        <p v-if="dash?.weight.kg != null" class="big">{{ weight(dash.weight.kg, app.unit) }} <span class="unit">{{ app.unit }}<template v-if="dash.weight.paceKgPerWeek != null">, {{ dash.weight.paceKgPerWeek < 0 ? 'losing' : 'gaining' }} {{ weight(Math.abs(dash.weight.paceKgPerWeek), app.unit) }} {{ app.unit }} a week</template></span></p>
        <p v-if="dash && !dash.deficits.overall.sinceGoalStart && dash.deficits.overall.loggedDays" class="text">Deficit, last four full weeks: <strong>{{ dash.deficits.overall.kcal }} kcal</strong></p>
        <p v-if="dash?.calorieTarget" class="text">Your {{ dash.calorieTarget }} kcal daily target still applies. It is separate from the weight goal.</p>
        <div class="row" v-if="!form">
          <button type="button" class="btn-primary" @click="form = 'new'">Set a weight goal</button>
          <button v-if="dash?.calorieTarget" type="button" class="btn-secondary" @click="$emit('turn-off-target')">Turn off calorie target</button>
        </div>
      </div>
    </template>

    <GoalCard v-else :goal="status" :unit="app.unit" :today="app.today" :series="dash?.weightSeries">
      <template #actions>
        <div v-if="!form" class="row">
          <template v-if="status.state === 'active'">
            <button v-if="status.kind === 'Reach' && status.pace === 'behind' && status.projectedDate" type="button" class="btn-secondary" :disabled="busy" @click="moveDate(status.projectedDate)">Move date to {{ shortDate(status.projectedDate) }}</button>
            <button v-if="status.kind === 'Reach'" type="button" class="btn-secondary" @click="form = 'edit'">Edit</button>
            <button type="button" class="btn-secondary" :disabled="busy" @click="end('Ended')">End goal</button>
          </template>
          <template v-else-if="status.state === 'reached'">
            <button type="button" class="btn-primary" :disabled="busy" @click="maintain">Maintain {{ weight(status.targetWeightKg, app.unit) }} {{ app.unit }}</button>
            <button type="button" class="btn-secondary" @click="form = 'new'">New goal</button>
            <button type="button" class="btn-secondary" :disabled="busy" @click="end('Reached')">No goal</button>
          </template>
          <template v-else>
            <button v-if="status.projectedDate" type="button" class="btn-primary" :disabled="busy" @click="moveDate(status.projectedDate)">Extend to {{ shortDate(status.projectedDate) }}</button>
            <button type="button" class="btn-secondary" @click="form = 'new'">New goal</button>
            <button type="button" class="btn-secondary" :disabled="busy" @click="end('Ended')">Close it</button>
          </template>
        </div>
        <p v-if="status.state === 'reached'" class="foot">Maintaining keeps you within 1 {{ app.unit }} of {{ weight(status.targetWeightKg, app.unit) }} and sets the target to the calories that keep your current weight.</p>
        <p v-if="status.state === 'datePassed'" class="foot">Until you choose, the dashboard runs as tracking only. The goal stays in your history.</p>
      </template>
    </GoalCard>

    <GoalForm v-if="form" :goal="form === 'edit' ? activeGoal : null" @saved="form = null; $emit('changed')" @cancel="form = null" />

    <div v-if="past.length" class="card">
      <h3>Past goals</h3>
      <div class="table-wrap">
        <table>
          <thead><tr><th>Goal</th><th>Dates</th><th>Result</th><th>Outcome</th></tr></thead>
          <tbody>
            <tr v-for="g in past" :key="g.id">
              <td>{{ g.kind === 'Maintain' ? `Keep ${weight(g.targetWeightKg, app.unit)} ${app.unit}` : `${weight(g.startingWeightKg, app.unit)} to ${weight(g.targetWeightKg, app.unit)} ${app.unit}` }}</td>
              <td>{{ shortDate(g.startDate) }} to {{ g.endedOn ? shortDate(g.endedOn, true) : '' }}</td>
              <td>{{ result(g) }}</td>
              <td>{{ outcome(g.endReason) }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import ErrorNote from '../ui/ErrorNote.vue'
import StatusChip from '../ui/StatusChip.vue'
import GoalCard from './GoalCard.vue'
import GoalForm from './GoalForm.vue'
import { useAppStore } from '../../stores/app'
import * as goalsApi from '../../api/goals'
import { shortDate, weight } from '../../utils/format'
import type { DashboardStats, Goal, GoalEndReason } from '../../types'

const props = defineProps<{ dash: DashboardStats | null; goals: Goal[] }>()
const emit = defineEmits<{ changed: []; 'turn-off-target': [] }>()

const app = useAppStore()
const status = computed(() => props.dash?.goal ?? null)
const activeGoal = computed(() => props.goals.find((g) => g.endedOn == null) ?? null)
const past = computed(() => props.goals.filter((g) => g.endedOn != null))
const form = ref<'new' | 'edit' | null>(null)
const busy = ref(false)
const actionError = ref('')

async function run(fn: () => Promise<unknown>) {
  busy.value = true
  actionError.value = ''
  try {
    await fn()
    app.refresh()
    emit('changed')
  } catch (e) {
    actionError.value = (e as Error).message
  } finally {
    busy.value = false
  }
}

const end = (reason: 'Ended' | 'Reached') => run(() => goalsApi.end(status.value!.id, reason))
const maintain = () => run(() => goalsApi.maintain())
const moveDate = (date: string) => {
  const g = activeGoal.value
  if (!g) return
  return run(() => goalsApi.update(g.id, { targetWeightKg: g.targetWeightKg, targetDate: date, startDate: g.startDate, startingWeightKg: g.startingWeightKg, notes: g.notes }))
}

function result(g: Goal) {
  if (g.endReason === 'Reached' && g.startingWeightKg != null) {
    const d = g.startingWeightKg - g.targetWeightKg
    return `${weight(Math.abs(d), app.unit)} ${app.unit} ${d >= 0 ? 'lost' : 'gained'}`
  }
  return '–'
}
function outcome(r: GoalEndReason | null) {
  return r === 'Reached' ? 'Reached' : r === 'Replaced' ? 'Replaced by a new goal' : r === 'Maintain' ? 'Switched to maintaining' : 'Ended early'
}
</script>

<style scoped>
.sec { display: flex; flex-direction: column; gap: 12px; border-left: 4px solid #0284c7; padding-left: 14px; }
h2 { margin: 0; font-size: 18px; color: #075985; }
h3 { margin: 0; font-size: 15px; }
.card { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 18px; display: flex; flex-direction: column; gap: 10px; }
.top { display: flex; justify-content: space-between; align-items: center; gap: 8px; flex-wrap: wrap; }
.big { margin: 0; font-size: 28px; font-weight: 700; }
.unit { font-size: 14px; font-weight: 400; color: #4b5563; }
.text { margin: 0; color: #374151; }
.row { display: flex; gap: 8px; flex-wrap: wrap; }
.row button { min-height: 44px; }
.foot { margin: 0; color: #4b5563; font-size: 13px; }
.table-wrap { overflow-x: auto; }
table { width: 100%; border-collapse: collapse; font-size: 14px; }
th { text-align: left; font-size: 13px; color: #4b5563; padding: 6px 8px; border-bottom: 1px solid #e5e7eb; }
td { padding: 8px; border-bottom: 1px solid #f3f4f6; }
</style>
