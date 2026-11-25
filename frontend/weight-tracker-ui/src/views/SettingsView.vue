<template>
  <main class="page">
    <div class="card">
      <h1>Settings</h1>

      <section class="sec">
        <h2>Calorie averaging window</h2>
        <div class="muted">How many weeks you have to reach your target on average.</div>
        <div class="five" role="group" aria-label="Averaging window">
          <button v-for="n in 5" :key="n" type="button" :aria-pressed="form.averagingWeeks === n" @click="form.averagingWeeks = n">{{ n === 1 ? '1 week' : n + ' weeks' }}</button>
        </div>
        <div v-if="previewBudget != null" class="line"><span class="muted">Budget this week</span><strong>{{ previewBudget }} kcal a day</strong></div>
        <div class="muted small">Changes your daily budget, planned days and which weeks show yellow. Averages and deficits stay the same.</div>
      </section>

      <section class="sec ruled">
        <h2>Profile</h2>
        <div class="two">
          <div class="f">
            <label for="height">Height (cm)</label>
            <input id="height" v-model.number="form.heightCm" type="number" min="50" max="300" step="1" inputmode="numeric" placeholder="e.g. 175" />
          </div>
          <div class="f">
            <span class="b">Weight unit</span>
            <div class="units" role="group" aria-label="Weight unit">
              <button v-for="u in ['kg', 'lbs'] as const" :key="u" type="button" :aria-pressed="form.preferredUnit === u" @click="form.preferredUnit = u">{{ u }}</button>
            </div>
          </div>
        </div>
        <div class="muted small">Height is used for BMI. TDEE and targets are in Goals. Weights are stored in kg; the unit only changes how they are shown.</div>
      </section>

      <section class="sec ruled">
        <h2>Data</h2>
        <div class="units">
          <button type="button" class="plain" :disabled="exporting" @click="doExport">{{ exporting ? 'Exporting…' : 'Export' }}</button>
          <label class="plain pick">
            {{ importing ? 'Importing…' : 'Import' }}
            <input type="file" accept=".json,application/json" :disabled="importing" @change="onPick" />
          </label>
        </div>
        <div v-if="pending" class="ask" role="alertdialog" aria-label="Replace your data?">
          <div class="askt">Importing <strong>{{ pending.name }}</strong> replaces all your logs, goals and settings.</div>
          <div class="row">
            <button type="button" class="danger" @click="confirmImport">Replace everything</button>
            <button type="button" class="plain small-btn" @click="pending = null">Cancel</button>
          </div>
        </div>
        <button type="button" class="clear" :disabled="clearing" @click="confirmClear = true">{{ clearing ? 'Clearing…' : 'Clear data' }}</button>
        <p v-if="dataError" role="alert" class="error">{{ dataError }}</p>
        <p v-if="imported" role="status" class="ok">Import done.</p>
        <p v-if="cleared" role="status" class="ok">All data cleared.</p>
      </section>

      <p v-if="saveError" role="alert" class="error">{{ saveError }}</p>
      <p v-if="saved" role="status" class="ok">Saved.</p>
      <Modal v-if="confirmClear" title="Delete all your data?" max-width="440px" @close="confirmClear = false">
        <p class="dlg">This permanently deletes every log, weight goal, calorie target and TDEE entry, and resets your settings. It can't be undone.</p>
        <p class="dlg">Export first if you want a copy.</p>
        <template #footer>
          <div class="dlg-row">
            <button type="button" class="plain small-btn" @click="confirmClear = false">Cancel</button>
            <button type="button" class="danger" @click="clearAll">Delete everything</button>
          </div>
        </template>
      </Modal>

      <button type="button" class="save" :disabled="saving" @click="save">{{ saving ? 'Saving…' : 'Save settings' }}</button>
    </div>
  </main>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import Modal from '../components/ui/Modal.vue'
import { useAppStore } from '../stores/app'
import { clearData, exportData, importData } from '../api/data'
import * as statsApi from '../api/stats'
import { localToday } from '../utils/dates'

const app = useAppStore()
const form = reactive({ heightCm: null as number | null, preferredUnit: 'kg' as 'kg' | 'lbs', averagingWeeks: 2 })
const budgets = ref<{ weeks: number; perDay: number | null }[]>([])
const saving = ref(false)
const saved = ref(false)
const saveError = ref('')
const exporting = ref(false)
const importing = ref(false)
const imported = ref(false)
const dataError = ref('')
const pending = ref<{ name: string; data: object } | null>(null)
const confirmClear = ref(false)
const clearing = ref(false)
const cleared = ref(false)

const previewBudget = computed(() => budgets.value.find((b) => b.weeks === form.averagingWeeks)?.perDay ?? null)

onMounted(async () => {
  if (!app.loaded) await app.loadSettings().catch(() => {})
  form.heightCm = app.settings.heightCm
  form.preferredUnit = app.settings.preferredUnit
  form.averagingWeeks = app.settings.averagingWeeks
})

// The preview follows whatever changes the budget: a save, an import, clearing the data.
watch(() => app.version, async () => { budgets.value = await statsApi.getBudgetByWindow().catch(() => []) }, { immediate: true })

async function save() {
  saving.value = true
  saved.value = false
  saveError.value = ''
  try {
    await app.saveSettings({ heightCm: form.heightCm || null, preferredUnit: form.preferredUnit, averagingWeeks: form.averagingWeeks })
    saved.value = true
  } catch (e) {
    saveError.value = (e as Error).message
  } finally {
    saving.value = false
  }
}

