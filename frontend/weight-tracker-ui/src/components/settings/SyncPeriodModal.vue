<template>
  <Modal :title="`Sync a period with ${name}`" sheet max-width="720px" @close="$emit('close')">
    <!-- 1. The period -->
    <form v-if="!data" id="sync-period" class="content" @submit.prevent="compare">
      <p class="muted">Compares the app with {{ name }} day by day. Nothing changes before you decide. Up to 12 months at once.</p>
      <div class="two">
        <div class="f"><label for="sp-from">From</label><input id="sp-from" v-model="from" type="date" :max="app.today" required /></div>
        <div class="f"><label for="sp-to">To</label><input id="sp-to" v-model="to" type="date" :max="app.today" required /></div>
      </div>
      <div class="quick" role="group" aria-label="Quick periods">
        <button v-for="q in quick" :key="q.months" type="button" class="plain" @click="setPeriod(q.months)">{{ q.text }}</button>
      </div>
      <p v-if="problem" role="alert" class="error">{{ problem }}</p>
      <p v-if="error" role="alert" class="error">{{ error }}</p>
      <p v-if="busy" role="status" class="muted">Reading {{ name }}… A long period can take half a minute.</p>
    </form>

    <!-- 2. The review -->
    <div v-else class="content">
      <p v-if="!data.rows.length" class="same">Everything matches.</p>
      <p class="muted">{{ shortDate(data.from, true) }} to {{ shortDate(data.to, true) }}: {{ data.equalCount }} the same, {{ data.missingCount }} only in the app.</p>

      <template v-if="data.rows.length">
        <div class="tools">
          <button type="button" class="plain" @click="chooseAll('Keep')">Keep all</button>
          <button type="button" class="plain" @click="chooseAll('Replace')">Replace all</button>
          <label v-if="conflicts.length && conflicts.length < data.rows.length" class="only"><input v-model="conflictsOnly" type="checkbox" /> Conflicts only ({{ conflicts.length }})</label>
        </div>

        <section v-for="w in weeks" :key="w.monday" class="week" :aria-label="w.title">
          <h2>{{ w.title }}</h2>
          <div v-for="r in w.rows" :key="rowKey(r)" class="entry" :data-test="rowKey(r)">
            <div class="what">
              <strong>{{ weekdayShort(r.date) }} {{ shortDate(r.date) }}</strong>
              <span class="muted">{{ r.field === 'Calories' ? 'Calories' : 'Weight' }}</span>
              <span v-if="r.setInAdvance" class="mark">set in advance</span>
              <span v-else-if="r.imported" class="mark">from {{ name }}</span>
            </div>
            <div class="pick" role="group" :aria-label="`${longDate(r.date)}, ${r.field === 'Calories' ? 'calories' : 'weight'}`">
              <button type="button" :aria-pressed="choices[rowKey(r)] === 'Keep'" @click="choices[rowKey(r)] = 'Keep'">
                <span class="side">{{ r.kind === 'New' ? 'Leave empty' : 'Keep app' }}</span><span class="v">{{ show(r, r.appValue) }}</span>
              </button>
              <button type="button" :aria-pressed="choices[rowKey(r)] === 'Replace'" @click="choices[rowKey(r)] = 'Replace'">
                <span class="side">{{ r.kind === 'New' ? 'Add' : 'Replace with' }} {{ name }}</span><span class="v">{{ show(r, r.value) }}</span>
              </button>
            </div>
          </div>
        </section>
        <p v-if="leavesEmpty" class="muted">The automatic sync fills empty days of the last 3 weeks, the ones you leave empty too.</p>
      </template>
      <p v-if="error" role="alert" class="error">{{ error }}</p>
    </div>

    <template #footer>
      <div class="foot-row">
        <span v-if="data?.rows.length" class="sum" role="status">{{ summary }}</span>
        <button type="button" class="plain" @click="$emit('close')">{{ data && !data.rows.length ? 'Close' : 'Cancel' }}</button>
        <button v-if="!data" type="submit" form="sync-period" class="go" :disabled="busy || !!problem">{{ busy ? 'Comparing…' : 'Compare' }}</button>
        <button v-else-if="data.rows.length" type="button" class="go" :disabled="busy" @click="applyChoices">{{ busy ? 'Applying…' : 'Apply' }}</button>
      </div>
    </template>
  </Modal>
</template>

<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import Modal from '../ui/Modal.vue'
import { useAppStore } from '../../stores/app'
import * as api from '../../api/integrations'
import { addDays, addMonths, lastDayOfMonth, monthOf } from '../../utils/dates'
import { kcal, longDate, shortDate, weekdayShort, weight } from '../../utils/format'
import { toUnit } from '../../utils/units'
import { applySummary, choiceSummary, groupByWeek, rowKey } from '../../utils/syncReview'
import type { SyncChoice, SyncPreview, SyncRow } from '../../types'

const props = defineProps<{ id: string; name: string }>()
const emit = defineEmits<{ close: []; done: [message: string] }>()

const app = useAppStore()

/** The same day, months earlier (the last day of that month when it is shorter). */
function monthsBack(date: string, months: number): string {
  const month = addMonths(monthOf(date), -months)
  const day = `${month}-${date.slice(8)}`
  const last = lastDayOfMonth(month)
  return day > last ? last : day
}

const from = ref(monthsBack(app.today, 1))
const to = ref(app.today)
const quick = [{ months: 1, text: 'Last month' }, { months: 3, text: 'Last 3 months' }, { months: 12, text: 'Last 12 months' }]
function setPeriod(months: number) {
  from.value = addDays(monthsBack(app.today, months), months === 12 ? 1 : 0)
  to.value = app.today
}

