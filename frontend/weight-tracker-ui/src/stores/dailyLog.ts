import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { DailyLog, UpsertDailyLogRequest } from '../types'
import * as api from '../api/dailyLog'

export const useDailyLogStore = defineStore('dailyLog', () => {
  const logs = ref<DailyLog[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  async function fetchAll(params?: { from?: string; to?: string; limit?: number }) {
    loading.value = true
    error.value = null
    try {
      logs.value = await api.getAll(params)
    } catch (e) {
      error.value = (e as Error).message
    } finally {
      loading.value = false
    }
  }

  async function upsert(date: string, data: UpsertDailyLogRequest) {
    const log = await api.upsert(date, data)
    const idx = logs.value.findIndex((l) => l.date === date)
    if (idx !== -1) logs.value[idx] = log
    else logs.value.unshift(log)
    logs.value.sort((a, b) => b.date.localeCompare(a.date))
    return log
  }

  async function deleteDay(date: string) {
    await api.deleteDay(date)
    logs.value = logs.value.filter((l) => l.date !== date)
  }

  async function deleteWeight(date: string) {
    const updated = await api.deleteWeight(date)
    if (updated === null) {
      logs.value = logs.value.filter((l) => l.date !== date)
    } else {
      const idx = logs.value.findIndex((l) => l.date === date)
      if (idx !== -1) logs.value[idx] = updated
    }
  }

  async function deleteCalories(date: string) {
    const updated = await api.deleteCalories(date)
    if (updated === null) {
      logs.value = logs.value.filter((l) => l.date !== date)
    } else {
      const idx = logs.value.findIndex((l) => l.date === date)
      if (idx !== -1) logs.value[idx] = updated
    }
  }

  async function toggleCheatDay(date: string, isCheatDay: boolean) {
    const updated = isCheatDay ? await api.setCheatDay(date) : await api.clearCheatDay(date)
    if (!isCheatDay && updated.id === 0) {
      // Row was deleted (no weight/calories left)
      logs.value = logs.value.filter((l) => l.date !== date)
    } else {
      const idx = logs.value.findIndex((l) => l.date === date)
      if (idx !== -1) logs.value[idx] = updated
      else {
        logs.value.unshift(updated)
        logs.value.sort((a, b) => b.date.localeCompare(a.date))
      }
    }
  }

  return { logs, loading, error, fetchAll, upsert, deleteDay, deleteWeight, deleteCalories, toggleCheatDay }
})
