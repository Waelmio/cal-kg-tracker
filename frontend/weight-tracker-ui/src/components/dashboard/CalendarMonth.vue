<template>
  <section class="cal" aria-labelledby="cal-title">
    <header class="head">
      <div class="title-row">
        <button type="button" class="nav" aria-label="Previous month" @click="move(-1)">‹</button>
        <h2 id="cal-title">{{ monthTitle(month) }}</h2>
        <button type="button" class="nav" aria-label="Next month" @click="move(1)">›</button>
      </div>
      <div class="actions">
        <button type="button" class="btn-secondary" :disabled="month === monthOf(app.today)" @click="month = monthOf(app.today)">Today</button>
        <button type="button" class="btn-secondary" @click="$emit('month-summary', month)">Month summary</button>
      </div>
    </header>

    <ErrorNote v-if="error" :message="error" :retry="reload" />

    <div class="grid" role="grid" :aria-busy="loading">
      <div class="h" role="columnheader">Week</div>
      <div v-for="h in ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun']" :key="h" class="h" role="columnheader">{{ h }}</div>

      <template v-for="w in rows" :key="w.monday">
        <div class="wk" role="gridcell" :style="w.style" :title="w.title">
          <span class="wk-top">W{{ w.isoWeek }}{{ w.sofar ? ' so far' : '' }}</span>
          <span class="wk-mid">{{ w.avg }}</span>
          <span class="wk-sub">{{ w.weightText }}</span>
        </div>
        <button
          v-for="d in w.days" :key="d.date" type="button" class="day" role="gridcell"
          :class="{ today: d.date === app.today, other: !d.inMonth, planned: d.planned != null && d.calories == null, loggedAhead: d.plannedLogged }"
          :style="d.style" :aria-label="d.label" @click="sheet.open(d.date)"
        >
          <span class="n">{{ d.n }}<span v-if="d.isCheatDay" class="mark" title="Cheat day"> ★</span><span v-if="d.hasNote" class="mark" title="Has a note"> ✎</span></span>
          <span class="k">{{ d.kcalText }}</span>
          <span class="w">{{ d.weightText }}</span>
        </button>
      </template>
    </div>

    <ul class="legend">
      <li><i :style="{ background: STATUS_COLORS.under.solid }" />On or under target</li>
      <li><i :style="{ background: STATUS_COLORS.balanced.solid }" /><InfoTip :label="`Over, ${app.settings.averagingWeeks}-week average ok`" text="Over target, but fine averaged with the weeks around it." /></li>
      <li><i :style="{ background: STATUS_COLORS.over.solid }" />Over target</li>
      <li><i class="dash" /><InfoTip label="Planned" text="Your budget. Doesn't count until you log it." /></li>
      <li><i class="light" /><InfoTip label="Lighter: outside the goal" text="Outside your current goal." /></li>
    </ul>
  </section>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import ErrorNote from '../ui/ErrorNote.vue'
import InfoTip from '../ui/InfoTip.vue'
import { useAppStore } from '../../stores/app'
import { useLogSheetStore } from '../../stores/logSheet'
import { useAsyncData } from '../../composables/useAsyncData'
import * as statsApi from '../../api/stats'
import { addDays, addMonths, lastDayOfMonth, mondayOf, monthOf } from '../../utils/dates'
import { kcal, longDate, monthTitle, STATUS_COLORS, statusText, weight, weightChange } from '../../utils/format'
import type { CalendarDay, CalendarWeek } from '../../types'

defineEmits<{ 'month-summary': [month: string] }>()

const app = useAppStore()
const sheet = useLogSheetStore()
const month = ref(monthOf(app.today))

const range = computed(() => {
  const from = mondayOf(`${month.value}-01`)
  const to = addDays(mondayOf(lastDayOfMonth(month.value)), 6)
  return { from, to }
})

const { data, loading, error, reload } = useAsyncData(() => statsApi.getCalendar(range.value.from, range.value.to), [month])

function move(by: number) { month.value = addMonths(month.value, by) }
watch(() => app.today, (t, old) => { if (month.value === monthOf(old)) month.value = monthOf(t) })

const byDate = computed(() => new Map((data.value?.days ?? []).map((d) => [d.date, d])))
const byWeek = computed(() => new Map((data.value?.weeks ?? []).map((w) => [w.monday, w])))

function dayStyle(d: CalendarDay | undefined) {
  const status = d?.status
  if (!status) return {}
  const c = STATUS_COLORS[status]
  return d!.inGoal ? { background: c.soft, color: c.text } : { background: tint(c.soft), color: c.text, opacity: 0.8 }
}
const tint = (hex: string) => hex   // "lighter" is expressed with opacity

function weekStyle(w: CalendarWeek | undefined) {
  const status = w?.figures.status
  if (!status) return { background: '#f3f4f6', color: '#4b5563' }
  const c = STATUS_COLORS[status]
  // White text on green and yellow is low contrast, as before. Kept on request: dark text is a one-line change.
  return w!.figures.inGoal ? { background: c.solid, color: '#ffffff' } : { background: c.light, color: c.text }
}

