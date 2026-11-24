<template>
  <section class="panel" aria-labelledby="td-h">
    <div>
      <h2 id="td-h">TDEE, what you burn</h2>
      <div class="big">{{ current ?? '–' }} <span class="by">kcal a day</span></div>
    </div>

    <form class="form" @submit.prevent="saveHand">
      <div class="f"><label for="td">Set by hand</label><input id="td" v-model="handInput" inputmode="numeric" :placeholder="current != null ? String(current) : 'kcal a day'" /></div>
      <button type="submit" class="go" :disabled="busy || !/^\d{3,5}$/.test(handInput.trim())">Save</button>
    </form>
    <p v-if="actionError" role="alert" class="error">{{ actionError }}</p>

    <div v-if="history.length" class="hist">
      <div v-for="h in history" :key="h.id">
        <span>{{ h.range }}</span><strong>{{ h.kcal }}</strong>
        <button type="button" class="rm" :disabled="busy" :aria-label="`Remove ${h.kcal}, ${h.range}`" @click="removing = h">Remove</button>
      </div>
    </div>
    <Modal v-if="removing" title="Remove this TDEE?" max-width="440px" @close="removing = null">
      <p>{{ removing.kcal }}, {{ removing.range.toLowerCase() }}. Its days use the value before it{{ history.length === 1 ? ', and there is none: their deficits disappear until you set a TDEE again' : '' }}.</p>
      <template #footer>
        <div class="dlg-row">
          <button type="button" class="plain" @click="removing = null">Cancel</button>
          <button type="button" class="danger" @click="remove">Remove</button>
        </div>
      </template>
    </Modal>

    <div class="box">
      <div class="est-head"><span class="b">Estimated from your logs</span><span class="est">{{ data?.estimate?.kcal ?? '' }}</span></div>
      <div class="periods">
        <button v-for="p in [30, 60, 90]" :key="p" type="button" :aria-pressed="days === p" @click="days = p as 30 | 60 | 90">{{ p }} days</button>
      </div>

      <ErrorNote v-if="error" :message="error" :retry="reload" />
      <p v-else-if="loading && !data" class="hint" aria-busy="true">Loading…</p>
      <template v-else-if="data">
        <div v-if="data.note" class="quick"><strong>Quick estimate.</strong> Can be off by 300 kcal or more.</div>
        <p v-if="data.suggestUpdate" class="suggest">This differs from your current TDEE by more than 100 kcal.</p>

        <template v-if="data.estimate">
          <div v-for="r in estimateRows" :key="r.label" class="r"><InfoTip :label="r.label" :text="r.tip" /><span>{{ r.value }}</span></div>
          <button type="button" class="use" :disabled="busy" @click="useEstimate">Use {{ data.estimate.kcal }}</button>
        </template>
        <p v-else class="hint">{{ data.missing }}</p>
      </template>
    </div>

    <div v-if="data?.diet" class="diet">
      <h3>What the diet changed, about</h3>
      <div v-for="r in data.diet.rows" :key="r.key" class="r" :class="{ line: r.key === 'now' }">
        <InfoTip :text="dietTip(r.key, r.label)"><span :class="{ strong: ['before', 'now', 'keep'].includes(r.key) }">{{ dietLabel(r.key, r.label) }}</span></InfoTip>
        <span :class="{ strong: ['before', 'now'].includes(r.key), keep: r.key === 'keep' }">{{ r.kcal < 0 ? '−' + Math.abs(r.kcal) : r.kcal }}</span>
      </div>
      <p class="hint">{{ data.diet.footer }}</p>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import ErrorNote from '../ui/ErrorNote.vue'
import InfoTip from '../ui/InfoTip.vue'
import Modal from '../ui/Modal.vue'
import { useAppStore } from '../../stores/app'
import { useAsyncData } from '../../composables/useAsyncData'
import * as statsApi from '../../api/stats'
import * as tdeeApi from '../../api/tdee'
import { addDays } from '../../utils/dates'
import { shortDate, weight } from '../../utils/format'
import type { TdeeEntry } from '../../types'

const props = defineProps<{ current: number | null; entries: TdeeEntry[] }>()

const app = useAppStore()
const days = ref<30 | 60 | 90>(90)
const handInput = ref('')
const busy = ref(false)
const actionError = ref('')

// "Since 15 Oct 2026" for the value in force, "1 Jun to 14 Oct 2026" for earlier ones.
const history = computed(() => {
  const sorted = [...props.entries].sort((a, b) => b.effectiveFrom.localeCompare(a.effectiveFrom))
  return sorted.map((e, i) => ({
    id: e.id,
    kcal: e.kcal,
    range: i === 0 ? `Since ${shortDate(e.effectiveFrom, true)}` : `${shortDate(e.effectiveFrom)} to ${shortDate(addDays(sorted[i - 1].effectiveFrom, -1), true)}`,
  }))
})
const removing = ref<{ id: number; kcal: number; range: string } | null>(null)

const { data, loading, error, reload } = useAsyncData(() => statsApi.getTdee(days.value), [days])

const estimateRows = computed(() => {
  const e = data.value?.estimate
  if (!e) return []
  const g = e.weightTrendGramsPerDay
  return [
    { label: 'Average eaten', value: String(e.avgCalories), tip: 'Average of logged days in the period.' },
    { label: 'Weight trend', value: `${g < 0 ? '▼' : g > 0 ? '▲' : ''} ${Math.abs(g).toFixed(0)} g a day`, tip: 'Slope of a straight line through all weigh-ins.' },
    { label: 'Adjusted for weight lost', value: `${e.adjustment >= 0 ? '+' : '−'}${Math.abs(e.adjustment)}`, tip: 'You burn about 17 kcal less per kg lost (Hall et al. 2011). This moves the period average to your weight now.' },
    { label: 'Based on', value: `${e.weighIns} weigh-ins, ${e.loggedDays} / ${e.spanDays} days logged`, tip: 'More weigh-ins and logged days give a better estimate.' },
    { label: 'Period', value: `${shortDate(e.first)} to ${shortDate(e.last, true)}`, tip: 'From your first to last weigh-in in the period.' },
  ]
})