const problem = computed(() => {
  if (!from.value || !to.value) return 'Choose both dates.'
  if (from.value > to.value) return 'The start date must be before the end date.'
  if (from.value < monthsBack(to.value, 12)) return 'The period can be up to 12 months.'
  return ''
})

const busy = ref(false)
const error = ref('')
const data = ref<SyncPreview | null>(null)
const choices = reactive<Record<string, SyncChoice>>({})
const conflictsOnly = ref(false)

const conflicts = computed(() => (data.value?.rows ?? []).filter((r) => r.kind === 'Conflict'))
const shown = computed(() => (conflictsOnly.value ? conflicts.value : data.value?.rows ?? []))
const weeks = computed(() => groupByWeek(shown.value))
const summary = computed(() => choiceSummary(data.value?.rows ?? [], choices))
const leavesEmpty = computed(() => (data.value?.rows ?? []).some((r) => r.kind === 'New' && choices[rowKey(r)] === 'Keep'))

function show(r: SyncRow, v: number | null): string {
  if (v == null) return '–'
  if (r.field === 'Calories') return `${kcal(v)} kcal`
  // Two decimals when one would show the same number on both sides.
  const same = r.appValue != null && weight(r.appValue, app.unit) === weight(r.value, app.unit)
  return same ? `${toUnit(v, app.unit).toFixed(2)} ${app.unit}` : weight(v, app.unit, true)
}

// Bulk choices apply to the rows on screen: with the filter on, the hidden ones keep theirs.
function chooseAll(choice: SyncChoice) {
  shown.value.forEach((r) => { choices[rowKey(r)] = choice })
}

async function compare() {
  if (problem.value) return
  busy.value = true
  error.value = ''
  try {
    const result = await api.preview(props.id, from.value, to.value)
    result.rows.forEach((r) => { choices[rowKey(r)] = r.suggested })
    data.value = result
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busy.value = false
  }
}

async function applyChoices() {
  if (!data.value) return
  busy.value = true
  error.value = ''
  try {
    const decisions = data.value.rows.map((r) => ({ date: r.date, field: r.field, choice: choices[rowKey(r)] }))
    emit('done', applySummary(await api.apply(props.id, data.value.previewId, decisions)))
  } catch (e) {
    error.value = (e as Error).message
    busy.value = false
  }
}
</script>

<style scoped>
.content { display: flex; flex-direction: column; gap: 14px; color: #1f2937; font-size: 15px; line-height: 1.45; font-variant-numeric: tabular-nums; }
p { margin: 0; }
.muted { color: #4b5563; font-size: 14px; }
.two { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 180px), 1fr)); gap: 12px; }
.f { display: flex; flex-direction: column; gap: 6px; }
.f label { font-weight: 600; }
input[type='date'] { box-sizing: border-box; width: 100%; height: 48px; border: 1px solid #9ca3af; border-radius: 8px; padding: 0 14px; font: inherit; color: #1f2937; background: #fff; }
.quick, .tools { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; }
.plain { min-height: 44px; padding: 0 14px; border: 1px solid #9ca3af; border-radius: 8px; background: #fff; color: #1f2937; font: inherit; font-weight: 600; }
.plain:hover { background: #f9fafb; }
.go { min-height: 44px; padding: 0 20px; border: 0; border-radius: 8px; background: #0284c7; color: #fff; font: inherit; font-weight: 600; }
.go:hover:not(:disabled) { background: #0369a1; }
.go:disabled { opacity: 0.6; }
.only { display: inline-flex; align-items: center; gap: 8px; min-height: 44px; margin-left: auto; cursor: pointer; }
.only input { width: 18px; height: 18px; }
.same { font-size: 18px; font-weight: 600; color: #006045; }
.week { display: flex; flex-direction: column; }
h2 { margin: 0; padding: 6px 0; font-size: 13px; font-weight: 600; color: #4b5563; }
.entry { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1.7fr); gap: 8px 16px; align-items: center; padding: 8px 0; border-top: 1px solid #e5e7eb; }
.what { display: flex; flex-wrap: wrap; gap: 2px 8px; align-items: baseline; }
.mark { font-size: 13px; color: #075985; background: #e0f2fe; border-radius: 999px; padding: 1px 8px; white-space: nowrap; }
.pick { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 8px; }
.pick button { min-height: 48px; padding: 4px 10px; border: 1px solid #9ca3af; border-radius: 8px; background: #fff; color: #374151; font: inherit; text-align: left; display: flex; flex-direction: column; justify-content: center; }
.pick button[aria-pressed='true'] { background: #0369a1; border-color: #0369a1; color: #fff; }
.side { font-size: 12px; line-height: 1.3; }
.v { font-weight: 600; line-height: 1.3; }
.foot-row { display: flex; flex-wrap: wrap; justify-content: flex-end; align-items: center; gap: 8px; }
.sum { margin-right: auto; font-weight: 600; font-variant-numeric: tabular-nums; }
.error { color: #a50036; background: #ffe4e6; padding: 10px 12px; border-radius: 8px; }
button:focus-visible, input:focus-visible { outline: 2px solid #0284c7; outline-offset: 2px; }
@media (max-width: 639px) { .entry { grid-template-columns: 1fr; } .only { margin-left: 0; } .sum { flex-basis: 100%; } }
</style>
