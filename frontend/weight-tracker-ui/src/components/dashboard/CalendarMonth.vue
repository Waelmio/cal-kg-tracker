<template>
  <section class="cal" aria-labelledby="cal-title">
    <header class="head">
      <div class="title-row">
        <button type="button" class="nav" aria-label="Previous month" @click="move(-1)">‹</button>
        <h2 id="cal-title" :style="{ color: titleColor }">{{ monthTitle(month) }}</h2>
        <button type="button" class="nav" aria-label="Next month" @click="move(1)">›</button>
      </div>
      <div class="actions">
        <button type="button" :class="{ away: month !== monthOf(app.today) }" :style="todayStyle" :title="todayTitle" @click="month = monthOf(app.today)">Today</button>
        <button type="button" :style="monthStyle" :title="monthTip" @click="$emit('month-summary', month)">Month summary</button>
      </div>
    </header>

    <ErrorNote v-if="error && !data" :message="error" :retry="() => store.show(month)" />

    <div class="grid" :aria-busy="!data">
      <div v-for="h in ['Week avg', 'Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa', 'Su']" :key="h" class="h">{{ h }}</div>

      <template v-for="w in rows" :key="w.monday">
        <div class="cell wk" :style="w.style" :title="w.title">
          <span class="top">{{ w.top }}</span>
          <span class="mid">{{ w.avg }}</span>
          <span class="sub">{{ w.weightText }}</span>
        </div>
        <button
          v-for="d in w.days" :key="d.date" type="button" class="cell day"
          :style="d.style" :title="d.title" :aria-label="d.label" @click="sheet.open(d.date)"
        >
          <span class="top" :style="{ fontSize: d.inMonth ? '14px' : '12px' }">{{ d.top }}</span>
          <span class="mid">{{ d.mid }}</span>
          <span class="sub">{{ d.sub }}</span>
        </button>
      </template>
    </div>

    <ul class="legend">
      <li><i :style="{ background: STATUS_COLORS.under.solid }" />On or under target</li>
      <li><i :style="{ background: STATUS_COLORS.balanced.solid }" /><InfoTip :label="`Over, ${app.settings.averagingWeeks}-week average ok`" text="Over target, but fine averaged with the weeks around it." /></li>
      <li><i :style="{ background: STATUS_COLORS.over.solid }" />Over target</li>
      <li><i class="dash" /><InfoTip label="Planned" text="Your budget. Doesn't count until you log it." /></li>
      <li><i class="ahead" /><InfoTip label="Logged ahead" text="A future day you already logged. Counts when the day comes." /></li>
      <li><i class="light" /><InfoTip label="Lighter: outside the goal" text="Outside your current goal." /></li>
    </ul>
  </section>
</template>

<script setup lang="ts">
import { computed, ref, shallowRef, watch } from 'vue'
import ErrorNote from '../ui/ErrorNote.vue'
import InfoTip from '../ui/InfoTip.vue'
import { useAppStore } from '../../stores/app'
import { useLogSheetStore } from '../../stores/logSheet'
import { calendarStart, useCalendarMonthsStore } from '../../stores/calendarMonths'
import { addDays, addMonths, isoWeek, monthOf } from '../../utils/dates'
import { CALENDAR_COLORS, kcal, longDate, monthTitle, PENDING_COLORS, STATUS_COLORS, statusText, weight, weightChange } from '../../utils/format'
import type { Calendar, CalendarDay, CalendarWeek } from '../../types'

defineEmits<{ 'month-summary': [month: string] }>()

const app = useAppStore()
const sheet = useLogSheetStore()
const month = ref(monthOf(app.today))

const range = computed(() => {
  const from = calendarStart(month.value)
  return { from, to: addDays(from, 41) }
})

const store = useCalendarMonthsStore()
const error = computed(() => store.error)
// After a change the month is loaded again: the page keeps showing what it has until then.
const last = shallowRef<{ month: string; calendar: Calendar } | null>(null)
const data = computed(() => store.get(month.value) ?? (last.value?.month === month.value ? last.value.calendar : null))
watch([month, () => app.version], () => store.show(month.value), { immediate: true })
watch(() => store.get(month.value), (calendar) => { if (calendar) last.value = { month: month.value, calendar } }, { immediate: true })

