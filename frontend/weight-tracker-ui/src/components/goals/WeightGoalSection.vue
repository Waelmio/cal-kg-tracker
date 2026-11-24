<template>
  <div class="stack">
    <ErrorNote v-if="actionError" :message="actionError" />

    <!-- No goal -->
    <section v-if="!status || status.state === 'none'" class="panel blue" aria-labelledby="wg">
      <div class="head">
        <div>
          <h2 id="wg">Weight goal</h2>
          <div class="big">No weight goal</div>
        </div>
        <StatusChip>Tracking only</StatusChip>
      </div>
      <p v-if="dash?.weight.kg != null" class="line"><strong>{{ weight(dash.weight.kg, app.unit) }} {{ app.unit }}</strong><template v-if="dash.weight.paceKgPerWeek != null">, {{ dash.weight.paceKgPerWeek < 0 ? 'losing' : 'gaining' }} {{ weight(Math.abs(dash.weight.paceKgPerWeek), app.unit) }} {{ app.unit }} a week</template></p>
      <p v-if="dash && !dash.deficits.overall.sinceGoalStart && dash.deficits.overall.loggedDays" class="line">Deficit, last four full weeks: <strong>{{ dash.deficits.overall.kcal }} kcal</strong></p>
      <p v-if="dash?.calorieTarget" class="line">Your {{ dash.calorieTarget }} kcal daily target still applies. It is separate from the weight goal.</p>
      <div v-if="!form" class="row">
        <button type="button" class="btn solid" @click="form = 'new'">Set a weight goal</button>
        <button v-if="dash?.calorieTarget" type="button" class="btn outline" @click="$emit('turn-off-target')">Turn off calorie target</button>
      </div>
    </section>

    <!-- A goal -->
    <section v-else class="panel blue" aria-labelledby="wg">
      <div class="head">
        <div>
          <h2 id="wg">Weight goal</h2>
          <div class="big">{{ bigTarget }} <span class="by">{{ status.kind === 'Maintain' ? 'keep your current weight' : status.targetDate ? 'by ' + fullDateShort(status.targetDate) : '' }}</span></div>
        </div>
        <StatusChip :kind="chip.kind">{{ chip.text }}</StatusChip>
      </div>

      <div v-if="status.progressPercent != null && status.kind === 'Reach'" class="progress">
        <div class="track" role="progressbar" :aria-valuenow="Math.round(status.progressPercent)" aria-valuemin="0" aria-valuemax="100" :aria-label="`${Math.round(status.progressPercent)}% of the way`"><div :style="{ width: status.progressPercent + '%' }" /></div>
        <div class="caption">
          <span>{{ weight(status.startingWeightKg, app.unit) }} {{ app.unit }} on {{ shortDate(status.startDate) }}</span>
          <strong class="now">{{ weight(status.currentKg, app.unit) }} {{ app.unit }} now, {{ Math.round(status.progressPercent) }}%</strong>
          <span>{{ weight(status.remainingKg, app.unit) }} {{ app.unit }} to go</span>
        </div>
      </div>

      <p v-if="status.state !== 'active' || status.kind === 'Maintain'" class="line">{{ sentence }}</p>

      <div v-if="status.kind === 'Reach' && status.state === 'active'" class="cols">
        <div class="col">
          <h3>Weight {{ losing ? 'loss' : 'gain' }} per week</h3>
          <div v-for="r in rates" :key="r.name" class="rate">
            <span class="rn">{{ r.name }}</span>
            <div class="rbar"><div :style="{ width: r.w + '%', background: r.color }" /></div>
            <strong class="rv">{{ r.v }}</strong>
          </div>
          <div class="line">{{ sentence }}</div>
        </div>
        <div v-if="bmi" class="col">
          <h3><InfoTip label="BMI" text="Weight compared with height." /></h3>
          <div class="bmi">
            <span class="pin" :style="{ left: bmi.goal + '%', marginLeft: '-60px' }">{{ dash!.weight.bmiAtGoal }} goal</span>
            <span class="pin" :style="{ left: bmi.now + '%' }">{{ dash!.weight.bmi }} now</span>
            <div class="scale"><i style="width: 8.3%; background: #7dd3fc; border-radius: 4px 0 0 4px" /><i style="width: 36.1%; background: #00bc7d" /><i style="width: 27.8%; background: #ffb900" /><i style="width: 27.8%; background: #ff2056; border-radius: 0 4px 4px 0" /></div>
            <div class="tick" :style="{ left: bmi.goal + '%' }" /><div class="tick" :style="{ left: bmi.now + '%' }" />
          </div>
          <div class="zones"><span style="width: 8.3%" /><span style="width: 36.1%">Normal 18.5</span><span style="width: 27.8%">Over 25</span><span style="width: 27.8%">Obese 30</span></div>
        </div>
      </div>

      <div v-if="!form" class="row">
        <template v-if="status.state === 'active'">
          <button v-if="status.kind === 'Reach'" type="button" class="btn solid" @click="form = 'edit'" :aria-expanded="false">Edit goal</button>
          <button v-if="status.kind === 'Reach' && status.pace === 'behind' && status.projectedDate" type="button" class="btn outline" :disabled="busy" @click="moveDate(status.projectedDate)">Move date to {{ shortDate(status.projectedDate) }}</button>
          <button type="button" class="btn outline" :disabled="busy" @click="end('Ended')">End goal</button>
        </template>
        <template v-else-if="status.state === 'reached'">
          <button type="button" class="btn solid" :disabled="busy" @click="maintain">Maintain {{ weight(status.targetWeightKg, app.unit) }} {{ app.unit }}</button>
          <button type="button" class="btn outline" @click="form = 'new'">New goal</button>
          <button type="button" class="btn outline" :disabled="busy" @click="end('Reached')">No goal</button>
        </template>
        <template v-else>
          <button v-if="status.projectedDate" type="button" class="btn solid" :disabled="busy" @click="moveDate(status.projectedDate)">Extend to {{ shortDate(status.projectedDate) }}</button>
          <button type="button" class="btn outline" @click="form = 'new'">New goal</button>
          <button type="button" class="btn outline" :disabled="busy" @click="end('Ended')">Close it</button>
        </template>
      </div>
      <p v-if="advice && status.state === 'active'" class="note">{{ advice }}</p>
      <p v-if="status.state === 'reached'" class="note">Maintaining keeps you within 1 {{ app.unit }} of {{ weight(status.targetWeightKg, app.unit) }} and sets the target to the calories that keep your current weight.</p>
      <p v-if="status.state === 'datePassed'" class="note">Until you choose, the dashboard runs as tracking only. The goal stays in your history.</p>
      <GoalForm v-if="form" :goal="form === 'edit' ? activeGoal : null" @saved="form = null; $emit('changed')" @cancel="form = null" />
    </section>
    <GoalForm v-if="form && (!status || status.state === 'none')" :goal="null" @saved="form = null; $emit('changed')" @cancel="form = null" />

    <section v-if="past.length" class="past" aria-labelledby="pg">
      <h2 id="pg">Past goals</h2>
      <div class="scroll">
        <table>
          <thead><tr><th>Goal</th><th>Dates</th><th>Result</th><th>Outcome</th></tr></thead>
          <tbody>
            <tr v-for="g in past" :key="g.id">
              <td class="b">{{ g.kind === 'Maintain' ? `Keep ${weight(g.targetWeightKg, app.unit)} ${app.unit}` : `${weight(g.startingWeightKg, app.unit)} to ${weight(g.targetWeightKg, app.unit)} ${app.unit}` }}</td>
              <td>{{ shortDate(g.startDate) }} to {{ g.endedOn ? shortDate(g.endedOn, true) : '' }}</td>
              <td>{{ result(g) }}</td>
              <td><span class="pill">{{ outcome(g.endReason) }}</span></td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import ErrorNote from '../ui/ErrorNote.vue'
