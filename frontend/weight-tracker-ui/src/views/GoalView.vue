<template>
  <div class="max-w-3xl mx-auto px-4 py-6 space-y-5">
    <h1 class="text-xl font-bold text-gray-800">Goal</h1>

    <!-- Active goal -->
    <div v-if="store.goal" class="bg-primary-50 border border-primary-100 rounded-xl p-5">
      <div class="flex items-start justify-between">
        <div>
          <p class="text-xs font-medium text-primary-600 uppercase tracking-wide">Active Goal</p>
          <p class="text-2xl font-bold text-gray-800 mt-1">{{ formatWeight(store.goal.targetWeightKg, unit) }}</p>
          <p class="text-sm text-gray-500 mt-0.5">by {{ new Date(store.goal.targetDate + 'T00:00:00Z').toLocaleDateString('en-GB', { timeZone: 'UTC' }) }}</p>
        </div>
        <div class="text-right text-xs text-gray-400">
          <p>Started {{ new Date(store.goal.startDate + 'T00:00:00Z').toLocaleDateString('en-GB', { timeZone: 'UTC' }) }}</p>
          <p v-if="store.goal.startingWeightKg">from {{ formatWeight(store.goal.startingWeightKg, unit) }}</p>
        </div>
      </div>
      <p v-if="requiredWeeklyLoss" class="text-sm mt-3"
        :class="goalOnTrack === true ? 'text-green-600' : goalOnTrack === false ? 'text-red-500' : 'text-gray-500'">
        Requires losing <span class="font-medium">{{ requiredWeeklyLoss }} {{ unit }}/week</span>
      </p>
      <p v-if="store.goal.notes" class="text-sm text-gray-500 mt-3 italic">{{ store.goal.notes }}</p>
      <div v-if="dashboard.data?.bmi" class="mt-3 pt-3 border-t border-primary-100 grid grid-cols-3 items-center">
        <div class="flex items-baseline gap-2">
          <span class="text-xs font-medium text-primary-600 uppercase tracking-wide">BMI</span>
          <span class="text-lg font-bold text-gray-800">{{ dashboard.data.bmi }}</span>
          <span class="text-sm font-medium" :class="bmiLabel(dashboard.data.bmi).color">{{ bmiLabel(dashboard.data.bmi).text }}</span>
        </div>
        <span v-if="goalBmi != null" class="text-2xl text-gray-400 text-center">→</span>
        <div v-if="goalBmi != null" class="flex items-baseline gap-2 justify-end">
          <span class="text-lg font-bold text-gray-800">{{ goalBmi }}</span>
          <span class="text-sm font-medium" :class="bmiLabel(goalBmi).color">{{ bmiLabel(goalBmi).text }}</span>
        </div>
      </div>
    </div>

    <!-- Calorie projection summary -->
    <div v-if="calorieGoalsStore.goals.length > 0" class="bg-orange-50 border border-orange-100 rounded-xl p-5 space-y-3">
      <div class="flex items-start justify-between">
        <div>
          <p class="text-xs font-medium text-orange-600 uppercase tracking-wide">Calorie Projections</p>
          <p class="text-2xl font-bold text-gray-800 mt-1">{{ calorieGoalsStore.goals[0].targetCalories }} kcal/day</p>
          <p class="text-sm text-gray-500 mt-0.5">daily calorie target</p>
        </div>
        <div v-if="settingsStore.settings.tdeeKcal" class="text-right text-xs text-gray-400">
          <p>TDEE {{ settingsStore.settings.tdeeKcal }} kcal/day</p>
        </div>
      </div>

      <template v-if="settingsStore.settings.tdeeKcal">
        <!-- vs BMR -->
        <div class="pt-2 border-t border-orange-100">
          <div class="flex items-start justify-between">
            <div>
              <p class="text-xs font-medium text-gray-600">vs TDEE ({{ settingsStore.settings.tdeeKcal }} kcal/day)</p>
              <p class="text-xs text-gray-500 mt-0.5">
                {{ projectionVsTdee.label }}
                <span :class="projectionVsTdee.deficit >= 0 ? 'text-green-600' : 'text-red-500'" class="font-medium">
                  {{ projectionVsTdee.deficit >= 0 ? '−' : '+' }}{{ Math.abs(projectionVsTdee.deficit) }} kcal/day
                </span>
              </p>
            </div>
            <div class="text-right">
              <p :class="projectionVsTdee.kgPerWeek >= 0 ? 'text-green-600' : 'text-red-500'" class="text-sm font-bold">
                {{ projectionVsTdee.kgPerWeek >= 0 ? '−' : '+' }}{{ formatProjectedWeight(Math.abs(projectionVsTdee.kgPerWeek)) }}/week
              </p>
            </div>
          </div>
        </div>

        <!-- vs weekly avg -->
        <div v-if="dashboard.data?.weeklyAvgCalories != null" class="pt-2 border-t border-orange-100">
          <div class="flex items-start justify-between">
            <div>
              <p class="text-xs font-medium text-gray-600">vs this week's avg ({{ Math.round(dashboard.data.weeklyAvgCalories) }} kcal/day)</p>
              <p class="text-xs text-gray-500 mt-0.5">
                {{ projectionVsWeeklyAvg.label }}
                <span :class="projectionVsWeeklyAvg.deficit >= 0 ? 'text-green-600' : 'text-red-500'" class="font-medium">
                  {{ projectionVsWeeklyAvg.deficit >= 0 ? '−' : '+' }}{{ Math.abs(projectionVsWeeklyAvg.deficit) }} kcal/day
                </span>
              </p>
            </div>
            <div class="text-right">
              <p :class="projectionVsWeeklyAvg.kgPerWeek >= 0 ? 'text-green-600' : 'text-red-500'" class="text-sm font-bold">
                {{ projectionVsWeeklyAvg.kgPerWeek >= 0 ? '−' : '+' }}{{ formatProjectedWeight(Math.abs(projectionVsWeeklyAvg.kgPerWeek)) }}/week
              </p>
            </div>
          </div>
        </div>
      </template>

      <p v-else class="text-xs text-gray-400 pt-2 border-t border-orange-100">
        Set your TDEE below to see weight projections.
      </p>
    </div>

    <!-- Daily calorie target -->
    <div class="bg-white rounded-xl border border-gray-200 p-5">
      <p class="text-sm font-semibold text-gray-700 mb-4">Daily Calorie Target</p>
      <form @submit.prevent="saveCalorieTarget" class="flex gap-3 items-end">
        <div class="flex-1">
          <label class="label">Target (kcal)</label>
          <input v-model.number="calorieTargetInput" type="number" min="500" max="10000"
            class="input" placeholder="e.g. 1800" />
        </div>
        <button type="submit" class="btn-primary" :disabled="savingCalories">
          {{ savingCalories ? 'Saving…' : 'Set Target' }}
        </button>
      </form>

      <div v-if="calorieGoalsStore.goals.length > 0" class="mt-3">
        <p class="text-xs text-gray-400">
          Current target: {{ calorieGoalsStore.goals[0].targetCalories }} kcal/day
        </p>

        <div v-if="calorieGoalsStore.goals.length > 1" class="mt-2">
          <button @click="showHistory = !showHistory"
            class="text-xs text-primary-600 hover:text-primary-700 hover:underline">
            {{ showHistory ? 'Hide history' : `Show history (${calorieGoalsStore.goals.length - 1} older)` }}
          </button>
          <div v-if="showHistory" class="mt-2 border-t border-gray-100 pt-2 space-y-1">
            <div v-for="goal in calorieGoalsStore.goals.slice(1)" :key="goal.id"
              class="flex items-center justify-between text-xs text-gray-400">
              <span>{{ goal.targetCalories }} kcal/day</span>
              <span>set {{ new Date(goal.createdAt).toLocaleDateString('en-GB') }}</span>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Total Daily Energy Expenditure -->
    <div class="bg-white rounded-xl border border-gray-200 p-5">
      <p class="text-sm font-semibold text-gray-700 mb-4">Total Daily Energy Expenditure</p>
      <p class="text-xs text-gray-500 mb-4">
        Your TDEE is the total number of calories your body burns in a day. Used to estimate your daily energy needs.
      </p>
      <form @submit.prevent="saveTdee" class="flex gap-3 items-end">
        <div class="flex-1">
          <label class="label">TDEE (kcal/day)</label>
          <input v-model.number="tdeeInput" type="number" min="500" max="10000"
            class="input" placeholder="e.g. 1700" />
        </div>
        <button type="submit" class="btn-primary" :disabled="savingTdee">
          {{ savingTdee ? 'Saving…' : 'Save' }}
        </button>
      </form>
      <p v-if="settingsStore.settings.tdeeKcal" class="text-xs text-gray-400 mt-3">
        Current TDEE: {{ settingsStore.settings.tdeeKcal }} kcal/day
      </p>

      <!-- Compute from logs -->
      <div class="mt-4 pt-4 border-t border-gray-100">
        <div class="flex gap-3 items-center">
          <select v-model.number="tdeeComputeDays" class="input flex-1">
            <option :value="30">Last 30 days</option>
            <option :value="60">Last 60 days</option>
            <option :value="90">Last 90 days</option>
            <option :value="180">Last 180 days</option>
          </select>
          <button @click="computeTdee" class="btn-secondary" :disabled="computingTdee">
            {{ computingTdee ? 'Computing…' : 'Compute from logs' }}
          </button>
        </div>
        <p v-if="tdeeComputeError" class="text-red-500 text-xs mt-2">{{ tdeeComputeError }}</p>
        <div v-if="tdeeComputed" class="mt-3 bg-gray-50 rounded-lg p-3 space-y-1">
          <div class="flex items-center justify-between">
            <span class="text-sm font-medium text-gray-700">Estimated TDEE</span>
            <span class="text-sm font-bold text-gray-900">{{ tdeeComputed.estimatedTdeeKcal }} kcal/day</span>
          </div>
          <div class="flex items-center justify-between text-xs text-gray-400">
            <span>Avg. daily intake</span>
            <span>{{ tdeeComputed.avgDailyCaloriesKcal }} kcal</span>
          </div>
          <div class="flex items-center justify-between text-xs text-gray-400">
            <span>Weight trend</span>
            <span>{{ tdeeComputed.weightTrendKgPerDay > 0 ? '+' : '' }}{{ (tdeeComputed.weightTrendKgPerDay * 1000).toFixed(1) }} g/day</span>
          </div>
          <div class="flex items-center justify-between text-xs text-gray-400">
            <span>Based on</span>
            <span>{{ tdeeComputed.weightDataPoints }} weight · {{ tdeeComputed.calorieDataPoints }} calorie entries</span>
          </div>
          <button @click="applyComputedTdee" class="btn-secondary w-full mt-2 text-xs">
            Use this value
          </button>
        </div>
      </div>
    </div>

    <!-- Set / replace goal -->
    <div class="bg-white rounded-xl border border-gray-200 p-5">
      <p class="text-sm font-semibold text-gray-700 mb-4">
        {{ store.goal ? 'Set a New Goal' : 'Set Your Goal' }}
      </p>
      <form @submit.prevent="submit" class="space-y-4">
        <div>
          <label class="label">Target Weight ({{ unit }})</label>
          <input v-model.number="targetDisplay" type="number" step="0.1" min="1" max="700"
            class="input" required :placeholder="`e.g. ${unit === 'kg' ? '70' : '154'}`" />
        </div>
        <div>
          <label class="label">Target Date</label>
          <input v-model="targetDate" type="date" class="input" required :min="tomorrow" />
        </div>
        <div>
          <label class="label">Notes (optional)</label>
          <textarea v-model="notes" class="input" rows="2" />
        </div>
        <button type="submit" class="btn-primary w-full" :disabled="saving">
          {{ saving ? 'Saving…' : (store.goal ? 'Replace Goal' : 'Set Goal') }}
        </button>
        <p v-if="error" class="text-red-500 text-sm">{{ error }}</p>
      </form>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useGoalsStore } from '../stores/goals'
