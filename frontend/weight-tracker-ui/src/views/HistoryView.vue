<template>
  <main class="page">
    <div class="top">
      <h1>History</h1>
      <div class="jump">
        <label for="jump">Go to</label>
        <input id="jump" type="month" :value="jumpValue" :max="monthOf(app.today)" @change="jumpTo(($event.target as HTMLInputElement).value)" />
      </div>
    </div>

    <ErrorNote v-if="error" :message="error" :retry="reload" />

    <section class="card" aria-labelledby="wt">
      <div class="head">
        <h2 id="wt">Weight</h2>
        <div class="seg" role="group" aria-label="Range">
          <button v-for="r in ranges" :key="r.label" type="button" :aria-pressed="range === r.days" @click="range = r.days">{{ r.label }}</button>
        </div>
      </div>
      <p v-if="!loadingWeights && !chart" class="muted">No weigh-ins in this range.</p>
      <div v-else-if="chart" class="scroll">
        <svg :viewBox="`0 0 ${chart.w} ${chart.h}`" role="img" aria-label="Weigh-ins and 7-day average" font-family="ui-sans-serif, system-ui, sans-serif" font-size="12">
          <rect v-if="chart.goal" :x="chart.goal.x" y="12" :width="chart.goal.w" :height="chart.h - 52" fill="#e0f2fe" />
          <path :d="chart.grid" stroke="#e5e7eb" fill="none" />
          <text v-for="t in chart.yTicks" :key="t.kg" x="36" :y="t.y + 4" text-anchor="end" fill="#4b5563">{{ t.label }}</text>
          <text v-for="t in chart.xTicks" :key="t.date" :x="t.x" :y="chart.h - 8" :text-anchor="t.anchor" fill="#4b5563">{{ t.label }}</text>
          <circle v-for="(d, i) in chart.dots" :key="i" :cx="d.x" :cy="d.y" r="2.5" fill="#9ca3af" />
          <path :d="chart.line" stroke="#0284c7" stroke-width="2" stroke-linejoin="round" fill="none" />
        </svg>
        <ul class="legend">
          <li><i class="dot" />Weigh-in</li><li><i class="ln" />7-day average</li><li v-if="chart.goal"><i class="gl" />Current goal</li>
        </ul>
      </div>
    </section>

    <div v-if="loading && !data" class="muted" aria-busy="true">Loading…</div>

    <section v-for="w in weeks" :key="w.monday" class="week">
      <button type="button" class="wk-head" :aria-expanded="isOpen(w.monday)" @click="toggle(w.monday)">
        <span class="wk-name"><span class="dot" :style="{ background: w.figures.status ? STATUS_COLORS[w.figures.status].solid : '#d1d5db' }" /> W{{ w.isoWeek }} <span class="muted">{{ shortDate(w.monday) }} – {{ shortDate(addDays(w.monday, 6)) }}</span></span>
        <span><strong>{{ kcal(w.figures.avgCalories) }}</strong> <span class="muted">kcal average</span></span>
        <span v-if="w.figures.loggedDays" :style="{ color: w.figures.gap > 0 ? '#c70036' : '#007a55' }">{{ signed(w.figures.gap) }} vs target</span><span v-else class="muted">no log</span>
        <span v-if="w.figures.avgWeightKg != null"><strong>{{ weight(w.figures.avgWeightKg, app.unit) }}</strong> <span class="muted">{{ app.unit }} {{ weightChange(w.figures.weightChangeKg, app.unit) }}</span></span>
        <span class="muted">{{ w.figures.loggedDays }} logged</span>
        <span aria-hidden="true">{{ isOpen(w.monday) ? '▾' : '▸' }}</span>
      </button>
      <div v-if="isOpen(w.monday)" class="table-wrap">
        <table>
          <thead><tr><th>Day</th><th>Calories</th><th>Against target</th><th>Weight</th><th>Note</th><th><span class="sr">Actions</span></th></tr></thead>
          <tbody>
            <tr v-for="d in w.days" :key="d.date">
              <td>{{ relativeDay(d.date, app.today) }}</td>
              <td>{{ d.calories == null ? (d.planned != null ? `plan ${d.planned}` : '–') : d.calories }}<span v-if="d.isCheatDay" title="Cheat day"> ★</span></td>
              <td :style="{ color: d.calories != null && d.target != null ? (d.calories > d.target ? '#c70036' : '#007a55') : undefined }">{{ d.calories != null && d.target != null ? signed(d.calories - d.target) : '' }}</td>
              <td>{{ d.weightKg == null ? '–' : weight(d.weightKg, app.unit) }}</td>
              <td class="note">{{ notes.get(d.date) ?? '' }}</td>
              <td><button type="button" class="link" :aria-label="`${d.calories != null || d.weightKg != null ? 'Edit' : 'Log'} ${longDate(d.date)}`" @click="sheet.open(d.date)">{{ d.calories != null || d.weightKg != null ? 'Edit' : 'Log' }}</button></td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>

    <button type="button" class="btn-secondary more" @click="older += 8">Show older weeks</button>
  </main>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import ErrorNote from '../components/ui/ErrorNote.vue'
