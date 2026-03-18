import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { Goal, CreateGoalRequest } from '../types'
import * as api from '../api/goals'

export const useGoalsStore = defineStore('goals', () => {
  const goal = ref<Goal | null>(null)
  const loading = ref(false)

  async function fetchActive() {
    loading.value = true
    try {
      goal.value = await api.getActive()
    } catch {
      goal.value = null
    } finally {
      loading.value = false
    }
  }

  async function create(data: CreateGoalRequest) {
    goal.value = await api.create(data)
    return goal.value
  }

  async function remove(id: number) {
    await api.remove(id)
    if (goal.value?.id === id) goal.value = null
  }

  return { goal, loading, fetchActive, create, remove }
})
