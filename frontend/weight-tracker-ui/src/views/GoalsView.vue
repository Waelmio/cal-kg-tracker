<template>
  <main class="page">
    <h1>Goals</h1>
    <ErrorNote v-if="error" :message="error" :retry="reload" />
    <p v-else-if="loading && !data" class="muted" aria-busy="true">Loading…</p>

    <ErrorNote v-if="actionError" :message="actionError" />
    <template v-if="data">
      <WeightGoalSection :dash="data.dash" :goals="data.goals" @turn-off-target="turnOffTarget" />
      <div class="two">
        <CalorieTargetSection :current="data.dash.calorieTarget" :comparison="data.dash.targetComparison" :goals="data.calorieGoals" @turn-off="turnOffTarget" />
        <TdeeSection :current="data.dash.tdee" :entries="data.tdees" />
      </div>
    </template>
  </main>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import ErrorNote from '../components/ui/ErrorNote.vue'
import WeightGoalSection from '../components/goals/WeightGoalSection.vue'
import CalorieTargetSection from '../components/goals/CalorieTargetSection.vue'
import TdeeSection from '../components/goals/TdeeSection.vue'
import { useAppStore } from '../stores/app'
import { useAsyncData } from '../composables/useAsyncData'
import * as statsApi from '../api/stats'
import * as goalsApi from '../api/goals'
import * as calorieApi from '../api/calorieGoals'
import * as tdeeApi from '../api/tdee'

const app = useAppStore()
const { data, loading, error, reload } = useAsyncData(async () => {
  const [dash, goals, calorieGoals, tdees] = await Promise.all([statsApi.getDashboard(), goalsApi.getAll(), calorieApi.getAll(), tdeeApi.getEntries()])
  return { dash, goals, calorieGoals, tdees }
})

const actionError = ref('')
async function turnOffTarget() {
  actionError.value = ''
  try {
    await calorieApi.create(null)
    app.refresh()
  } catch (e) {
    actionError.value = (e as Error).message
  }
}
</script>

<style scoped>
.page { max-width: 960px; margin: 0 auto; padding: 20px 16px 24px; display: flex; flex-direction: column; gap: 16px; color: #1f2937; font-variant-numeric: tabular-nums; }
h1 { margin: 0; font-size: 20px; font-weight: 700; }
.two { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 380px), 1fr)); gap: 16px; align-items: start; }
.muted { color: #4b5563; }
</style>