import { useAppStore } from '../stores/app'
import { useLogSheetStore } from '../stores/logSheet'
import { useAsyncData } from '../composables/useAsyncData'
import * as statsApi from '../api/stats'
import * as logApi from '../api/dailyLog'
import { addDays, mondayOf, monthOf, daysBetween } from '../utils/dates'
import { kcal, longDate, relativeDay, shortDate, signed, STATUS_COLORS, weight, weightChange } from '../utils/format'
import { displayWeight } from '../utils/units'
import { niceTicks } from '../utils/chart'

const app = useAppStore()
const sheet = useLogSheetStore()

const ranges = [{ label: '3 months', days: 91 }, { label: '6 months', days: 183 }, { label: 'All', days: 3650 }]
const range = ref(183)
const older = ref(0)             // extra weeks loaded with "Show older weeks"
const jumpMonth = ref<string | null>(null)
const opened = ref<Set<string>>(new Set())
const closed = ref<Set<string>>(new Set())

const WEEKS_SHOWN = 8
const oldest = computed(() => {
  let from = addDays(mondayOf(app.today), -7 * (WEEKS_SHOWN - 1 + older.value))
  if (jumpMonth.value && `${jumpMonth.value}-01` < from) from = mondayOf(`${jumpMonth.value}-01`)
  return from
})
const jumpValue = computed(() => jumpMonth.value ?? monthOf(app.today))
function jumpTo(v: string) { if (v) jumpMonth.value = v }

const { data, loading, error, reload } = useAsyncData(async () => {
  const to = addDays(mondayOf(app.today), 6)
  const [calendar, logs] = await Promise.all([statsApi.getCalendar(oldest.value, to), logApi.getAll({ from: oldest.value, to })])
  return { calendar, logs }
}, [oldest])

const weightsFrom = computed(() => addDays(app.today, -range.value))
const { data: weights, loading: loadingWeights } = useAsyncData(() => statsApi.getWeights(weightsFrom.value), [weightsFrom])
const { data: dash } = useAsyncData(() => statsApi.getDashboard())

const notes = computed(() => new Map((data.value?.logs ?? []).filter((l) => l.notes).map((l) => [l.date, l.notes!])))

const weeks = computed(() => {
  if (!data.value) return []
  const days = new Map(data.value.calendar.days.map((d) => [d.date, d]))
  return [...data.value.calendar.weeks]
    .sort((a, b) => b.monday.localeCompare(a.monday))
    .map((w) => ({ ...w, days: Array.from({ length: 7 }, (_, i) => days.get(addDays(w.monday, i))!).filter(Boolean).reverse() }))
})

// Weeks are collapsed except the latest two; the user's own toggles win.
function isOpen(monday: string) {
  if (opened.value.has(monday)) return true
  if (closed.value.has(monday)) return false
  const index = weeks.value.findIndex((w) => w.monday === monday)
  return index >= 0 && index < 2
}
function toggle(monday: string) {
  const open = isOpen(monday)
  const o = new Set(opened.value), c = new Set(closed.value)
  if (open) { o.delete(monday); c.add(monday) } else { c.delete(monday); o.add(monday) }
  opened.value = o; closed.value = c
}

