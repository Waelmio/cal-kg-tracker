<template>
  <main class="page">
    <h1>Goals</h1>
    <ErrorNote v-if="error" :message="error" :retry="reload" />
    <p v-else-if="loading && !data" class="muted" aria-busy="true">Loading…</p>

    <template v-if="data">
      <WeightGoalSection :dash="data.dash" :goals="data.goals" @changed="reload" @turn-off-target="turnOffTarget" />
      <CalorieTargetSection :current="data.dash.calorieTarget" :goals="data.calorieGoals" @changed="reload" @turn-off="turnOffTarget" />
      <TdeeSection :current="data.dash.tdee" @changed="reload" />
    </template>
  </main>
</template>

<script setup lang="ts">
import ErrorNote from '../components/ui/ErrorNote.vue'
import WeightGoalSection from '../components/goals/WeightGoalSection.vue'
import CalorieTargetSection from '../components/goals/CalorieTargetSection.vue'
import TdeeSection from '../components/goals/TdeeSection.vue'
import { useAppStore } from '../stores/app'
import { useAsyncData } from '../composables/useAsyncData'
import * as statsApi from '../api/stats'
import * as goalsApi from '../api/goals'
import * as calorieApi from '../api/calorieGoals'

const app = useAppStore()
const { data, loading, error, reload } = useAsyncData(async () => {
  const [dash, goals, calorieGoals] = await Promise.all([statsApi.getDashboard(), goalsApi.getAll(), calorieApi.getAll()])
  return { dash, goals, calorieGoals }
})

async function turnOffTarget() {
  try {
    await calorieApi.create(null)
    app.refresh()
  } catch { /* the section shows its own errors; a failed toggle leaves the target as it was */ }
}
</script>

<style scoped>
.page { max-width: 760px; margin: 0 auto; padding: 20px 16px 24px; display: flex; flex-direction: column; gap: 24px; }
h1 { margin: 0; font-size: 20px; }
.muted { color: #4b5563; }
</style>
