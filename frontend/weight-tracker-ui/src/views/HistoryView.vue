<template>
  <main class="page">
    <div class="top">
      <h1>History</h1>
      <div class="jump">
        <label for="jump">Go to</label>
        <input id="jump" type="month" :value="jumpMonth ?? monthOf(app.today)" :max="monthOf(app.today)" @change="jumpTo(($event.target as HTMLInputElement).value)" />
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

    <section v-for="w in weeks" :id="`w-${w.monday}`" :key="w.monday" class="week">
      <button type="button" class="wk-head" :style="{ background: tint(w.figures.status) }" :aria-expanded="isOpen(w.monday)" @click="toggle(w.monday)">
        <span class="sw" :style="{ background: w.figures.status ? STATUS_COLORS[w.figures.status].solid : '#d1d5db' }" />
        <span class="c1"><strong>W{{ w.isoWeek }}</strong> <span class="range">{{ shortDate(w.monday) }} to {{ addDays(w.monday, 6) >= app.today && w.monday <= app.today ? 'today' : shortDate(addDays(w.monday, 6)) }}</span></span>
        <span class="c2"><strong>{{ kcal(w.figures.avgCalories) }}</strong> kcal average</span>
        <span class="c3">{{ w.figures.loggedDays ? `${Math.abs(w.figures.gap)} ${w.figures.gap > 0 ? 'over' : 'under'}` : 'no log' }}</span>
        <span class="c4"><template v-if="w.figures.avgWeightKg != null"><strong>{{ weight(w.figures.avgWeightKg, app.unit) }}</strong> {{ app.unit }} {{ weightChange(w.figures.weightChangeKg, app.unit) }}</template></span>
        <span class="c5">{{ w.figures.loggedDays }} logged</span>
        <span class="arrow" aria-hidden="true">{{ isOpen(w.monday) ? '▾' : '▸' }}</span>
      </button>
      <div v-if="isOpen(w.monday)" class="table-wrap">
        <table>
          <thead><tr><th class="w1">Day</th><th class="w1">Calories</th><th class="w2 wide">Against target</th><th class="w3">Weight</th><th class="wide">Note</th><th class="w4"><span class="sr">Actions</span></th></tr></thead>
          <tbody>
            <tr v-for="d in w.days" :key="d.date">
              <td class="day">{{ dayName(d.date) }}<span v-if="remark(d)" class="under">{{ remark(d) }}</span></td>
              <td><span v-if="d.calories != null" class="chip" :style="chipStyle(d)">{{ d.calories }}</span><span v-else-if="d.planned != null" class="plan">plan {{ d.planned }}</span><span v-else class="plan">{{ d.date < app.today ? 'no log' : '' }}</span></td>
              <td class="gray wide">{{ d.calories != null && d.target != null ? (d.calories <= d.target ? `${d.target - d.calories} under` : `${d.calories - d.target} over`) : '' }}</td>
              <td>{{ d.weightKg == null ? '' : weight(d.weightKg, app.unit) + ' ' + app.unit }}</td>
              <td class="gray wide">{{ remark(d) }}</td>
              <td class="r"><button type="button" class="link" :aria-label="`${hasData(d) ? 'Edit' : 'Add'} ${longDate(d.date)}`" @click="sheet.open(d.date)">{{ hasData(d) ? 'Edit' : 'Add' }}</button></td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>

    <button v-if="hasOlder" type="button" class="more" @click="older += 8">Show older weeks</button>
  </main>
</template>

<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import ErrorNote from '../components/ui/ErrorNote.vue'
import { useAppStore } from '../stores/app'
import { useLogSheetStore } from '../stores/logSheet'
import { useAsyncData } from '../composables/useAsyncData'
import * as statsApi from '../api/stats'
import { addDays, daysBetween, lastDayOfMonth, mondayOf, monthOf } from '../utils/dates'
import { CALENDAR_COLORS, kcal, longDate, PENDING_COLORS, shortDate, STATUS_COLORS, weekdayShort, weight, weightChange } from '../utils/format'
import type { CalendarDay, Status } from '../types'
import { toUnit } from '../utils/units'
import { niceTicks } from '../utils/chart'