const chart = computed(() => {
  const pts = weights.value ?? []
  if (pts.length === 0) return null
  const w = 900, h = 240, left = 44, right = 20, top = 12, bottom = 40
  const first = pts[0].date
  const last = app.today
  const span = Math.max(daysBetween(first, last), 1)
  const vals = pts.flatMap((p) => [displayWeight(p.kg, app.unit), displayWeight(p.avg7, app.unit)])
  const ticks = niceTicks(Math.min(...vals), Math.max(...vals))
  const min = ticks[0], max = ticks[ticks.length - 1]
  const x = (d: string) => left + (daysBetween(first, d) / span) * (w - left - right)
  const y = (kg: number) => top + (1 - (kg - min) / Math.max(max - min, 0.001)) * (h - top - bottom)
  const f = (n: number) => Math.round(n * 10) / 10
  const goal = dash.value?.goal
  const gStart = goal ? Math.max(daysBetween(first, goal.startDate), 0) : 0
  const gEnd = goal ? Math.min(daysBetween(first, goal.targetDate ?? last), span) : 0
  return {
    w, h,
    grid: ticks.map((t) => `M${left} ${f(y(t))}H${w - right}`).join(''),
    yTicks: ticks.map((t) => ({ kg: t, y: f(y(t)), label: String(t) })),
    xTicks: [
      { date: first, x: left, label: shortDate(first, true), anchor: 'start' },
      { date: last, x: w - right, label: shortDate(last, true), anchor: 'end' },
    ],
    dots: pts.map((p) => ({ x: f(x(p.date)), y: f(y(displayWeight(p.kg, app.unit))) })),
    line: pts.map((p, i) => `${i ? 'L' : 'M'}${f(x(p.date))} ${f(y(displayWeight(p.avg7, app.unit)))}`).join(''),
    goal: goal && gEnd > gStart ? { x: f(left + (gStart / span) * (w - left - right)), w: f(((gEnd - gStart) / span) * (w - left - right)) } : null,
  }
})
</script>

<style scoped>
.page { max-width: 960px; margin: 0 auto; padding: 20px 16px 24px; display: flex; flex-direction: column; gap: 16px; }
.top { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 8px 16px; }
h1 { margin: 0; font-size: 20px; }
.jump { display: flex; align-items: center; gap: 8px; }
.jump input { min-height: 44px; border: 1px solid #d1d5db; border-radius: 8px; padding: 0 10px; }
.card, .week { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; }
.card { padding: 18px; display: flex; flex-direction: column; gap: 12px; }
.head { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 8px; }
h2 { margin: 0; font-size: 16px; }
.seg { display: flex; gap: 4px; }
.seg button { min-height: 44px; padding: 0 14px; border: 1px solid #d1d5db; background: #fff; border-radius: 8px; cursor: pointer; }
.seg button[aria-pressed='true'] { background: #0369a1; color: #fff; border-color: #0369a1; }
.scroll { overflow-x: auto; }
svg { display: block; width: 100%; min-width: 600px; height: auto; }
.legend { display: flex; flex-wrap: wrap; gap: 4px 18px; font-size: 13px; color: #4b5563; list-style: none; margin: 8px 0 0; padding: 0; }
.legend li { display: inline-flex; align-items: center; gap: 6px; }
.dot { display: inline-block; width: 8px; height: 8px; border-radius: 50%; background: #9ca3af; }
.ln { width: 18px; height: 2px; background: #0284c7; display: inline-block; }
.gl { width: 14px; height: 10px; background: #e0f2fe; display: inline-block; }
.muted { color: #4b5563; font-size: 13px; }
.wk-head { all: unset; box-sizing: border-box; width: 100%; cursor: pointer; display: grid; grid-template-columns: minmax(150px, 1.4fr) repeat(4, minmax(0, 1fr)) 20px; gap: 8px 12px; align-items: center; padding: 12px 16px; min-height: 48px; }
.wk-head:focus-visible { outline: 2px solid #0284c7; outline-offset: -2px; border-radius: 12px; }
.wk-name { font-weight: 600; }
.table-wrap { overflow-x: auto; padding: 0 16px 12px; }
table { width: 100%; border-collapse: collapse; font-size: 14px; }
th { text-align: left; font-size: 13px; font-weight: 600; color: #4b5563; padding: 6px 8px; border-bottom: 1px solid #e5e7eb; white-space: nowrap; }
td { padding: 8px; border-bottom: 1px solid #f3f4f6; }
td.note { max-width: 220px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; color: #4b5563; }
.link { all: unset; cursor: pointer; color: #0369a1; font-weight: 600; min-height: 44px; display: inline-flex; align-items: center; padding: 0 8px; }
.link:focus-visible { outline: 2px solid #0284c7; }
.sr { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); }
.more { align-self: center; min-height: 44px; }
@media (max-width: 639px) {
  .wk-head { grid-template-columns: 1fr 20px; }
  .wk-head > span:not(.wk-name):not([aria-hidden]) { grid-column: 1 / -1; }
}
</style>
