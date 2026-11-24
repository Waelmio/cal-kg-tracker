<template>
  <Modal :title="title" max-width="760px" @close="$emit('close')">
    <div class="nav">
      <button type="button" class="step" aria-label="Previous month" @click="$emit('change', addMonths(month, -1))">‹</button>
      <strong>{{ title }}</strong>
      <button type="button" class="step" aria-label="Next month" :disabled="month >= monthOf(app.today)" @click="$emit('change', addMonths(month, 1))">›</button>
    </div>

    <ErrorNote v-if="error" :message="error" :retry="reload" />
    <p v-else-if="loading && !data" class="muted" aria-busy="true">Loading…</p>

    <template v-if="data">
      <p v-if="f.loggedDays === 0" class="muted">Nothing logged in {{ title }}.</p>

      <div class="cards">
        <section class="c">
          <h2>Calories</h2>
          <div class="big">{{ kcal(f.avgCalories) }} <span class="unit">kcal average</span></div>
          <TileRow label="Target" :value="kcal(f.avgTarget)" />
          <TileRow label="Days logged" :value="`${f.loggedDays} / ${data.daysInMonth}`" />
          <TileRow label="Cheat days" :value="data.cheatDays" />
        </section>

        <section class="c">
          <h2>Weight</h2>
          <div class="big">{{ weight(f.avgWeightKg, app.unit) }} <span class="unit">{{ app.unit }} average</span></div>
          <TileRow :label="`From ${prevMonthName}`">
            <span v-if="f.weightChangeKg != null">{{ weightChange(f.weightChangeKg, app.unit) }} {{ app.unit }}</span><span v-else>–</span>
          </TileRow>
          <TileRow label="First to last" :value="`${weight(data.startWeightKg, app.unit)} → ${weight(data.endWeightKg, app.unit)}`" />
          <TileRow label="Weigh-ins" :value="f.weighIns" />
        </section>

        <section class="c">
          <h2>Deficit</h2>
          <div class="big" :style="{ color: f.deficitKcal >= 0 ? '#007a55' : '#c70036' }">{{ kcal(Math.abs(f.deficitKcal)) }} <span class="unit">kcal {{ f.deficitKcal >= 0 ? 'below' : 'above' }} TDEE</span></div>
          <TileRow>
            <template #label><InfoTip label="Worth about" text="7700 kcal is about 1 kg." /></template>
            {{ Math.abs(f.deficitKg).toFixed(2) }} kg {{ f.deficitKg >= 0 ? 'lost' : 'gained' }}
          </TileRow>
          <TileRow label="Against target">
            <span :style="{ color: f.gap > 0 ? '#c70036' : '#007a55' }">{{ Math.abs(f.gap) }} {{ f.gap > 0 ? 'over' : 'under' }}</span>
          </TileRow>
          <TileRow>
            <template #label><InfoTip label="Estimated TDEE" text="From the 90 days ending this month." /></template>
            {{ data.tdee ? kcal(data.tdee.kcal) : 'Not enough data' }}
          </TileRow>
        </section>
      </div>

      <section v-if="days.length" class="perday">
        <h2>Calories per day, with each day's target</h2>
        <div class="days" role="list">
          <div v-for="d in days" :key="d.date" class="d" role="listitem" :title="d.tip" :aria-label="d.tip">
            <div class="plot">
              <div v-if="d.target" class="tgt" :style="{ bottom: d.targetPct + '%' }" />
              <div v-if="d.calories != null" class="bar" :style="{ height: d.pct + '%', background: d.color }" />
            </div>
            <span class="n">{{ d.n }}</span>
          </div>
        </div>
        <ul class="legend">
          <li><i :style="{ background: STATUS_COLORS.under.solid }" />On or under target</li>
          <li><i :style="{ background: STATUS_COLORS.balanced.solid }" />Over, {{ app.settings.averagingWeeks }}-week average ok</li>
          <li><i :style="{ background: STATUS_COLORS.over.solid }" />Over target</li>
          <li><i class="t" />Target</li>
        </ul>
      </section>

      <div class="table-wrap">
        <table>
          <thead><tr><th>Week</th><th>Average</th><th>Against target</th><th>Deficit</th><th>Weight</th><th>Logged</th></tr></thead>
          <tbody>
            <tr v-for="w in data.weeks" :key="w.monday">
              <td><span class="dot" :style="{ background: w.figures.status ? STATUS_COLORS[w.figures.status].solid : '#d1d5db' }" /> W{{ w.isoWeek }}<span class="muted"> {{ shortDate(w.figures.from) }}</span></td>
              <td>{{ kcal(w.figures.avgCalories) }}</td>
              <td>{{ w.figures.loggedDays ? signed(w.figures.gap) : '–' }}</td>
              <td>{{ w.figures.loggedDays ? kcal(w.figures.deficitKcal) : '–' }}</td>
              <td>{{ weight(w.figures.avgWeightKg, app.unit) }} <span class="muted">{{ weightChange(w.figures.weightChangeKg, app.unit) }}</span></td>
              <td>{{ w.figures.loggedDays }} / 7</td>
            </tr>
          </tbody>
        </table>
      </div>
      <p class="muted">Weeks are full weeks, as in the calendar. Totals above count {{ monthTitle(month).split(' ')[0] }} only.</p>
    </template>
  </Modal>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import Modal from '../ui/Modal.vue'
