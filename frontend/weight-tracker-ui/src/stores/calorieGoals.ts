import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { CalorieGoal } from '../types'
import * as api from '../api/calorieGoals'

export const useCalorieGoalsStore = defineStore('calorieGoals', () => {
  const goals = ref<CalorieGoal[]>([])

  async function fetchAll() {
    try {
      goals.value = await api.getAll()
    } catch {
      // keep existing goals on error
    }
  }

  async function create(targetCalories: number) {
    const goal = await api.create(targetCalories)
    goals.value = [goal, ...goals.value]
    return goal
  }

  async function remove(id: number) {
    await api.remove(id)
    goals.value = goals.value.filter((g) => g.id !== id)
  }

  // Returns the calorie target that was active on a given date (yyyy-MM-dd).
  // Dates are compared as UTC midnight to stay consistent with server-side UTC dates.
  function getTargetForDate(date: string): number | null {
    const d = new Date(date + 'T00:00:00Z')
    const effective = goals.value.find((g) => {
      const goalDay = new Date(g.createdAt)
      goalDay.setUTCHours(0, 0, 0, 0)
      return goalDay <= d
    })
    return effective?.targetCalories ?? null
  }

  return { goals, fetchAll, create, remove, getTargetForDate }
})
