<template>
  <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/40" @mousedown.self="$emit('close')">
    <div class="bg-white rounded-xl shadow-xl w-full max-w-sm p-6">
      <div class="flex items-center justify-between mb-5">
        <h2 class="text-lg font-semibold text-gray-800">Log Weight</h2>
        <button @click="$emit('close')" class="text-gray-400 hover:text-gray-600 text-xl leading-none">✕</button>
      </div>

      <form @submit.prevent="save" class="space-y-4">
        <div>
          <label class="label">Date</label>
          <input v-model="date" type="date" class="input" :max="today" />
        </div>
        <div>
          <label class="label">Weight ({{ unit }})</label>
          <input v-model.number="weightDisplay" type="number" step="0.1" min="1" max="700"
            class="input" :placeholder="`e.g. ${unit === 'kg' ? '75.5' : '166'}`" required autofocus />
        </div>
        <div v-if="existing?.weightKg != null" class="flex items-center justify-between">
          <p class="text-xs text-gray-400">Current: {{ formatWeight(existing.weightKg, unit) }}</p>
          <button type="button" @click="removeWeight"
            class="text-xs text-red-400 hover:text-red-600 hover:underline">
            Remove weigh-in
          </button>
        </div>
        <button type="submit" class="btn-primary w-full" :disabled="saving || !weightDisplay">
          {{ saving ? 'Saving…' : 'Save' }}
        </button>
        <p v-if="error" class="text-red-500 text-sm">{{ error }}</p>
      </form>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { useDailyLogStore } from '../../stores/dailyLog'
import { useDashboardStore } from '../../stores/dashboard'
import { useSettingsStore } from '../../stores/settings'
import { toKg, displayWeight, formatWeight } from '../../utils/units'
import type { DailyLog } from '../../types'
import * as api from '../../api/dailyLog'

const props = defineProps<{ initialDate?: string }>()
const emit = defineEmits<{ close: []; saved: [] }>()

const store = useDailyLogStore()
const dashboard = useDashboardStore()
const settingsStore = useSettingsStore()
const unit = computed(() => settingsStore.settings.preferredUnit)

const today = new Date().toISOString().split('T')[0]
const date = ref(props.initialDate ?? today)
const weightDisplay = ref<number | ''>('')
const existing = ref<DailyLog | null>(null)
const saving = ref(false)
const error = ref('')

async function loadExisting() {
  try { existing.value = await api.getByDate(date.value) }
  catch { existing.value = null }
  if (existing.value?.weightKg != null) {
    weightDisplay.value = displayWeight(existing.value.weightKg, unit.value)
  } else {
    const lastLog = store.logs.find(l => l.weightKg != null && l.date !== date.value)
    weightDisplay.value = lastLog?.weightKg != null ? displayWeight(lastLog.weightKg, unit.value) : ''
  }
}

watch(date, loadExisting)
onMounted(loadExisting)

async function save() {
  saving.value = true
  error.value = ''
  try {
    await store.upsert(date.value, {
      date: date.value,
      weightKg: toKg(Number(weightDisplay.value), unit.value),
    })
    await dashboard.fetch()
    emit('saved')
    emit('close')
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    saving.value = false
  }
}

async function removeWeight() {
  if (confirm('Remove this weigh-in?')) {
    await store.deleteWeight(date.value)
    await dashboard.fetch()
    emit('saved')
    emit('close')
  }
}
</script>