function move(by: number) { month.value = addMonths(month.value, by) }
watch(() => app.today, (t, old) => { if (month.value === monthOf(old)) month.value = monthOf(t) })

const byDate = computed(() => new Map((data.value?.days ?? []).map((d) => [d.date, d])))
const byWeek = computed(() => new Map((data.value?.weeks ?? []).map((w) => [w.monday, w])))

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']

// Colors and sizes follow the Calendar mockup.
function dayStyle(date: string, d: CalendarDay | undefined) {
  const base = { background: '#ffffff', color: '#4b5563', border: '1px solid #e5e7eb' }
  if (!d) return base
  if (d.calories != null && d.status) {
    const [bg, fg] = CALENDAR_COLORS[d.inGoal ? 'day' : 'dayOut'][d.status]
    return { background: bg, color: fg, border: date === app.today ? `2px solid ${PENDING_COLORS.today[2]}` : '1px solid transparent' }
  }
  if (date === app.today) {
    const [bg, fg, line] = PENDING_COLORS.today
    return d.calories != null
      ? { background: bg, color: fg, border: `2px solid ${line}`, fontWeight: 600 }
      : { background: '#ffffff', color: PENDING_COLORS.planned[1], border: `2px dashed ${line}` }
  }
  if (d.calories != null && date > app.today) {
    const [bg, fg, line] = PENDING_COLORS.ahead
    return { background: bg, color: fg, border: `1px solid ${line}`, fontWeight: 600 }
  }
  if (d.planned != null) {
    const [bg, fg, line] = PENDING_COLORS.planned
    return { background: bg, color: fg, border: `1px dashed ${line}` }
  }
  return base
}

function weekStyle(w: CalendarWeek | undefined) {
  const status = w?.figures.status
  if (!status) return { background: '#f3f4f6', color: '#4b5563', border: '1px solid transparent' }
  // White text on green and yellow is low contrast, as in the mockup. Kept on request.
  const [bg, fg] = CALENDAR_COLORS[w!.figures.inGoal ? 'week' : 'weekOut'][status]
  return { background: bg, color: fg, border: '1px solid transparent' }
}

// The buttons and the title carry the color of what they stand for: today like its day cell, the month like a
// week's average (months are only green or red).
const today = computed(() => data.value?.todayDay ?? last.value?.calendar.todayDay)
const todayStyle = computed(() => dayStyle(app.today, today.value))
const todayTitle = computed(() => {
  const d = today.value
  return d?.calories != null ? `${d.calories} kcal${d.status ? ', ' + statusText(d.status).toLowerCase() : ''}` : 'Not logged yet'
})
const figures = computed(() => data.value?.months.find((m) => m.month === month.value)?.figures)
const monthStyle = computed(() => {
  const f = figures.value
  if (!f?.status) return undefined
  const [bg, fg] = CALENDAR_COLORS[f.inGoal ? 'week' : 'weekOut'][f.status]
  return { background: bg, color: fg, borderColor: 'transparent' }
})
const titleColor = computed(() => (figures.value?.status ? STATUS_COLORS[figures.value.status].text : undefined))
const monthTip = computed(() => (figures.value?.avgCalories != null ? `Average ${figures.value.avgCalories} kcal${figures.value.avgTarget != null ? `, target ${figures.value.avgTarget}` : ''}` : 'Nothing logged'))

