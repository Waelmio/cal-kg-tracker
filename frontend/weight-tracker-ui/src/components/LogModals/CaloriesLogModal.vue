<template>
  <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/40" @mousedown.self="$emit('close')">
    <div class="bg-white rounded-xl shadow-xl w-full max-w-sm p-6">
      <div class="flex items-center justify-between mb-5">
        <h2 class="text-lg font-semibold text-gray-800">Log Calories</h2>
        <button @click="$emit('close')" class="text-gray-400 hover:text-gray-600 text-xl leading-none cursor-pointer">✕</button>
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
            class="text-xs text-red-400 hover:text-red-600 hover:underline cursor-pointer">
            Remove entry
          </button>
        </div>
        <!-- More options -->
        <div>
          <button type="button" @click="showMore = !showMore"
            class="text-xs text-gray-400 hover:text-gray-600 cursor-pointer">
            {{ showMore ? '− Less' : '+ More' }}
          </button>
          <div v-if="showMore" class="flex items-center justify-between pt-3">
            <div>
              <p class="text-sm font-medium" :class="isCheatDay ? 'text-orange-600' : 'text-gray-700'">Cheat Day</p>
              <p class="text-xs" :class="isCheatDay ? 'text-orange-400' : 'text-gray-400'">
                {{ isCheatDay ? 'Target set to TDEE for today' : 'Override target with TDEE' }}
              </p>
            </div>
            <button type="button" @click="isCheatDay = !isCheatDay"
              class="relative inline-flex h-6 w-11 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 focus:outline-hidden"
              :class="isCheatDay ? 'bg-orange-400' : 'bg-gray-200'">
              <span class="pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow ring-0 transition duration-200"
                :class="isCheatDay ? 'translate-x-5' : 'translate-x-0'" />
            </button>
          </div>
        </div>

        <button type="submit" class="btn-primary w-full cursor-pointer" :disabled="saving || !calories">
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
import { useCalorieGoalsStore } from '../../stores/calorieGoals'
import { useSettingsStore } from '../../stores/settings'
import type { DailyLog } from '../../types'
import * as api from '../../api/dailyLog'

const props = defineProps<{ initialDate?: string }>()
const emit = defineEmits<{ close: []; saved: [] }>()

const store = useDailyLogStore()
const calorieGoalsStore = useCalorieGoalsStore()
const settingsStore = useSettingsStore()

const today = new Date().toISOString().split('T')[0]
const date = ref(props.initialDate ?? today)
const isCheatDay = ref(false)
const showMore = ref(false)
const calorieTarget = computed(() =>
  isCheatDay.value
    ? (settingsStore.settings.tdeeKcal ?? calorieGoalsStore.getTargetForDate(date.value))
    : calorieGoalsStore.getTargetForDate(date.value)
)
const calories = ref<number | ''>('')
const existing = ref<DailyLog | null>(null)
const saving = ref(false)
const error = ref('')

async function loadExisting() {
  try { existing.value = await api.getByDate(date.value) }
  catch { existing.value = null }
  calories.value = existing.value?.caloriesKcal ?? calorieTarget.value ?? ''
  isCheatDay.value = existing.value?.isCheatDay ?? false
  if (isCheatDay.value) showMore.value = true
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
    const wasCheatDay = existing.value?.isCheatDay ?? false
    if (isCheatDay.value !== wasCheatDay) {
      await store.toggleCheatDay(date.value, isCheatDay.value)
    }
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
    emit('saved')
    emit('close')
  }
}
</script>
