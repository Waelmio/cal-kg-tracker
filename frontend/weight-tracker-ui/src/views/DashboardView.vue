<template>
  <main class="page">
    <div class="title-row">
      <h1>{{ longDate(app.today) }}</h1>
    </div>

    <ErrorNote v-if="error" :message="error" :retry="reload" />
    <DashboardTiles v-if="data" :stats="data" :unit="app.unit" />
    <div v-else-if="loading" class="grid-skeleton" aria-busy="true">
      <Tile v-for="i in 6" :key="i" loading />
    </div>

    <CalendarMonth @month-summary="summaryMonth = $event" />
    <AverageChart />
    <GoalCard v-if="data?.goal && data.goal.state !== 'none'" :goal="data.goal" :unit="app.unit" :today="app.today" :series="data.weightSeries" />

    <MonthSummaryModal v-if="summaryMonth" :month="summaryMonth" @close="summaryMonth = null" @change="summaryMonth = $event" />
  </main>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import ErrorNote from '../components/ui/ErrorNote.vue'
import Tile from '../components/ui/Tile.vue'
import DashboardTiles from '../components/dashboard/DashboardTiles.vue'
import CalendarMonth from '../components/dashboard/CalendarMonth.vue'
import AverageChart from '../components/dashboard/AverageChart.vue'
import MonthSummaryModal from '../components/dashboard/MonthSummaryModal.vue'
import GoalCard from '../components/goals/GoalCard.vue'
import { useAppStore } from '../stores/app'
import { useAsyncData } from '../composables/useAsyncData'
import * as statsApi from '../api/stats'
import { longDate } from '../utils/format'

const app = useAppStore()
const summaryMonth = ref<string | null>(null)
const { data, loading, error, reload } = useAsyncData(() => statsApi.getDashboard())
</script>

<style scoped>
.page { max-width: 960px; margin: 0 auto; padding: 20px 16px 24px; display: flex; flex-direction: column; gap: 16px; }
h1 { margin: 0; font-size: 20px; font-weight: 700; }
.grid-skeleton { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 290px), 1fr)); column-gap: 16px; row-gap: 0; }
</style>