import { useDashboardStore } from '../stores/dashboard'
import { useSettingsStore } from '../stores/settings'
import { useCalorieGoalsStore } from '../stores/calorieGoals'
import { toKg, formatWeight, bmiLabel } from '../utils/units'
import { getComputedTdee } from '../api/tdee'
import type { TdeeComputation } from '../types'

const store = useGoalsStore()
const dashboard = useDashboardStore()
const settingsStore = useSettingsStore()
const calorieGoalsStore = useCalorieGoalsStore()
const unit = computed(() => settingsStore.settings.preferredUnit)

function kgPerWeekFromDeficit(deficit: number) {
  return (deficit * 7) / 7700
}

function formatProjectedWeight(kg: number): string {
  const value = unit.value === 'lbs' ? kg * 2.20462 : kg
  return `${value.toFixed(2)} ${unit.value}`
}

function buildProjection(bmr: number, intake: number) {
  const deficit = Math.round(bmr - intake)
  const kgPerWeek = kgPerWeekFromDeficit(deficit)
  const label = deficit >= 0 ? 'Deficit' : 'Surplus'
  return { deficit, kgPerWeek, label }
}

const projectionVsTdee = computed(() => {
  const bmr = Number(settingsStore.settings.tdeeKcal)
  const target = calorieGoalsStore.goals[0]?.targetCalories
  if (!bmr || !target) return { deficit: 0, kgPerWeek: 0, label: '' }
  return buildProjection(bmr, target)
})

