<template>
  <main class="page">
    <h1>Settings</h1>

    <form class="card" @submit.prevent="save">
      <h2>You</h2>
      <div class="field">
        <label for="height">Height (cm)</label>
        <input id="height" v-model.number="form.heightCm" class="input" type="number" min="50" max="300" step="1" inputmode="numeric" placeholder="e.g. 175" />
        <p class="hint">Used to calculate BMI.</p>
      </div>
      <div class="field">
        <label for="unit">Weight unit</label>
        <select id="unit" v-model="form.preferredUnit" class="input">
          <option value="kg">Kilograms (kg)</option>
          <option value="lbs">Pounds (lbs)</option>
        </select>
        <p class="hint">Weights are stored in kg. This only changes how they are shown.</p>
      </div>
      <div class="field">
        <label for="weeks">Averaging window</label>
        <select id="weeks" v-model.number="form.averagingWeeks" class="input">
          <option v-for="n in 5" :key="n" :value="n">{{ n }} {{ n === 1 ? 'week' : 'weeks' }}</option>
        </select>
        <p class="hint">How many weeks you have to reach your target on average.</p>
      </div>
      <p v-if="saveError" role="alert" class="error">{{ saveError }}</p>
      <p v-if="saved" role="status" class="ok">Saved.</p>
      <div><button type="submit" class="btn-primary" :disabled="saving">{{ saving ? 'Saving…' : 'Save settings' }}</button></div>
    </form>

    <section class="card" aria-labelledby="data">
      <h2 id="data">Your data</h2>
      <p class="hint">Export everything as a JSON file, or restore from one. Importing replaces what is in the app now.</p>
      <div class="row">
        <button type="button" class="btn-secondary" :disabled="exporting" @click="doExport">{{ exporting ? 'Exporting…' : 'Export JSON' }}</button>
        <label class="btn-secondary pick">
          {{ importing ? 'Importing…' : 'Import JSON' }}
          <input type="file" accept=".json,application/json" :disabled="importing" @change="onPick" />
        </label>
      </div>
      <p v-if="dataError" role="alert" class="error">{{ dataError }}</p>
      <p v-if="imported" role="status" class="ok">Import done.</p>
    </section>

    <Modal v-if="pending" title="Replace your data?" max-width="440px" @close="pending = null">
      <p>Importing <strong>{{ pending.name }}</strong> replaces every log, goal and setting in the app with the file's contents. This can't be undone. Export first if you want a copy.</p>
      <template #footer>
        <div class="row end">
          <button type="button" class="btn-secondary" @click="pending = null">Cancel</button>
          <button type="button" class="btn-primary danger" @click="confirmImport">Replace my data</button>
        </div>
      </template>
    </Modal>
  </main>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import Modal from '../components/ui/Modal.vue'
import { useAppStore } from '../stores/app'
import { exportData, importData } from '../api/data'
import { localToday } from '../utils/dates'

const app = useAppStore()
const form = reactive({ heightCm: null as number | null, preferredUnit: 'kg' as 'kg' | 'lbs', averagingWeeks: 2 })
const saving = ref(false)
const saved = ref(false)
const saveError = ref('')
const exporting = ref(false)
const importing = ref(false)
const imported = ref(false)
const dataError = ref('')
const pending = ref<{ name: string; data: object } | null>(null)

onMounted(async () => {
  if (!app.loaded) await app.loadSettings().catch(() => {})
  form.heightCm = app.settings.heightCm
  form.preferredUnit = app.settings.preferredUnit
  form.averagingWeeks = app.settings.averagingWeeks
})

async function save() {
  saving.value = true
  saved.value = false
  saveError.value = ''
  try {
    await app.saveSettings({ ...app.settings, heightCm: form.heightCm || null, preferredUnit: form.preferredUnit, averagingWeeks: form.averagingWeeks })
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

// Import asks first: nothing is sent before the person confirms.
async function onPick(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return
  dataError.value = ''
  imported.value = false
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
.page { max-width: 640px; margin: 0 auto; padding: 20px 16px 24px; display: flex; flex-direction: column; gap: 16px; }
h1 { margin: 0; font-size: 20px; }
h2 { margin: 0; font-size: 16px; }
.card { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 18px; display: flex; flex-direction: column; gap: 14px; }
.field { display: flex; flex-direction: column; gap: 4px; }
label { font-weight: 600; }
.input { min-height: 44px; }
.hint { margin: 0; color: #4b5563; font-size: 13px; }
.row { display: flex; flex-wrap: wrap; gap: 8px; }
.row.end { justify-content: flex-end; }
.pick { position: relative; cursor: pointer; display: inline-flex; align-items: center; min-height: 44px; }
.pick input { position: absolute; inset: 0; opacity: 0; cursor: pointer; }
.pick:focus-within { outline: 2px solid #0284c7; }
.error { margin: 0; color: #a50036; background: #ffe4e6; padding: 10px 12px; border-radius: 8px; }
.ok { margin: 0; color: #006045; }
.danger { background: #c70036; }
.btn-primary, .btn-secondary { min-height: 44px; }
</style>