const app = useAppStore()
const sheet = useLogSheetStore()

const ranges = [{ label: '30 days', days: 30 }, { label: '90 days', days: 90 }, { label: 'All', days: 3650 }]
const range = ref(90)
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

const { data, loading, error, reload } = useAsyncData(() => statsApi.getCalendar(oldest.value, addDays(mondayOf(app.today), 6)), [oldest])

// The month's last week, opened and brought into view once its weeks are loaded.
async function jumpTo(v: string) {
  if (!v) return
  jumpMonth.value = v
  await nextTick()
  if (loading.value) await new Promise<void>((done) => { const stop = watch(loading, (l) => { if (!l) { stop(); done() } }) })
  await nextTick()
  const last = mondayOf(lastDayOfMonth(v))
  const monday = last > mondayOf(app.today) ? mondayOf(app.today) : last
  if (!isOpen(monday)) toggle(monday)
  await nextTick()
  document.getElementById(`w-${monday}`)?.scrollIntoView({ behavior: 'smooth', block: 'start' })
}

const weightsFrom = computed(() => addDays(app.today, -range.value))
const { data: weights, loading: loadingWeights } = useAsyncData(() => statsApi.getWeights(weightsFrom.value), [weightsFrom])
const { data: dash } = useAsyncData(() => statsApi.getDashboard())
const hasOlder = computed(() => dash.value?.firstLog != null && dash.value.firstLog < oldest.value)

const tint = (s: Status | null) => (s === 'under' ? '#ecfdf5' : s === 'balanced' ? '#fffbeb' : s === 'over' ? '#fff1f2' : '#ffffff')
const dayName = (d: string) => `${weekdayShort(d)} ${shortDate(d)}`
const hasData = (d: CalendarDay) => d.calories != null || d.weightKg != null || d.isCheatDay || !!d.notes
const remark = (d: CalendarDay) => [d.isCheatDay ? 'cheat day' : '', d.notes ?? ''].filter(Boolean).join(', ')
function chipStyle(d: CalendarDay) {
  if (!d.status) {
    const [bg, fg] = d.date === app.today ? PENDING_COLORS.today : PENDING_COLORS.ahead
    return { background: bg, color: fg }
  }
  const [bg, fg] = CALENDAR_COLORS[d.inGoal ? 'day' : 'dayOut'][d.status]
  return { background: bg, color: fg }
}

const weeks = computed(() => {
  if (!data.value) return []
  const days = new Map(data.value.days.map((d) => [d.date, d]))
  return [...data.value.weeks]
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
  const vals = pts.flatMap((p) => [toUnit(p.kg, app.unit), toUnit(p.avg7, app.unit)])
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
    dots: pts.map((p) => ({ x: f(x(p.date)), y: f(y(toUnit(p.kg, app.unit))) })),
    line: pts.map((p, i) => `${i ? 'L' : 'M'}${f(x(p.date))} ${f(y(toUnit(p.avg7, app.unit)))}`).join(''),
    goal: goal && gEnd > gStart ? { x: f(left + (gStart / span) * (w - left - right)), w: f(((gEnd - gStart) / span) * (w - left - right)) } : null,
  }
})
</script>

