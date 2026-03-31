<template>
  <div class="max-w-3xl mx-auto px-4 py-6 space-y-5">
    <h1 class="text-xl font-bold text-gray-800">Log</h1>

    <!-- Date selector -->
    <div class="flex gap-2 flex-wrap">
      <button @click="selectedDate = today"
        class="text-sm px-3 py-1.5 rounded-lg transition-colors"
        :class="selectedDate === today ? 'bg-primary-600 text-white' : 'bg-gray-100 text-gray-600 hover:bg-gray-200'">
        Today
      </button>
      <button @click="selectedDate = yesterday"
        class="text-sm px-3 py-1.5 rounded-lg transition-colors"
        :class="selectedDate === yesterday ? 'bg-primary-600 text-white' : 'bg-gray-100 text-gray-600 hover:bg-gray-200'">
        Yesterday
      </button>
      <input v-model="selectedDate" type="date"
        class="text-sm border border-gray-200 rounded-lg px-3 py-1.5 text-gray-600 focus:outline-hidden focus:ring-2 focus:ring-primary-500" />
    </div>

    <div class="grid sm:grid-cols-2 gap-4">
      <!-- Weight card -->
      <div class="bg-white rounded-xl border border-gray-200 p-5">
        <div class="flex items-center justify-between mb-4">
          <p class="text-sm font-semibold text-gray-700">Weight</p>
          <span v-if="log?.weightKg != null" class="text-xs text-gray-400">
            Logged: {{ formatWeight(log.weightKg, unit) }}
          </span>
        </div>
        <div v-if="log?.weightKg != null" class="flex flex-col gap-3">
          <p class="text-3xl font-bold text-gray-800">{{ formatWeight(log.weightKg, unit) }}</p>
          <p class="text-xs invisible">placeholder</p>
          <div class="flex gap-2">
            <button @click="openModal('weight', selectedDate)" class="btn-secondary text-xs flex-1">Edit</button>
            <button @click="confirmRemoveWeight" class="text-xs text-red-400 hover:text-red-600 flex-1 border border-red-200 rounded-lg py-2 hover:bg-red-50 transition-colors">Remove</button>
          </div>
        </div>
        <button v-else @click="openModal('weight', selectedDate)" class="btn-primary w-full text-sm">
          + Log Weight
        </button>
      </div>

      <!-- Calories card -->
      <div class="bg-white rounded-xl border border-gray-200 p-5">
        <div class="flex items-center justify-between mb-4">
          <p class="text-sm font-semibold text-gray-700">Calories</p>
          <span v-if="calorieTarget" class="text-xs text-gray-400">Target: {{ calorieTarget }} kcal</span>
        </div>
        <div v-if="log?.caloriesKcal != null" class="flex flex-col gap-3">
          <p class="text-3xl font-bold text-gray-800">
            {{ log.caloriesKcal }}
            <span class="text-sm font-normal text-gray-400">kcal</span>
          </p>
          <p class="text-xs"
            :class="calorieTarget ? (log.caloriesKcal > calorieTarget ? 'text-red-400' : 'text-emerald-500') : 'invisible'">
            {{ calorieTarget
              ? (log.caloriesKcal > calorieTarget
                ? `${log.caloriesKcal - calorieTarget} kcal over target`
                : `${calorieTarget - log.caloriesKcal} kcal under target`)
              : 'placeholder' }}
          </p>
          <div class="flex gap-2">
            <button @click="openModal('calories', selectedDate)" class="btn-secondary text-xs flex-1">Edit</button>
            <button @click="confirmRemoveCalories" class="text-xs text-red-400 hover:text-red-600 flex-1 border border-red-200 rounded-lg py-2 hover:bg-red-50 transition-colors">Remove</button>
          </div>
        </div>
        <button v-else @click="openModal('calories', selectedDate)" class="btn-primary w-full text-sm">
          + Log Calories
        </button>
      </div>
    </div>

    <div v-if="log?.notes" class="bg-white rounded-xl border border-gray-200 p-4">
      <p class="text-xs text-gray-400 mb-1">Notes</p>
      <p class="text-sm text-gray-700">{{ log.notes }}</p>
    </div>

    <!-- Weight chart -->
    <WeightChart :logs="dailyLogStore.logs" :unit="unit" />

    <!-- History table -->
    <div class="bg-white rounded-xl border border-gray-200 p-5">
      <div v-if="dailyLogStore.loading" class="space-y-2">
        <div v-for="i in 8" :key="i" class="h-9 bg-gray-100 animate-pulse rounded-sm" />
      </div>
      <div v-else-if="dailyLogStore.logs.length === 0" class="text-gray-400 text-center py-10 text-sm">No entries yet.</div>
      <table v-else class="w-full text-sm">
        <thead>
          <tr class="text-left border-b border-gray-100">
            <th class="pb-2 text-xs font-medium text-gray-400 uppercase tracking-wide">Date</th>
            <th class="pb-2 text-xs font-medium text-gray-400 uppercase tracking-wide">Weight</th>
            <th class="pb-2 text-xs font-medium text-gray-400 uppercase tracking-wide">Calories</th>
            <th class="pb-2 text-xs font-medium text-gray-400 uppercase tracking-wide text-right">Actions</th>
          </tr>
        </thead>
        <tbody>
          <template v-for="month in grouped" :key="month.key">
            <tr>
              <td colspan="4" class="pt-4 pb-1">
                <span class="text-xs font-semibold text-gray-500 uppercase tracking-wide">{{ month.label }}</span>
              </td>
            </tr>
            <template v-for="week in month.weeks" :key="week.key">
              <tr>
                <td colspan="4" class="py-1 pl-2">
                  <span class="text-xs text-gray-400">W{{ week.weekNumber }} &middot; {{ week.range }}</span>
                </td>
              </tr>
              <tr v-for="entry in week.logs" :key="entry.id"
                class="border-b border-gray-50 hover:bg-gray-50 transition-colors">
                <td class="py-2.5 pl-2 text-gray-600 font-medium">{{ new Date(entry.date + 'T00:00:00Z').toLocaleDateString('en-GB', { timeZone: 'UTC' }) }}</td>
                <td class="py-2.5">
                  <button v-if="entry.weightKg != null"
                    @click="openModal('weight', entry.date)"
                    class="text-gray-800 font-semibold hover:text-primary-600 hover:underline">
                    {{ formatWeight(entry.weightKg, unit) }}
                  </button>
                  <button v-else @click="openModal('weight', entry.date)"
                    class="text-gray-300 hover:text-primary-500 text-xs">+ add</button>
                </td>
                <td class="py-2.5">
                  <button v-if="entry.caloriesKcal != null"
                    @click="openModal('calories', entry.date)"
                    class="text-gray-800 hover:text-primary-600 hover:underline">
                    {{ entry.caloriesKcal }} kcal
                  </button>
                  <button v-else @click="openModal('calories', entry.date)"
                    class="text-gray-300 hover:text-primary-500 text-xs">+ add</button>
                </td>
                <td class="py-2.5 text-right">
                  <button @click="confirmDelete(entry)" class="text-red-400 hover:underline text-xs">Delete</button>
                </td>
              </tr>
            </template>
          </template>
        </tbody>
      </table>
    </div>

    <WeightLogModal v-if="activeModal === 'weight'" :initial-date="modalDate"
      @close="activeModal = null" @saved="onSaved" />
    <CaloriesLogModal v-if="activeModal === 'calories'" :initial-date="modalDate"
      @close="activeModal = null" @saved="onSaved" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useSettingsStore } from '../stores/settings'
