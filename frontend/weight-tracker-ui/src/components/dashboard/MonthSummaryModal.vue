<template>
  <Modal :title="title" max-width="760px" @close="$emit('close')">
    <template #header="{ titleId, close }">
      <div class="nav">
        <div class="nav-l">
          <button type="button" class="step" aria-label="Previous month" @click="$emit('change', addMonths(month, -1))">‹</button>
          <h1 :id="titleId" class="t">{{ title }}</h1>
          <button type="button" class="step" aria-label="Next month" :disabled="month >= monthOf(app.today)" @click="$emit('change', addMonths(month, 1))">›</button>
        </div>
        <button type="button" class="x" aria-label="Close" @click="close()">×</button>
      </div>
    </template>

    <ErrorNote v-if="error" :message="error" :retry="reload" />
    <p v-else-if="loading && !data" class="muted" aria-busy="true">Loading…</p>

    <div v-if="data" class="content">
      <p v-if="f.loggedDays === 0" class="muted">Nothing logged in {{ title }}.</p>

      <div class="cards">
        <section class="c">
          <h2>Calories</h2>
          <div class="big" :style="{ color: f.status === 'over' ? OVER : f.status === 'under' ? UNDER : undefined }">{{ kcal(f.avgCalories) }} <span class="unit">kcal average</span></div>
          <TileRow label="Target" :value="kcal(f.avgTarget)" />
          <TileRow label="Days logged" :value="`${f.loggedDays} / ${data.daysInMonth}`" />
          <TileRow label="Cheat days" :value="data.cheatDays" />
        </section>

        <section class="c">
          <h2>Weight</h2>
          <div class="big">{{ weight(f.avgWeightKg, app.unit) }} <span class="unit">{{ app.unit }} average</span></div>
          <TileRow :label="`From ${prevMonthName}`">
            <span v-if="f.weightChangeKg != null" :style="{ color: f.weightChangeKg <= 0 ? UNDER : OVER }">{{ weightChange(f.weightChangeKg, app.unit) }} {{ app.unit }}</span><span v-else>–</span>
          </TileRow>
          <TileRow :label="`1 to ${lastDay} ${monthTitle(month).split(' ')[0].slice(0, 3)}`" :value="`${weight(data.startWeightKg, app.unit)} → ${weight(data.endWeightKg, app.unit)}`" />
          <TileRow label="Weigh-ins" :value="f.weighIns" />
        </section>

        <section class="c">
          <h2>Deficit</h2>
          <div class="big" :style="{ color: f.deficitKcal >= 0 ? UNDER : OVER }">{{ kcal(Math.abs(f.deficitKcal)) }} <span class="unit">kcal {{ f.deficitKcal >= 0 ? 'below' : 'above' }} TDEE</span></div>
          <TileRow>
            <template #label><InfoTip label="Worth about" text="7700 kcal is about 1 kg." /></template>
            {{ Math.abs(f.deficitKg).toFixed(2) }} kg {{ f.deficitKg >= 0 ? 'lost' : 'gained' }}
          </TileRow>
          <TileRow label="Against target">
            <span :style="{ color: f.gap > 0 ? OVER : UNDER }">{{ Math.abs(f.gap) }} {{ f.gap > 0 ? 'over' : 'under' }}</span>
          </TileRow>
          <TileRow>
            <template #label><InfoTip label="Estimated TDEE" text="From the 90 days ending this month." /></template>
            {{ data.tdee ? kcal(data.tdee.kcal) : 'Not enough data' }}
          </TileRow>
        </section>
      </div>

      <section v-if="days.length" class="perday">
        <h2>Calories per day, with each day's target</h2>
        <div class="scroll">
          <div class="days" role="list" :style="{ gridTemplateColumns: `repeat(${days.length}, minmax(0, 1fr))` }">
            <div v-for="d in days" :key="d.date" class="d" role="listitem" :title="d.tip" :aria-label="d.tip">
              <div class="plot">
                <div v-if="d.calories != null" class="bar" :style="{ height: d.pct + '%', background: d.color }" />
                <div v-if="d.target" class="tgt" :style="{ bottom: d.targetPct + '%' }" />
              </div>
              <span class="n">{{ d.n }}</span>
            </div>
          </div>
        </div>
        <ul class="legend">
          <li><i :style="{ background: STATUS_COLORS.under.solid }" />On or under target</li>
          <li><i :style="{ background: STATUS_COLORS.balanced.solid }" />Over, {{ app.settings.averagingWeeks }}-week average ok</li>
          <li><i :style="{ background: STATUS_COLORS.over.solid }" />Over target</li>
          <li><i class="t" />Target</li>
        </ul>
      </section>

      <div class="scroll">
        <table>
          <thead><tr><th class="l">Week</th><th>Average</th><th>Against target</th><th>Deficit</th><th>Weight</th><th>Logged</th></tr></thead>
          <tbody>
            <tr v-for="w in data.weeks" :key="w.monday">
              <td class="l"><span class="dot" :style="{ background: w.figures.status ? STATUS_COLORS[w.figures.status].solid : '#d1d5db' }" /><strong>W{{ w.isoWeek }}</strong> <span class="muted">{{ shortDate(w.monday) }}</span></td>
              <td><strong>{{ kcal(w.figures.avgCalories) }}</strong></td>
              <td :style="{ color: w.figures.loggedDays ? (w.figures.gap > 0 ? OVER : UNDER) : undefined }">{{ w.figures.loggedDays ? signed(w.figures.gap) : '–' }}</td>
              <td>{{ w.figures.loggedDays ? kcal(w.figures.deficitKcal) : '–' }}</td>
              <td>{{ weight(w.figures.avgWeightKg, app.unit) }} <span class="muted">{{ weightChange(w.figures.weightChangeKg, app.unit) }}</span></td>
              <td>{{ w.figures.loggedDays }} / 7</td>
            </tr>
          </tbody>
        </table>
      </div>
      <p class="muted">Weeks are full weeks, as in the calendar. Totals above count {{ monthTitle(month).split(' ')[0] }} only.</p>
    </div>
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
const UNDER = '#007a55'
const OVER = '#c70036'
const lastDay = computed(() => Number(lastDayOfMonth(props.month).slice(8)))
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
.nav { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 20px 24px 0; }
.nav-l { display: flex; align-items: center; gap: 8px; }
.t { margin: 0; font-size: 18px; font-weight: 700; min-width: 130px; text-align: center; white-space: nowrap; }
.step { width: 44px; height: 44px; border: 1px solid #d1d5db; border-radius: 8px; background: #fff; font-size: 18px; color: #374151; }
.step:hover:not(:disabled) { background: #f3f4f6; }
.step:disabled { color: #d1d5db; cursor: default; }
.x { all: unset; cursor: pointer; width: 44px; height: 44px; background: #f3f4f6; border-radius: 50%; font-size: 18px; color: #374151; display: inline-flex; align-items: center; justify-content: center; }
.x:hover { background: #e5e7eb; }
.step:focus-visible, .x:focus-visible { outline: 2px solid #0284c7; outline-offset: 2px; }
.content { display: flex; flex-direction: column; gap: 18px; color: #1f2937; font-size: 14px; line-height: 1.45; font-variant-numeric: tabular-nums; }
.cards { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 210px), 1fr)); gap: 20px 28px; }
.c { display: flex; flex-direction: column; gap: 6px; }
h2 { margin: 0; font-size: 13px; font-weight: 600; color: #4b5563; }
.big { font-size: 28px; font-weight: 700; line-height: 1.1; }
.unit { font-size: 14px; font-weight: 400; color: #4b5563; }
.muted { color: #4b5563; font-weight: 400; }
.perday { display: flex; flex-direction: column; gap: 8px; }
.scroll { overflow-x: auto; }
.days { min-width: 560px; display: grid; }
.d { display: flex; flex-direction: column; align-items: center; gap: 4px; }
.plot { position: relative; width: 100%; height: 126px; border-bottom: 1px solid #6b7280; }
.bar { position: absolute; bottom: 0; left: 50%; width: 14px; margin-left: -7px; border-radius: 4px 4px 0 0; }
.tgt { position: absolute; left: 0; right: 0; height: 2px; background: #1f2937; }
.n { font-size: 12px; color: #4b5563; }
.legend { display: flex; flex-wrap: wrap; gap: 4px 16px; font-size: 13px; color: #4b5563; list-style: none; margin: 0; padding: 0; }
.legend li { display: inline-flex; align-items: center; gap: 6px; }
.legend i { width: 12px; height: 12px; border-radius: 3px; display: inline-block; }
.legend i.t { width: 14px; height: 2px; border-radius: 0; background: #1f2937; }
table { width: 100%; min-width: 560px; border-collapse: collapse; text-align: right; }
th { padding: 8px 0; font-weight: 600; font-size: 13px; color: #4b5563; }
td { padding: 9px 0; border-top: 1px solid #e5e7eb; }
.l { text-align: left; }
.dot { display: inline-block; width: 9px; height: 9px; border-radius: 50%; margin-right: 6px; }
</style>
