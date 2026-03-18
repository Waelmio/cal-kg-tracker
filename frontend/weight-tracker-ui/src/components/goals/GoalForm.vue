<template>
  <form @submit.prevent="submit" class="space-y-4">
    <div>
      <label class="block text-sm font-medium text-gray-700 mb-1">
        Target Weight ({{ unit }})
      </label>
      <input v-model.number="displayTarget" type="number" step="0.1" min="1" max="700"
        class="input" required placeholder="e.g. 70" />
    </div>
    <div>
      <label class="block text-sm font-medium text-gray-700 mb-1">Target Date</label>
      <input v-model="form.targetDate" type="date" class="input" required :min="tomorrow" />
    </div>
    <div>
      <label class="block text-sm font-medium text-gray-700 mb-1">Notes (optional)</label>
      <textarea v-model="form.notes" class="input" rows="2" />
    </div>
    <button type="submit" class="btn-primary w-full" :disabled="saving">
      {{ saving ? 'Saving…' : 'Set Goal' }}
    </button>
    <p v-if="error" class="text-red-500 text-sm">{{ error }}</p>
  </form>
</template>

<script setup lang="ts">
import { reactive, ref, computed } from 'vue'
import { useGoalsStore } from '../../stores/goals'
import { useDashboardStore } from '../../stores/dashboard'
import { useSettingsStore } from '../../stores/settings'
import { toKg } from '../../utils/units'

const emit = defineEmits<{ submitted: [] }>()
const store = useGoalsStore()
const dashboard = useDashboardStore()
const settingsStore = useSettingsStore()
const unit = computed(() => settingsStore.settings.preferredUnit)

const saving = ref(false)
const error = ref('')
const displayTarget = ref<number | ''>('')

const tomorrow = new Date(Date.now() + 86400000).toISOString().split('T')[0]
const form = reactive({ targetDate: tomorrow, notes: '' })

async function submit() {
  if (!displayTarget.value) return
  saving.value = true
  error.value = ''
  try {
    await store.create({
      targetWeightKg: toKg(Number(displayTarget.value), unit.value),
      targetDate: form.targetDate,
      notes: form.notes || undefined,
    })
    await dashboard.fetch()
    emit('submitted')
    displayTarget.value = ''
    form.notes = ''
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    saving.value = false
  }
}
</script>