import { useCalorieGoalsStore } from '../stores/calorieGoals'
import { useDailyLogStore } from '../stores/dailyLog'
import type { DailyLog } from '../types'
import { formatWeight } from '../utils/units'
import { getISOWeek, getISOWeekYear, getWeekDates } from '../utils/weeks'
import WeightChart from '../components/dashboard/WeightChart.vue'
import WeightLogModal from '../components/LogModals/WeightLogModal.vue'
import CaloriesLogModal from '../components/LogModals/CaloriesLogModal.vue'

const settingsStore = useSettingsStore()
const calorieGoalsStore = useCalorieGoalsStore()
const dailyLogStore = useDailyLogStore()
const unit = computed(() => settingsStore.settings.preferredUnit)

const today = new Date().toISOString().split('T')[0]
const yesterday = new Date(Date.now() - 86400000).toISOString().split('T')[0]
const selectedDate = ref(today)
const calorieTarget = computed(() => calorieGoalsStore.getTargetForDate(selectedDate.value))
const log = computed(() => dailyLogStore.logs.find((l) => l.date === selectedDate.value) ?? null)

const activeModal = ref<'weight' | 'calories' | null>(null)
const modalDate = ref(today)

function openModal(type: 'weight' | 'calories', date: string) {
  modalDate.value = date
  activeModal.value = type
}

function onSaved() {
  // log store already updated in-place by upsert/delete actions
}

async function confirmRemoveWeight() {
  if (confirm('Remove this weigh-in?')) {
    await dailyLogStore.deleteWeight(selectedDate.value)
  }
}

async function confirmRemoveCalories() {
  if (confirm('Remove calories for this day?')) {
    await dailyLogStore.deleteCalories(selectedDate.value)
  }
}

async function confirmDelete(entry: DailyLog) {
  if (confirm(`Delete the entire entry for ${new Date(entry.date + 'T00:00:00Z').toLocaleDateString('en-GB', { timeZone: 'UTC' })}?`)) {
    await dailyLogStore.deleteDay(entry.date)
  }
}

// Grouping logic
const monthFmt = new Intl.DateTimeFormat('en-GB', { month: 'long', year: 'numeric', timeZone: 'UTC' })
const dayFmt = new Intl.DateTimeFormat('en-GB', { day: 'numeric', month: 'short', timeZone: 'UTC' })

interface WeekGroup { key: string; weekNumber: number; range: string; logs: DailyLog[] }
interface MonthGroup { key: string; label: string; weeks: WeekGroup[] }

const grouped = computed<MonthGroup[]>(() => {
  const months = new Map<string, MonthGroup>()
  const weeks = new Map<string, WeekGroup>()
  for (const entry of dailyLogStore.logs) {
    const d = new Date(entry.date + 'T00:00:00Z')
    const monthKey = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`
    if (!months.has(monthKey))
      months.set(monthKey, { key: monthKey, label: monthFmt.format(d), weeks: [] })
    const month = months.get(monthKey)!
    const weekNum = getISOWeek(d)
    const weekYear = getISOWeekYear(d)
    const weekKey = `${weekYear}-W${String(weekNum).padStart(2, '0')}`
    if (!weeks.has(weekKey)) {
      const dates = getWeekDates(weekYear, weekNum)
      const range = `${dayFmt.format(new Date(dates[0] + 'T00:00:00Z'))} – ${dayFmt.format(new Date(dates[6] + 'T00:00:00Z'))}`
      const week: WeekGroup = { key: weekKey, weekNumber: weekNum, range, logs: [] }
      weeks.set(weekKey, week)
      month.weeks.push(week)
    }
    weeks.get(weekKey)!.logs.push(entry)
  }
  return [...months.values()]
})

onMounted(async () => {
  await dailyLogStore.fetchAll()
})
</script>
