<template>
  <section class="sec" aria-labelledby="td-h">
    <h2 id="td-h">TDEE, what you burn</h2>
    <div class="card">
      <p class="big">{{ current ?? '–' }} <span class="unit">kcal a day</span></p>
      <form class="row" @submit.prevent="saveHand">
        <div class="f">
          <label for="td">Set by hand</label>
          <input id="td" v-model="handInput" class="input" inputmode="numeric" placeholder="kcal a day" />
        </div>
        <button type="submit" class="btn-primary" :disabled="busy || !/^\d{3,5}$/.test(handInput.trim())">Save</button>
      </form>
      <p class="hint">A new value counts from today. Past days keep the TDEE that was in force.</p>

      <h3>Estimated from your logs</h3>
      <div class="seg" role="group" aria-label="Period">
        <button v-for="p in [30, 60, 90]" :key="p" type="button" :aria-pressed="days === p" @click="days = p as 30 | 60 | 90">{{ p }} days</button>
      </div>

      <ErrorNote v-if="error" :message="error" :retry="reload" />
      <p v-else-if="loading && !data" class="hint" aria-busy="true">Loading…</p>
      <template v-else-if="data">
        <p v-if="data.note" class="note"><strong>Quick estimate.</strong> Can be off by 300 kcal or more.</p>

        <template v-if="data.estimate">
          <p class="big est">{{ data.estimate.kcal }} <span class="unit">kcal a day</span></p>
          <p v-if="data.suggestUpdate" class="suggest">This differs from your current TDEE by more than 100 kcal.</p>
          <dl class="rows">
            <div v-for="r in estimateRows" :key="r.label" class="r">
              <dt><InfoTip :label="r.label" :text="r.tip" /></dt><dd>{{ r.value }}</dd>
            </div>
          </dl>
          <div><button type="button" class="btn-secondary" :disabled="busy" @click="useEstimate">Use {{ data.estimate.kcal }}</button></div>
        </template>
        <p v-else class="missing">{{ data.missing }}</p>

        <template v-if="data.diet">
          <h3>What the diet changed, about</h3>
          <dl class="rows">
            <div v-for="r in data.diet.rows" :key="r.key" class="r">
              <dt><InfoTip :label="r.label" :text="dietTip(r.key, r.label)" /></dt><dd>{{ r.kcal > 0 && r.key !== 'before' && r.key !== 'now' && r.key !== 'keep' ? '+' : '' }}{{ r.kcal < 0 ? '−' + Math.abs(r.kcal) : r.kcal }}</dd>
            </div>
          </dl>
          <p class="hint">{{ data.diet.footer }}</p>
        </template>
      </template>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import ErrorNote from '../ui/ErrorNote.vue'
import InfoTip from '../ui/InfoTip.vue'
import { useAppStore } from '../../stores/app'
import { useAsyncData } from '../../composables/useAsyncData'
import * as statsApi from '../../api/stats'
import * as tdeeApi from '../../api/tdee'
import { shortDate } from '../../utils/format'

defineProps<{ current: number | null }>()
const emit = defineEmits<{ changed: [] }>()

const app = useAppStore()
const days = ref<30 | 60 | 90>(90)
const handInput = ref('')
const busy = ref(false)
const actionError = ref('')

const { data, loading, error, reload } = useAsyncData(() => statsApi.getTdee(days.value), [days])

const estimateRows = computed(() => {
  const e = data.value?.estimate
  if (!e) return []
  const g = e.weightTrendGramsPerDay
  return [
    { label: 'TDEE', value: `${e.kcal} kcal`, tip: 'Calories you burn in a day.' },
    { label: 'Average eaten', value: `${e.avgCalories} kcal`, tip: 'Average of logged days in the period.' },
    { label: 'Weight trend', value: `${g > 0 ? '+' : g < 0 ? '−' : ''}${Math.abs(g).toFixed(1)} g a day`, tip: 'Slope of a straight line through all weigh-ins.' },
    { label: 'Adjusted for weight lost', value: `${e.adjustment >= 0 ? '+' : '−'}${Math.abs(e.adjustment)} kcal`, tip: 'You burn about 17 kcal less per kg lost (Hall et al. 2011). This moves the period average to your weight now.' },
    { label: 'Based on', value: `${e.weighIns} weigh-ins, ${e.loggedDays} / ${e.spanDays} days logged`, tip: 'More weigh-ins and logged days give a better estimate.' },
    { label: 'Period', value: `${shortDate(e.first)} to ${shortDate(e.last)}`, tip: 'From your first to last weigh-in in the period.' },
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

async function add(kcal: number, source: 'Manual' | 'Estimate') {
  busy.value = true
  actionError.value = ''
  try {
    await tdeeApi.addEntry(kcal, source)
    handInput.value = ''
    app.refresh()      // every statistic that uses the TDEE refetches, the dashboard included
    emit('changed')
  } catch (e) {
    actionError.value = (e as Error).message
  } finally {
    busy.value = false
  }
}
const saveHand = () => add(Number(handInput.value.trim()), 'Manual')
const useEstimate = () => data.value?.estimate && add(data.value.estimate.kcal, 'Estimate')
</script>

<style scoped>
.sec { display: flex; flex-direction: column; gap: 12px; border-left: 4px solid #7c3aed; padding-left: 14px; }
h2 { margin: 0; font-size: 18px; color: #5b21b6; }
h3 { margin: 6px 0 0; font-size: 15px; }
.card { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 18px; display: flex; flex-direction: column; gap: 10px; }
.big { margin: 0; font-size: 28px; font-weight: 700; }
.big.est { font-size: 24px; }
.unit { font-size: 14px; font-weight: 400; color: #4b5563; }
.row { display: flex; flex-wrap: wrap; align-items: flex-end; gap: 8px; }
.f { display: flex; flex-direction: column; gap: 4px; }
label { font-weight: 600; }
.input { min-height: 44px; width: 160px; }
.row button, .card > div > button { min-height: 44px; }
.hint { margin: 0; color: #4b5563; font-size: 13px; }
.seg { display: flex; gap: 4px; }
.seg button { min-height: 44px; padding: 0 14px; border: 1px solid #d1d5db; background: #fff; border-radius: 8px; cursor: pointer; }
.seg button[aria-pressed='true'] { background: #7c3aed; color: #fff; border-color: #7c3aed; }
.note { margin: 0; background: #fef3c6; color: #973c00; padding: 8px 12px; border-radius: 8px; }
.suggest { margin: 0; color: #5b21b6; }
.missing { margin: 0; color: #4b5563; }
.rows { margin: 0; display: flex; flex-direction: column; gap: 4px; }
.r { display: flex; justify-content: space-between; gap: 12px; }
dt { color: #4b5563; }
dd { margin: 0; font-weight: 600; text-align: right; }
</style>