const rows = computed(() => {
  const out = []
  for (let monday = range.value.from; monday <= range.value.to; monday = addDays(monday, 7)) {
    const w = byWeek.value.get(monday)
    const f = w?.figures
    const days = Array.from({ length: 7 }, (_, i) => {
      const date = addDays(monday, i)
      const d = byDate.value.get(date)
      const inMonth = monthOf(date) === month.value
      const n = Number(date.slice(8))
      const isToday = date === app.today
      const logged = d?.calories != null
      const past = d != null && date < app.today      // nothing is said about a day that is still loading
      return {
        date, inMonth,
        // Days of the neighbouring months carry their month: "26 Oct".
        top: inMonth ? String(n) : `${n} ${MONTHS[Number(date.slice(5, 7)) - 1]}`,
        mid: logged ? kcal(d!.calories) : d?.planned != null ? kcal(d.planned) : past ? 'no log' : '',
        sub: isToday ? 'today' : d?.isCheatDay ? 'cheat' : '',
        style: dayStyle(date, d),
        title: [d?.target != null ? `Target ${d.target} kcal` : '', d?.planned != null && !logged ? 'Planned from your budget' : '', d?.notes ?? ''].filter(Boolean).join('\n'),
        label: [
          longDate(date),
          logged ? `${d!.calories} kcal${date > app.today ? ', logged ahead' : d!.status ? ', ' + statusText(d!.status).toLowerCase() : ''}` : d?.planned != null ? `planned ${d.planned} kcal` : 'not logged',
          d?.weightKg != null ? weight(d.weightKg, app.unit, true) : '',
          d?.isCheatDay ? 'cheat day' : '',
          d?.notes ? `note: ${d.notes}` : '',
        ].filter(Boolean).join(', '),
      }
    })
    out.push({
      monday,
      top: `W${isoWeek(monday)}${addDays(monday, 6) >= app.today && monday <= app.today ? ' so far' : ''}`,
      avg: f?.avgCalories == null ? '' : kcal(f.avgCalories),
      weightText: f?.avgWeightKg == null ? '' : `${weight(f.avgWeightKg, app.unit)}${f.weightChangeKg != null ? ' ' + weightChange(f.weightChangeKg, app.unit) : ''}`,
      title: f?.avgCalories == null ? 'Nothing logged' : 'Average calories, average weight and its change from the previous week',
      style: weekStyle(w),
      days,
    })
  }
  return out
})
</script>

<style scoped>
.cal { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 18px; display: flex; flex-direction: column; gap: 14px; }
.head { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 8px 12px; }
.title-row { display: flex; align-items: center; gap: 8px; }
h2 { margin: 0; min-width: 150px; font-size: 16px; font-weight: 600; text-align: center; white-space: nowrap; }
.nav { width: 44px; height: 44px; border: 1px solid #d1d5db; border-radius: 8px; background: #fff; font-size: 18px; color: #374151; }
.nav:hover { background: #f3f4f6; }
.actions { display: flex; align-items: center; gap: 8px; }
.actions button { min-height: 44px; padding: 0 16px; border: 1px solid #d1d5db; border-radius: 8px; background: #fff; font: inherit; font-weight: 600; color: #1f2937; }
.actions button:hover { filter: brightness(0.97); }
/* On another month: today's button is the way back. */
.actions button.away { box-shadow: 0 0 0 3px #bae6fd; }
button:focus-visible { outline: 2px solid #0284c7; outline-offset: 2px; }
.grid { display: grid; grid-template-columns: minmax(80px, 1.5fr) repeat(7, minmax(0, 1fr)); gap: 4px; }
.h { text-align: center; font-size: 12px; font-weight: 600; color: #4b5563; }
.cell { min-height: 68px; min-width: 0; box-sizing: border-box; border-radius: 8px; padding: 4px 2px; display: flex; flex-direction: column; align-items: center; justify-content: center; text-align: center; font: inherit; font-weight: 400; }
.day:hover { filter: brightness(0.97); }
.top { font-size: 12px; font-weight: 600; }
.mid { font-size: 12px; }
.sub { font-size: 12px; }
.wk .mid { font-size: 17px; font-weight: 700; }
.wk .sub { font-weight: 600; }
.legend { display: flex; flex-wrap: wrap; gap: 4px 16px; font-size: 13px; color: #4b5563; list-style: none; margin: 0; padding: 0; }
.legend li { display: inline-flex; align-items: center; gap: 6px; }
.legend i { width: 12px; height: 12px; border-radius: 3px; display: inline-block; }
.legend i.dash { background: #fff; border: 1px dashed #6b7280; }
.legend i.ahead { background: #e2e8f0; border: 1px solid #64748b; }
.legend i.light { width: 16px; background: linear-gradient(90deg, #a4f4cf 50%, #ffccd3 50%); }
/* Narrow screens: the same layout, a little tighter. */
@media (max-width: 639px) {
  .cal { padding: 12px; }
  .grid { grid-template-columns: minmax(56px, 1.3fr) repeat(7, minmax(0, 1fr)); gap: 2px; }
  .cell { min-height: 58px; padding: 3px 1px; }
  .top, .mid, .sub { font-size: 11px; }
  .wk .mid { font-size: 14px; }
  .wk .top, .wk .sub { font-size: 10px; }
}
</style>
