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
    </div>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref, onMounted } from 'vue'
import { useSettingsStore } from '../../stores/settings'
import type { UserSettings } from '../../types'

const emit = defineEmits<{ close: [] }>()
const store = useSettingsStore()
const saving = ref(false)
const form = reactive<UserSettings>({ heightCm: null, preferredUnit: 'kg', tdeeKcal: null })

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