async function doExport() {
  exporting.value = true
  dataError.value = ''
  try {
    const data = await exportData()
    const url = URL.createObjectURL(new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' }))
    const a = document.createElement('a')
    a.href = url
    a.download = `weighttracker-${localToday()}.json`
    a.click()
    URL.revokeObjectURL(url)
  } catch (e) {
    dataError.value = (e as Error).message
  } finally {
    exporting.value = false
  }
}

async function clearAll() {
  confirmClear.value = false
  clearing.value = true
  dataError.value = ''
  imported.value = false
  cleared.value = false
  try {
    await clearData()
    await app.loadSettings()
    form.heightCm = app.settings.heightCm
    form.preferredUnit = app.settings.preferredUnit
    form.averagingWeeks = app.settings.averagingWeeks
    app.refresh()
    cleared.value = true
  } catch (e) {
    dataError.value = (e as Error).message
  } finally {
    clearing.value = false
  }
}

// Import asks first: nothing is sent before the person confirms.
async function onPick(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return
  dataError.value = ''
  imported.value = false
  cleared.value = false
  try {
    pending.value = { name: file.name, data: JSON.parse(await file.text()) }
  } catch {
    dataError.value = 'That file is not valid JSON.'
  }
}

async function confirmImport() {
  if (!pending.value) return
  const { data } = pending.value
  pending.value = null
  importing.value = true
  try {
    await importData(data)
    await app.loadSettings()
    form.heightCm = app.settings.heightCm
    form.preferredUnit = app.settings.preferredUnit
    form.averagingWeeks = app.settings.averagingWeeks
    app.refresh()
    imported.value = true
  } catch (e) {
    dataError.value = (e as Error).message
  } finally {
    importing.value = false
  }
}
</script>

<style scoped>
.page { max-width: 648px; margin: 0 auto; padding: 20px 16px 24px; color: #1f2937; font-size: 15px; line-height: 1.45; font-variant-numeric: tabular-nums; }
.card { background: #fff; border: 1px solid #e5e7eb; border-radius: 14px; padding: 20px 24px 24px; display: flex; flex-direction: column; gap: 22px; }
h1 { margin: 0; font-size: 20px; font-weight: 700; }
h2 { margin: 0; font-size: 16px; font-weight: 600; }
.sec { display: flex; flex-direction: column; gap: 10px; }
.ruled { border-top: 1px solid #e5e7eb; padding-top: 18px; gap: 12px; }
.muted { color: #4b5563; }
.small { font-size: 14px; }
.sec > .muted:first-of-type { color: #374151; }
.five { display: grid; grid-template-columns: repeat(5, minmax(0, 1fr)); gap: 8px; }
.five button, .units button { min-height: 48px; border-radius: 8px; border: 1px solid #9ca3af; background: #fff; color: #374151; font: inherit; font-weight: 600; }
.five button[aria-pressed='true'], .units button[aria-pressed='true'] { background: #0369a1; border-color: #0369a1; color: #fff; }
.line { display: flex; justify-content: space-between; gap: 12px; }
.two { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 16px; }
.f { display: flex; flex-direction: column; gap: 6px; }
label, .b { font-weight: 600; }
input[type='number'] { box-sizing: border-box; width: 100%; height: 48px; border: 1px solid #9ca3af; border-radius: 8px; padding: 0 14px; font: inherit; color: #1f2937; }
.units { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 8px; }
.plain { min-height: 48px; border: 1px solid #9ca3af; border-radius: 8px; background: #fff; color: #1f2937; font: inherit; font-weight: 600; display: inline-flex; align-items: center; justify-content: center; }
.small-btn { min-height: 44px; padding: 0 16px; }
.pick { position: relative; cursor: pointer; }
.pick input { position: absolute; inset: 0; opacity: 0; cursor: pointer; }
.pick:focus-within { outline: 2px solid #0284c7; }
.ask { background: #fff1f2; border-radius: 8px; padding: 14px; display: flex; flex-direction: column; gap: 10px; }
.askt { color: #881337; }
.row { display: flex; gap: 8px; }
.danger { min-height: 44px; padding: 0 16px; border: 0; border-radius: 8px; background: #c70036; color: #fff; font: inherit; font-weight: 600; }
.clear { min-height: 48px; border: 1px solid #fda4af; border-radius: 8px; background: #fff; color: #c70036; font: inherit; font-weight: 600; }
.dlg { margin: 0 0 8px; }
.dlg-row { display: flex; justify-content: flex-end; gap: 8px; }
.save { min-height: 52px; border: 0; border-radius: 10px; background: #0284c7; color: #fff; font: inherit; font-size: 16px; font-weight: 600; }
.save:hover:not(:disabled) { background: #0369a1; }
.error { margin: 0; color: #a50036; background: #ffe4e6; padding: 10px 12px; border-radius: 8px; }
.ok { margin: 0; color: #006045; }
button:focus-visible, input:focus-visible { outline: 2px solid #0284c7; outline-offset: 2px; }
@media (max-width: 639px) { .card { padding: 16px; } .two { grid-template-columns: 1fr; } .five { grid-template-columns: repeat(auto-fit, minmax(72px, 1fr)); } }
</style>
