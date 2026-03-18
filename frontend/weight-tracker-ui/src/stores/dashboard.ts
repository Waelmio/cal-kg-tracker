import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { DashboardData } from '../types'
import { getDashboard } from '../api/dashboard'

export const useDashboardStore = defineStore('dashboard', () => {
  const data = ref<DashboardData | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  async function fetch() {
    loading.value = true
    error.value = null
    try {
      data.value = await getDashboard()
    } catch (e) {
      error.value = (e as Error).message
    } finally {
      loading.value = false
    }
  }

  return { data, loading, error, fetch }
})