const projectionVsWeeklyAvg = computed(() => {
  const bmr = Number(settingsStore.settings.tdeeKcal)
  const avg = dashboard.data?.weeklyAvgCalories
  if (!bmr || avg == null) return { deficit: 0, kgPerWeek: 0, label: '' }
  return buildProjection(bmr, avg)
})

const requiredWeeklyLossKg = computed<number | null>(() => {
  const goal = store.goal
  const currentWeight = dashboard.data?.currentWeightKg
  if (!goal || currentWeight == null) return null
  const remaining = currentWeight - goal.targetWeightKg
  if (remaining <= 0) return null
  const msPerWeek = 7 * 24 * 60 * 60 * 1000
  const weeksLeft = (new Date(goal.targetDate).getTime() - Date.now()) / msPerWeek
  if (weeksLeft <= 0) return null
  return remaining / weeksLeft
})

const requiredWeeklyLoss = computed(() => {
  if (requiredWeeklyLossKg.value == null) return null
  return unit.value === 'lbs'
    ? (requiredWeeklyLossKg.value * 2.20462).toFixed(2)
    : requiredWeeklyLossKg.value.toFixed(2)
})

const goalBmi = computed<number | null>(() => {
  const h = settingsStore.settings.heightCm
  const targetKg = store.goal?.targetWeightKg
  if (!h || !targetKg) return null
  const hm = h / 100
  return Math.round((targetKg / (hm * hm)) * 10) / 10
})