<style scoped>
.page { max-width: 960px; margin: 0 auto; padding: 20px 16px 24px; display: flex; flex-direction: column; gap: 16px; color: #1f2937; font-variant-numeric: tabular-nums; }
.top { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 8px 16px; }
h1 { margin: 0; font-size: 20px; font-weight: 700; }
.jump { display: flex; align-items: center; gap: 8px; }
.jump label { color: #4b5563; }
.jump input { box-sizing: border-box; height: 44px; border: 1px solid #9ca3af; border-radius: 8px; padding: 0 12px; font: inherit; background: #fff; color: #1f2937; }
.card, .week { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; }
.week { overflow: hidden; }
.card { padding: 18px; display: flex; flex-direction: column; gap: 12px; }
.head { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 8px 16px; }
h2 { margin: 0; font-size: 16px; font-weight: 600; }
.seg { display: flex; gap: 4px; }
.seg button { min-height: 44px; padding: 0 14px; border: 1px solid #9ca3af; background: #fff; color: #374151; border-radius: 8px; font: inherit; font-weight: 600; }
.seg button[aria-pressed='true'] { background: #0369a1; color: #fff; border-color: #0369a1; }
.scroll { overflow-x: auto; }
svg { display: block; width: 100%; min-width: 600px; height: auto; }
.legend { display: flex; flex-wrap: wrap; gap: 4px 18px; font-size: 13px; color: #4b5563; list-style: none; margin: 0; padding: 0; }
.legend li { display: inline-flex; align-items: center; gap: 6px; }
.dot { width: 7px; height: 7px; border-radius: 50%; background: #9ca3af; display: inline-block; }
.ln { width: 18px; height: 2px; background: #0284c7; display: inline-block; }
.gl { width: 14px; height: 12px; background: #e0f2fe; display: inline-block; }
.muted { color: #4b5563; font-size: 13px; }
.wk-head { width: 100%; min-height: 60px; border: 0; color: #1f2937; font: inherit; padding: 10px 18px; display: flex; flex-wrap: wrap; align-items: center; gap: 4px 20px; text-align: left; }
.wk-head:focus-visible { outline: 2px solid #0284c7; outline-offset: -2px; }
.sw { width: 12px; height: 12px; border-radius: 3px; flex: none; }
.c1 { flex: 1 1 170px; } .c2 { flex: 0 1 170px; white-space: nowrap; } .c3 { flex: 0 1 110px; } .c4 { flex: 0 1 140px; white-space: nowrap; } .c5 { flex: 0 1 90px; white-space: nowrap; }
.range { color: #374151; }
.arrow { width: 16px; text-align: center; }
.table-wrap { overflow-x: auto; padding: 0 18px 8px; }
table { width: 100%; min-width: 620px; border-collapse: collapse; text-align: left; table-layout: fixed; }
th { text-align: left; white-space: nowrap; }
th { padding: 8px 0; font-weight: 600; font-size: 13px; color: #4b5563; }
.w1 { width: 120px; } .w2 { width: 130px; } .w3 { width: 100px; } .w4 { width: 70px; }
td { padding: 4px 0; border-top: 1px solid #f3f4f6; }
td.day { font-weight: 600; }
td.gray, .plan { color: #4b5563; }
td.r { text-align: right; }
.chip { display: inline-block; min-width: 44px; text-align: center; padding: 3px 8px; border-radius: 6px; font-weight: 600; }
.link { display: inline-flex; align-items: center; min-height: 44px; padding: 0 6px; background: none; border: 0; font: inherit; font-weight: 600; color: #0369a1; }
.link:focus-visible { outline: 2px solid #0284c7; }
.sr { position: absolute; left: -9999px; }
.under { display: none; font-weight: 400; font-size: 13px; color: #4b5563; }
.week { scroll-margin-top: 8px; }
/* On a phone everything fits without scrolling sideways: the note goes under the day. */
@media (max-width: 639px) {
  table { min-width: 0; }
  .wide { display: none; }
  .under { display: block; overflow-wrap: anywhere; }
  .w1 { width: auto; } .w1 + .w1 { width: 84px; } .w3 { width: 76px; } .w4 { width: 46px; }
  td.day { white-space: nowrap; font-size: 15px; }
  .under { white-space: normal; }
  .table-wrap { padding: 0 14px 8px; }
}
.more { align-self: center; min-height: 44px; padding: 0 20px; border: 1px solid #9ca3af; border-radius: 8px; background: #fff; color: #1f2937; font: inherit; font-weight: 600; }
</style>