import ErrorNote from '../ui/ErrorNote.vue'
import InfoTip from '../ui/InfoTip.vue'
import TileRow from '../ui/TileRow.vue'
import { useAppStore } from '../../stores/app'
import { useAsyncData } from '../../composables/useAsyncData'
import * as statsApi from '../../api/stats'
import { addMonths, lastDayOfMonth, monthOf } from '../../utils/dates'
import { kcal, monthTitle, shortDate, signed, STATUS_COLORS, weight, weightChange } from '../../utils/format'
import { computed as c2, toRef } from 'vue'

const props = defineProps<{ month: string }>()
defineEmits<{ close: []; change: [month: string] }>()

const app = useAppStore()
const monthRef = toRef(props, 'month')
const title = computed(() => monthTitle(props.month))
const prevMonthName = computed(() => monthTitle(addMonths(props.month, -1)).split(' ')[0])

const { data, loading, error, reload } = useAsyncData(async () => {
  const [summary, calendar] = await Promise.all([
    statsApi.getMonth(props.month),
    statsApi.getCalendar(`${props.month}-01`, lastDayOfMonth(props.month)),
  ])
  return { ...summary, calendarDays: calendar.days }
}, [monthRef])

const f = c2(() => data.value!.figures)

const days = computed(() => {
  const list = data.value?.calendarDays ?? []
  const max = Math.max(1, ...list.map((d) => Math.max(d.calories ?? 0, d.target ?? 0)))
  return list.map((d) => ({
    date: d.date, n: Number(d.date.slice(8)), calories: d.calories, target: d.target,
    pct: d.calories == null ? 0 : Math.max(2, (d.calories / max) * 100),
    targetPct: d.target == null ? 0 : (d.target / max) * 100,
    color: d.status ? STATUS_COLORS[d.status].solid : '#d1d5db',
    tip: d.calories == null ? `${d.date}: not logged` : `${d.date}: ${d.calories} kcal, target ${d.target ?? '–'}`,
  }))
})

void monthOf
</script>

<style scoped>
.nav { display: flex; align-items: center; justify-content: center; gap: 8px; margin-bottom: 8px; }
.step { all: unset; cursor: pointer; width: 44px; height: 44px; display: grid; place-items: center; font-size: 22px; border-radius: 8px; }
.step:hover:not(:disabled) { background: #f3f4f6; }
.step:disabled { color: #d1d5db; cursor: default; }
.step:focus-visible { outline: 2px solid #0284c7; }
.cards { display: grid; grid-template-columns: repeat(auto-fit, minmax(190px, 1fr)); gap: 12px; margin-bottom: 16px; }
.c { border: 1px solid #e5e7eb; border-radius: 12px; padding: 14px; display: flex; flex-direction: column; gap: 6px; }
h2 { margin: 0; font-size: 13px; font-weight: 600; color: #4b5563; }
.big { font-size: 26px; font-weight: 700; line-height: 1.1; }
.unit { font-size: 13px; font-weight: 400; color: #4b5563; }
.muted { color: #4b5563; font-size: 13px; }
.perday { margin-bottom: 16px; }
.days { display: grid; grid-template-columns: repeat(auto-fit, minmax(14px, 1fr)); gap: 2px; margin-top: 8px; }
.d { display: flex; flex-direction: column; align-items: center; }
.plot { position: relative; height: 90px; width: 100%; display: flex; align-items: flex-end; justify-content: center; }
.bar { width: 70%; border-radius: 2px 2px 0 0; }
.tgt { position: absolute; left: 0; right: 0; border-top: 2px solid #374151; }
.n { font-size: 10px; color: #6b7280; }
.legend { display: flex; flex-wrap: wrap; gap: 4px 16px; font-size: 13px; color: #4b5563; list-style: none; margin: 8px 0 0; padding: 0; }
.legend li { display: inline-flex; align-items: center; gap: 6px; }
.legend i { width: 10px; height: 10px; border-radius: 2px; display: inline-block; }
.legend i.t { height: 0; border-top: 2px solid #374151; border-radius: 0; }
.table-wrap { overflow-x: auto; }
table { width: 100%; border-collapse: collapse; font-size: 13px; }
th { text-align: left; font-weight: 600; color: #4b5563; padding: 6px 8px; border-bottom: 1px solid #e5e7eb; white-space: nowrap; }
td { padding: 8px; border-bottom: 1px solid #f3f4f6; white-space: nowrap; }
.dot { display: inline-block; width: 9px; height: 9px; border-radius: 50%; margin-right: 4px; }
</style>
