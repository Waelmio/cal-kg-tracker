<template>
  <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/40" @mousedown.self="$emit('close')">
    <div class="bg-white rounded-xl shadow-xl w-full max-w-sm p-6">
      <div class="flex items-center justify-between mb-5">
        <h2 class="text-lg font-semibold text-gray-800">Log Calories</h2>
        <button @click="$emit('close')" class="text-gray-400 hover:text-gray-600 text-xl leading-none">✕</button>
      </div>

      <form @submit.prevent="save" class="space-y-4">
        <div>
          <label class="label">Date</label>
          <input v-model="date" type="date" class="input" />
        </div>
        <div>
          <label class="label">Calories (kcal)</label>
          <input v-model.number="calories" type="number" min="0" max="20000"
            class="input" placeholder="e.g. 1800" required autofocus />
          <p v-if="calorieTarget && calories" class="text-xs mt-1.5"
            :class="Number(calories) > calorieTarget ? 'text-red-400' : 'text-emerald-500'">
            {{ Number(calories) > calorieTarget
              ? `${Number(calories) - calorieTarget} kcal over your ${calorieTarget} kcal target`
              : `${calorieTarget - Number(calories)} kcal under your ${calorieTarget} kcal target` }}
          </p>
        </div>
        <div v-if="existing?.caloriesKcal != null" class="flex items-center justify-between">
          <p class="text-xs text-gray-400">Current: {{ existing.caloriesKcal }} kcal</p>
          <button type="button" @click="removeCalories"
            class="text-xs text-red-400 hover:text-red-600 hover:underline">
            Remove entry
          </button>
        </div>
        <button type="submit" class="btn-primary w-full" :disabled="saving || !calories">
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
import { useCalorieGoalsStore } from '../../stores/calorieGoals'
import type { DailyLog } from '../../types'
import * as api from '../../api/dailyLog'

const props = defineProps<{ initialDate?: string }>()
const emit = defineEmits<{ close: []; saved: [] }>()

const store = useDailyLogStore()
const dashboard = useDashboardStore()
const calorieGoalsStore = useCalorieGoalsStore()

const today = new Date().toISOString().split('T')[0]
const date = ref(props.initialDate ?? today)
const calorieTarget = computed(() => calorieGoalsStore.getTargetForDate(date.value))
const calories = ref<number | ''>('')
const existing = ref<DailyLog | null>(null)
const saving = ref(false)
const error = ref('')

async function loadExisting() {
  try { existing.value = await api.getByDate(date.value) }
  catch { existing.value = null }
  calories.value = existing.value?.caloriesKcal ?? ''
}

watch(date, loadExisting)
onMounted(loadExisting)

async function save() {
  saving.value = true
  error.value = ''
  try {
    await store.upsert(date.value, {
      date: date.value,
      caloriesKcal: Number(calories.value),
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

async function removeCalories() {
  if (confirm('Remove calories for this day?')) {
    await store.deleteCalories(date.value)
    await dashboard.fetch()
    emit('saved')
    emit('close')
  }
}
</script>