const goalOnTrack = computed<boolean | null>(() => {
  if (requiredWeeklyLossKg.value == null || !settingsStore.settings.tdeeKcal) return null
  return projectionVsTdee.value.kgPerWeek >= requiredWeeklyLossKg.value
})

const tomorrow = new Date(Date.now() + 86400000).toISOString().split('T')[0]
const targetDisplay = ref<number | ''>('')
const targetDate = ref(tomorrow)
const notes = ref('')
const saving = ref(false)
const error = ref('')

const calorieTargetInput = ref<number | ''>(calorieGoalsStore.goals[0]?.targetCalories ?? '')
const savingCalories = ref(false)
const showHistory = ref(false)

const tdeeInput = ref<number | ''>(settingsStore.settings.tdeeKcal ?? '')
const savingTdee = ref(false)
const tdeeComputeDays = ref(90)
const computingTdee = ref(false)
const tdeeComputed = ref<TdeeComputation | null>(null)
const tdeeComputeError = ref('')

async function computeTdee() {
  computingTdee.value = true
  tdeeComputeError.value = ''
  tdeeComputed.value = null
  try {
    tdeeComputed.value = await getComputedTdee(tdeeComputeDays.value)
  } catch (e) {
    tdeeComputeError.value = (e as Error).message
  } finally {
    computingTdee.value = false
  }
}

function applyComputedTdee() {
  if (tdeeComputed.value) tdeeInput.value = tdeeComputed.value.estimatedTdeeKcal
}

async function saveTdee() {
  if (!tdeeInput.value) return
  savingTdee.value = true
  try {
    await settingsStore.save({
      ...settingsStore.settings,
      tdeeKcal: Number(tdeeInput.value),
    })
  } finally {
    savingTdee.value = false
  }
}

async function saveCalorieTarget() {
  if (!calorieTargetInput.value) return
  savingCalories.value = true
  try {
    await calorieGoalsStore.create(Number(calorieTargetInput.value))
    await dashboard.fetch()
  } finally {
    savingCalories.value = false
  }
}

async function submit() {
  if (!targetDisplay.value) return
  saving.value = true
  error.value = ''
  try {
    await store.create({
      targetWeightKg: toKg(Number(targetDisplay.value), unit.value),
      targetDate: targetDate.value,
      notes: notes.value || undefined,
    })
    await dashboard.fetch()
    targetDisplay.value = ''
    notes.value = ''
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    saving.value = false
  }
}


onMounted(async () => {
  await Promise.all([store.fetchActive(), dashboard.fetch()])
  calorieTargetInput.value = calorieGoalsStore.goals[0]?.targetCalories ?? ''
  tdeeInput.value = settingsStore.settings.tdeeKcal ?? ''
})
</script>