function dietTip(key: string, label: string) {
  switch (key) {
    case 'before': return 'What you burned at your starting weight, eating normally.'
    case 'lighter': return 'A smaller body burns less (17 per kg).'
    case 'digest': return 'Digestion burns about 10% of what you eat.'
    case 'slowed': return 'Your metabolism slows down when you eat less calories.'
    case 'now': return 'Used for your deficits.'
    case 'keep': return 'Higher than your TDEE, because eating more makes you burn more.'
    default: return label
  }
}

const dietLabel = (key: string, label: string) => {
  if (key !== 'lighter' || !data.value?.diet) return label
  const kg = data.value.diet.lostKg      // in the user's unit
  return `${weight(Math.abs(kg), app.unit)} ${app.unit} ${kg >= 0 ? 'lighter' : 'heavier'}`
}

async function run(change: () => Promise<unknown>) {
  busy.value = true
  actionError.value = ''
  try {
    await change()
    handInput.value = ''
    app.refresh()      // every statistic that uses the TDEE refetches, the dashboard included
  } catch (e) {
    actionError.value = (e as Error).message
  } finally {
    busy.value = false
  }
}
const add = (kcal: number, source: 'Manual' | 'Estimate') => run(() => tdeeApi.addEntry(kcal, source))
const remove = () => {
  const id = removing.value!.id
  removing.value = null
  return run(() => tdeeApi.removeEntry(id))
}
const saveHand = () => add(Number(handInput.value.trim()), 'Manual')
const useEstimate = () => data.value?.estimate && add(data.value.estimate.kcal, 'Estimate')
</script>

<style scoped>
.panel { background: #f5f3ff; border: 1px solid #ddd6fe; border-radius: 12px; padding: 20px; display: flex; flex-direction: column; gap: 14px; }
h2 { margin: 0; font-size: 14px; font-weight: 600; color: #5b21b6; }
h3 { margin: 0; font-size: 14px; font-weight: 600; color: #5b21b6; }
.big { font-size: 30px; font-weight: 700; line-height: 1.2; }
.by { font-size: 15px; font-weight: 400; color: #374151; }
.form { display: grid; grid-template-columns: minmax(0, 1fr) auto; gap: 8px; align-items: end; }
.f { display: flex; flex-direction: column; gap: 6px; }
label { font-weight: 600; }
input { box-sizing: border-box; width: 100%; height: 46px; border: 1px solid #9ca3af; border-radius: 8px; padding: 0 12px; font: inherit; font-weight: 600; }
.go { min-height: 46px; padding: 0 18px; border: 0; border-radius: 8px; background: #6d28d9; color: #fff; font: inherit; font-weight: 600; }
.go:disabled { opacity: 0.5; }
.hist { border-top: 1px solid #ddd6fe; padding-top: 4px; display: flex; flex-direction: column; }
.hist div { display: grid; grid-template-columns: minmax(0, 1fr) auto auto; gap: 12px; align-items: center; }
.rm { min-height: 36px; padding: 0 4px; border: 0; background: none; font: inherit; font-size: 13px; color: #5b21b6; }
.dlg-row { display: flex; justify-content: flex-end; gap: 8px; }
.dlg-row button { min-height: 44px; padding: 0 16px; border-radius: 8px; font: inherit; font-weight: 600; }
.plain { border: 1px solid #9ca3af; background: #fff; color: #1f2937; }
.danger { border: 0; background: #c70036; color: #fff; }
.box { background: #fff; border: 1px solid #ddd6fe; border-radius: 10px; padding: 14px; display: flex; flex-direction: column; gap: 6px; }
.est-head { display: flex; justify-content: space-between; gap: 12px; align-items: baseline; }
.b { font-weight: 600; }
.est { font-size: 20px; font-weight: 700; color: #5b21b6; }
.periods { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 6px; }
.periods button { min-height: 44px; border-radius: 8px; border: 1px solid #c4b5fd; background: #fff; color: #5b21b6; font: inherit; font-weight: 600; }
.periods button[aria-pressed='true'] { background: #6d28d9; border-color: #6d28d9; color: #fff; }
.quick { background: #fef3c6; color: #973c00; border-radius: 8px; padding: 10px 12px; }
.suggest { margin: 0; color: #5b21b6; }
.r { display: flex; justify-content: space-between; gap: 12px; align-items: center; color: #4b5563; }
.r span:last-child, .r > span { color: #1f2937; }
.r.line { border-top: 1px solid #ddd6fe; padding-top: 6px; }
.strong { font-weight: 600; color: #1f2937; }
.keep { font-weight: 700; color: #5b21b6; }
.use { margin-top: 6px; min-height: 44px; border: 1px solid #c4b5fd; border-radius: 8px; background: #f5f3ff; color: #5b21b6; font: inherit; font-weight: 600; }
.diet { display: flex; flex-direction: column; gap: 6px; }
.hint { margin: 0; font-size: 13px; color: #4b5563; }
.error { margin: 0; color: #a50036; background: #ffe4e6; padding: 10px 12px; border-radius: 8px; }
button:focus-visible, input:focus-visible { outline: 2px solid #0284c7; outline-offset: 2px; }
</style>
