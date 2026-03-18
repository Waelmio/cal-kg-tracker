import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useCalorieGoalsStore } from './calorieGoals'
import * as api from '../api/calorieGoals'

vi.mock('../api/calorieGoals', () => ({
  getAll: vi.fn(),
  create: vi.fn(),
  remove: vi.fn(),
}))

describe('useCalorieGoalsStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  describe('getTargetForDate', () => {
    it('returns null when no goals are set', () => {
      const store = useCalorieGoalsStore()

      expect(store.getTargetForDate('2024-01-15')).toBeNull()
    })

    it('returns the most recent goal active on the given date', () => {
      const store = useCalorieGoalsStore()
      store.goals = [
        { id: 2, targetCalories: 1800, createdAt: '2024-01-10T00:00:00.000Z' },
        { id: 1, targetCalories: 2000, createdAt: '2024-01-05T00:00:00.000Z' },
      ]

      expect(store.getTargetForDate('2024-01-15')).toBe(1800)
    })

    it('falls back to an earlier goal when a newer goal was created after the date', () => {
      const store = useCalorieGoalsStore()
      store.goals = [
        { id: 2, targetCalories: 1800, createdAt: '2024-01-10T00:00:00.000Z' },
        { id: 1, targetCalories: 2000, createdAt: '2024-01-05T00:00:00.000Z' },
      ]

      expect(store.getTargetForDate('2024-01-08')).toBe(2000)
    })

    it('returns null when queried date is before all goals', () => {
      const store = useCalorieGoalsStore()
      store.goals = [
        { id: 2, targetCalories: 1800, createdAt: '2024-01-10T00:00:00.000Z' },
        { id: 1, targetCalories: 2000, createdAt: '2024-01-05T00:00:00.000Z' },
      ]

      expect(store.getTargetForDate('2024-01-01')).toBeNull()
    })

    it('returns goal created on the same day as the queried date', () => {
      const store = useCalorieGoalsStore()
      store.goals = [
        { id: 1, targetCalories: 2000, createdAt: '2024-01-15T12:00:00.000Z' },
      ]

      expect(store.getTargetForDate('2024-01-15')).toBe(2000)
    })
  })

  describe('create', () => {
    it('prepends new goal to the list', async () => {
      const store = useCalorieGoalsStore()
      store.goals = [{ id: 1, targetCalories: 2000, createdAt: '2024-01-10T00:00:00.000Z' }]
      const newGoal = { id: 2, targetCalories: 1800, createdAt: '2024-01-15T00:00:00.000Z' }
      vi.mocked(api.create).mockResolvedValue(newGoal)

      await store.create(1800)

      expect(store.goals[0]).toEqual(newGoal)
      expect(store.goals).toHaveLength(2)
    })
  })

  describe('remove', () => {
    it('removes goal from list by id', async () => {
      const store = useCalorieGoalsStore()
      store.goals = [
        { id: 1, targetCalories: 2000, createdAt: '2024-01-10T00:00:00.000Z' },
        { id: 2, targetCalories: 1800, createdAt: '2024-01-05T00:00:00.000Z' },
      ]
      vi.mocked(api.remove).mockResolvedValue(undefined)

      await store.remove(1)

      expect(store.goals).toHaveLength(1)
      expect(store.goals[0].id).toBe(2)
    })
  })
})