import StatusChip from '../ui/StatusChip.vue'
import InfoTip from '../ui/InfoTip.vue'
import GoalForm from './GoalForm.vue'
import { useAppStore } from '../../stores/app'
import * as goalsApi from '../../api/goals'
import { fullDate, shortDate, weight } from '../../utils/format'
import { goalChip, goalSentence } from '../../utils/goalText'
import { displayWeight } from '../../utils/units'
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

const chip = computed(() => goalChip(status.value!))
const sentence = computed(() => goalSentence(status.value!, app.unit))
const losing = computed(() => (status.value?.startingWeightKg ?? 0) > (status.value?.targetWeightKg ?? 0))
const bigTarget = computed(() => `${weight(status.value!.targetWeightKg, app.unit).replace(/\.0$/, '')} ${app.unit}`)
const fullDateShort = (d: string) => fullDate(d).replace(/^(\d+) (\w{3})\w* /, '$1 $2 ')

const advice = computed(() => {
  const c = props.dash?.targetComparison
  if (!c || c.onTimeKcal == null || c.last4Avg == null) return ''
  return `You averaged ${c.last4Avg} kcal over the last four full weeks. About ${c.onTimeKcal} gets you there on time.`
})

// Weekly rates as bars, the biggest is the full width.
const rates = computed(() => {
  const s = status.value
  const c = props.dash?.targetComparison
  if (!s) return []
  const rows: { name: string; kg: number; color: string }[] = []
  if (s.neededKgPerWeek != null) rows.push({ name: 'Needed', kg: s.neededKgPerWeek, color: '#7dd3fc' })
  if (c?.targetKgPerWeek != null && c.target != null) rows.push({ name: `At ${c.target} a day`, kg: Math.abs(c.targetKgPerWeek), color: '#fb923c' })
  if (s.paceKgPerWeek != null) rows.push({ name: 'Your pace', kg: Math.abs(s.paceKgPerWeek), color: '#0284c7' })
  const max = Math.max(...rows.map((r) => r.kg), 0.001)
  return rows.map((r) => ({ name: r.name, color: r.color, w: Math.round((r.kg / max) * 100), v: `${(Math.round(displayWeight(r.kg, app.unit) * 100) / 100).toFixed(2)} ${app.unit}` }))
})

