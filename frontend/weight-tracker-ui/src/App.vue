<template>
  <div class="min-h-screen bg-gray-50">
    <AppNav @open-settings="showSettings = true" />
    <RouterView />
    <SettingsModal v-if="showSettings" @close="showSettings = false" />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import AppNav from './components/layout/AppNav.vue'
import SettingsModal from './components/layout/SettingsModal.vue'
import { useSettingsStore } from './stores/settings'
import { useCalorieGoalsStore } from './stores/calorieGoals'
import { prefillWeek } from './api/dailyLog'
import { getISOWeek, getISOWeekYear } from './utils/weeks'

const showSettings = ref(false)

onMounted(async () => {
  const settingsStore = useSettingsStore()
  const calorieGoalsStore = useCalorieGoalsStore()
  await Promise.all([settingsStore.fetch(), calorieGoalsStore.fetchAll()])

  const today = new Date().toLocaleDateString('en-CA')
  const d = new Date(today + 'T00:00:00Z')
  const weekKey = `${getISOWeekYear(d)}-W${String(getISOWeek(d)).padStart(2, '0')}`
  if (localStorage.getItem('prefillWeek') !== weekKey) {
    await prefillWeek(today)
    localStorage.setItem('prefillWeek', weekKey)
  }
})
</script>
