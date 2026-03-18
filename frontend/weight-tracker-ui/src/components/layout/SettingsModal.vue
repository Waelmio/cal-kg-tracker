<template>
  <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/40" @click.self="$emit('close')">
    <div class="bg-white rounded-xl shadow-xl w-full max-w-sm p-6">
      <h2 class="text-lg font-semibold mb-4 text-gray-800">Settings</h2>
      <form @submit.prevent="save" class="space-y-4">
        <div>
          <label class="label">Height (cm)</label>
          <input v-model.number="form.heightCm" type="number" step="0.1" min="50" max="300"
            class="input" placeholder="e.g. 175" />
          <p class="text-xs text-gray-400 mt-1">Used to calculate BMI.</p>
        </div>
        <div>
          <label class="label">Weight Unit</label>
          <select v-model="form.preferredUnit" class="input">
            <option value="kg">Kilograms (kg)</option>
            <option value="lbs">Pounds (lbs)</option>
          </select>
        </div>
        <div class="flex gap-3 pt-2">
          <button type="submit" class="btn-primary flex-1" :disabled="saving">
            {{ saving ? 'Saving…' : 'Save' }}
          </button>
          <button type="button" @click="$emit('close')" class="btn-secondary flex-1">Cancel</button>
        </div>
      </form>

      <div class="mt-5 pt-4 border-t border-gray-100 space-y-2">
        <p class="text-xs font-semibold text-gray-500 uppercase tracking-wide">Data</p>
        <div class="flex gap-3">
          <button type="button" @click="handleExport" :disabled="exporting" class="btn-secondary flex-1 text-sm">
            {{ exporting ? 'Exporting…' : 'Export JSON' }}
          </button>
          <label class="btn-secondary flex-1 text-sm text-center cursor-pointer">
            {{ importing ? 'Importing…' : 'Import JSON' }}
            <input type="file" accept=".json,application/json" class="hidden" @change="handleImport" :disabled="importing" />
          </label>
        </div>
        <p v-if="dataError" class="text-red-500 text-xs">{{ dataError }}</p>
        <p v-if="importSuccess" class="text-green-600 text-xs">Import successful — please refresh the page.</p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref, onMounted } from 'vue'
import { useSettingsStore } from '../../stores/settings'
import { exportData, importData } from '../../api/data'
import type { UserSettings } from '../../types'

const emit = defineEmits<{ close: [] }>()
const store = useSettingsStore()
const saving = ref(false)
const form = reactive<UserSettings>({ heightCm: null, preferredUnit: 'kg', tdeeKcal: null })

const exporting = ref(false)
const importing = ref(false)
const dataError = ref('')
const importSuccess = ref(false)

async function handleExport() {
  exporting.value = true
  dataError.value = ''
  try {
    const data = await exportData()
    const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `weighttracker-${new Date().toISOString().slice(0, 10)}.json`
    a.click()
    URL.revokeObjectURL(url)
  } catch (e) {
    dataError.value = (e as Error).message
  } finally {
    exporting.value = false
  }
}

async function handleImport(event: Event) {
  const file = (event.target as HTMLInputElement).files?.[0]
  if (!file) return
  importing.value = true
  dataError.value = ''
  importSuccess.value = false
  try {
    const text = await file.text()
    const data = JSON.parse(text)
    await importData(data)
    importSuccess.value = true
  } catch (e) {
    dataError.value = (e as Error).message
  } finally {
    importing.value = false
    ;(event.target as HTMLInputElement).value = ''
  }
}

onMounted(() => {
  form.heightCm = store.settings.heightCm
  form.preferredUnit = store.settings.preferredUnit
  form.tdeeKcal = store.settings.tdeeKcal
})

async function save() {
  saving.value = true
  try {
    await store.save({ ...form })
    emit('close')
  } finally {
    saving.value = false
  }
}
</script>