// BMI scale from 17 to 35.
const pos = (v: number) => Math.min(100, Math.max(0, ((v - 17) / 18) * 100))
const bmi = computed(() => {
  const w = props.dash?.weight
  return w?.bmi != null && w.bmiAtGoal != null ? { goal: pos(w.bmiAtGoal), now: pos(w.bmi) } : null
})

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
.stack { display: flex; flex-direction: column; gap: 16px; }
.panel { border-radius: 12px; padding: 20px; display: flex; flex-direction: column; gap: 16px; }
.panel.blue { background: #f0f9ff; border: 1px solid #bae6fd; }
.head { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: flex-start; gap: 8px 16px; }
h2 { margin: 0; font-size: 14px; font-weight: 600; color: #0369a1; }
h3 { margin: 0; font-size: 14px; font-weight: 600; }
.big { font-size: 30px; font-weight: 700; line-height: 1.2; }
.by { font-size: 15px; font-weight: 400; color: #374151; }
.line { margin: 0; color: #374151; }
.progress { display: flex; flex-direction: column; gap: 6px; }
.track { height: 14px; background: #bae6fd; border-radius: 7px; overflow: hidden; }
.track > div { height: 14px; background: #0284c7; }
.caption { display: flex; justify-content: space-between; gap: 12px; flex-wrap: wrap; }
.now { color: #0369a1; }
.cols { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 320px), 1fr)); gap: 20px 32px; }
.col { display: flex; flex-direction: column; gap: 8px; }
.rate { display: grid; grid-template-columns: 118px minmax(0, 1fr) 72px; gap: 10px; align-items: center; }
.rn { color: #374151; }
.rbar { height: 14px; }
.rbar > div { height: 14px; border-radius: 0 4px 4px 0; }
.rv { text-align: right; white-space: nowrap; }
.bmi { position: relative; padding-top: 22px; }
.pin { position: absolute; top: 0; width: 60px; text-align: center; font-weight: 700; white-space: nowrap; }
.scale { display: flex; gap: 2px; height: 14px; }
.scale i { display: block; }
.tick { position: absolute; top: 20px; width: 3px; height: 18px; margin-left: -1px; background: #1f2937; }
.zones { display: flex; gap: 2px; font-size: 12px; color: #374151; }
.row { display: flex; flex-wrap: wrap; gap: 10px; }
.btn { min-height: 44px; padding: 0 18px; border-radius: 8px; font: inherit; font-weight: 600; }
.btn.solid { border: 0; background: #0369a1; color: #fff; }
.btn.solid:hover:not(:disabled) { background: #075985; }
.btn.outline { border: 1px solid #7dd3fc; background: #fff; color: #075985; }
.btn:focus-visible { outline: 2px solid #0284c7; outline-offset: 2px; }
.note { margin: 0; color: #4b5563; font-size: 13px; }
.past { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 20px; display: flex; flex-direction: column; gap: 10px; }
.past h2 { font-size: 16px; color: #1f2937; }
.scroll { overflow-x: auto; }
table { width: 100%; min-width: 520px; border-collapse: collapse; text-align: left; }
th { padding: 6px 0; font-weight: 600; font-size: 13px; color: #4b5563; }
td { padding: 10px 0; border-top: 1px solid #e5e7eb; }
td.b { font-weight: 600; }
.pill { background: #f3f4f6; color: #374151; font-weight: 600; font-size: 13px; padding: 4px 10px; border-radius: 999px; }
</style>