const rows = computed(() => {
  const out = []
  for (let monday = range.value.from; monday <= range.value.to; monday = addDays(monday, 7)) {
    const w = byWeek.value.get(monday)
    const f = w?.figures
    const days = Array.from({ length: 7 }, (_, i) => {
      const date = addDays(monday, i)
      const d = byDate.value.get(date)
      const inMonth = monthOf(date) === month.value
      const shown = d?.calories != null ? d.calories : d?.planned
      return {
        date, n: Number(date.slice(8)), inMonth,
        calories: d?.calories ?? null, planned: d?.planned ?? null, plannedLogged: d?.plannedLogged ?? false,
        isCheatDay: d?.isCheatDay ?? false, hasNote: d?.hasNote ?? false,
        kcalText: shown == null ? '' : kcal(shown),
        weightText: d?.weightKg == null ? '' : weight(d.weightKg, app.unit),
        style: dayStyle(d),
        label: [
          longDate(date),
          d?.calories != null ? `${d.calories} kcal, ${statusText(d.status ?? null).toLowerCase()}` : d?.planned != null ? `planned ${d.planned} kcal` : 'not logged',
          d?.weightKg != null ? weight(d.weightKg, app.unit, true) : '',
        ].filter(Boolean).join(', '),
      }
    })
    out.push({
      monday, isoWeek: w?.isoWeek ?? 0,
      sofar: addDays(monday, 6) >= app.today && monday <= app.today,
      avg: f?.avgCalories == null ? '–' : kcal(f.avgCalories),
      weightText: f?.avgWeightKg == null ? '' : `${weight(f.avgWeightKg, app.unit)}${f.weightChangeKg != null ? ' ' + weightChange(f.weightChangeKg, app.unit) : ''}`,
      title: f?.avgCalories == null ? 'No logged days' : `Average ${f.avgCalories}, target ${f.avgTarget ?? '–'}`,
      style: weekStyle(w),
      days,
    })
  }
  return out
})
</script>

<style scoped>
.cal { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 18px; display: flex; flex-direction: column; gap: 12px; }
.head { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 8px 16px; }
.title-row { display: flex; align-items: center; gap: 4px; }
h2 { margin: 0; font-size: 16px; font-weight: 600; min-width: 140px; text-align: center; }
.nav { all: unset; cursor: pointer; width: 44px; height: 44px; display: grid; place-items: center; font-size: 22px; border-radius: 8px; }
.nav:hover { background: #f3f4f6; }
.actions { display: flex; gap: 8px; }
.actions button { min-height: 44px; }
.grid { display: grid; grid-template-columns: 64px repeat(7, minmax(0, 1fr)); gap: 3px; }
.h { font-size: 12px; font-weight: 600; color: #4b5563; text-align: center; padding: 2px 0; }
.wk { border-radius: 8px; padding: 4px; display: flex; flex-direction: column; align-items: center; justify-content: center; text-align: center; min-height: 64px; font-size: 11px; }
.wk-top { font-weight: 600; }
.wk-mid { font-size: 15px; font-weight: 700; }
.wk-sub { font-size: 11px; }
.day { all: unset; box-sizing: border-box; cursor: pointer; border-radius: 8px; min-height: 64px; padding: 4px 5px; display: flex; flex-direction: column; background: #f9fafb; color: #6b7280; border: 1px solid transparent; }
.day:hover { border-color: #9ca3af; }
.day:focus-visible { outline: 2px solid #0284c7; outline-offset: 1px; }
.day.today { border: 2px solid #0369a1; }
.day.other { opacity: 0.45; }
.day.planned { border: 1px dashed #9ca3af; color: #4b5563; background: #fff; }
.day.loggedAhead { border-style: solid; }
.n { font-size: 11px; }
.k { font-size: 14px; font-weight: 700; }
.w { font-size: 10px; margin-top: auto; opacity: 0.85; }
.mark { font-size: 10px; }
.legend { display: flex; flex-wrap: wrap; gap: 4px 16px; font-size: 13px; color: #4b5563; list-style: none; margin: 0; padding: 0; }
.legend li { display: inline-flex; align-items: center; gap: 6px; }
.legend i { width: 10px; height: 10px; border-radius: 2px; display: inline-block; }
.legend i.dash { background: #fff; border: 1px dashed #6b7280; }
.legend i.light { background: linear-gradient(90deg, #a4f4cf 50%, #ffccd3 50%); }
@media (max-width: 639px) {
  .grid { grid-template-columns: 44px repeat(7, minmax(0, 1fr)); gap: 2px; }
  .wk-sub, .w { display: none; }
  .k { font-size: 11px; }
  .wk-mid { font-size: 12px; }
}
</style>
