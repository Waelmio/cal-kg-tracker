import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useDailyLogStore } from './dailyLog'
import * as api from '../api/dailyLog'
import type { DailyLog } from '../types'

vi.mock('../api/dailyLog', () => ({
  getAll: vi.fn(),
  upsert: vi.fn(),
  deleteDay: vi.fn(),
  deleteWeight: vi.fn(),
  deleteCalories: vi.fn(),
}))

const makeLog = (date: string, weightKg: number | null = null, caloriesKcal: number | null = null): DailyLog => ({
  id: Math.random(),
  date,
  weightKg,
  caloriesKcal,
  notes: null,
  createdAt: '2024-01-01T00:00:00.000Z',
  updatedAt: '2024-01-01T00:00:00.000Z',
})

describe('useDailyLogStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  describe('upsert', () => {
    it('adds a new log and keeps list sorted by date descending', async () => {
      const store = useDailyLogStore()
      store.logs = [makeLog('2024-01-20'), makeLog('2024-01-10')]
      const newLog = makeLog('2024-01-15', 75)
      vi.mocked(api.upsert).mockResolvedValue(newLog)

      await store.upsert('2024-01-15', { date: '2024-01-15', weightKg: 75 })

      expect(store.logs).toHaveLength(3)
      expect(store.logs[0].date).toBe('2024-01-20')
      expect(store.logs[1].date).toBe('2024-01-15')
      expect(store.logs[2].date).toBe('2024-01-10')
    })

    it('updates an existing log in place', async () => {
      const store = useDailyLogStore()
      const existing = makeLog('2024-01-15', 74)
      store.logs = [existing]
      const updated = makeLog('2024-01-15', 75)
      vi.mocked(api.upsert).mockResolvedValue(updated)

      await store.upsert('2024-01-15', { date: '2024-01-15', weightKg: 75 })

      expect(store.logs).toHaveLength(1)
      expect(store.logs[0].weightKg).toBe(75)
    })

    it('returns the upserted log', async () => {
      const store = useDailyLogStore()
      const log = makeLog('2024-01-15', 75)
      vi.mocked(api.upsert).mockResolvedValue(log)

      const result = await store.upsert('2024-01-15', { date: '2024-01-15', weightKg: 75 })

      expect(result).toEqual(log)
    })
  })

  describe('deleteDay', () => {
    it('removes the log from the list', async () => {
      const store = useDailyLogStore()
      store.logs = [makeLog('2024-01-15'), makeLog('2024-01-14')]
      vi.mocked(api.deleteDay).mockResolvedValue(undefined)

      await store.deleteDay('2024-01-15')

      expect(store.logs).toHaveLength(1)
      expect(store.logs[0].date).toBe('2024-01-14')
    })
  })

  describe('deleteWeight', () => {
    it('removes log from list when API returns null (both fields now null)', async () => {
      const store = useDailyLogStore()
      store.logs = [makeLog('2024-01-15', 75, null)]
      vi.mocked(api.deleteWeight).mockResolvedValue(null)

      await store.deleteWeight('2024-01-15')

      expect(store.logs).toHaveLength(0)
    })

    it('updates log in list when API returns updated log (calories still present)', async () => {
      const store = useDailyLogStore()
      store.logs = [makeLog('2024-01-15', 75, 2000)]
      const updated = makeLog('2024-01-15', null, 2000)
      vi.mocked(api.deleteWeight).mockResolvedValue(updated)

      await store.deleteWeight('2024-01-15')

      expect(store.logs).toHaveLength(1)
      expect(store.logs[0].weightKg).toBeNull()
      expect(store.logs[0].caloriesKcal).toBe(2000)
    })
  })

  describe('deleteCalories', () => {
    it('removes log from list when API returns null (both fields now null)', async () => {
      const store = useDailyLogStore()
      store.logs = [makeLog('2024-01-15', null, 2000)]
      vi.mocked(api.deleteCalories).mockResolvedValue(null)

      await store.deleteCalories('2024-01-15')

      expect(store.logs).toHaveLength(0)
    })

    it('updates log in list when API returns updated log (weight still present)', async () => {
      const store = useDailyLogStore()
      store.logs = [makeLog('2024-01-15', 75, 2000)]
      const updated = makeLog('2024-01-15', 75, null)
      vi.mocked(api.deleteCalories).mockResolvedValue(updated)

      await store.deleteCalories('2024-01-15')

      expect(store.logs).toHaveLength(1)
      expect(store.logs[0].weightKg).toBe(75)
      expect(store.logs[0].caloriesKcal).toBeNull()
    })
  })

  describe('fetchAll', () => {
    it('sets logs from API response', async () => {
      const store = useDailyLogStore()
      const logs = [makeLog('2024-01-15'), makeLog('2024-01-14')]
      vi.mocked(api.getAll).mockResolvedValue(logs)

      await store.fetchAll()

      expect(store.logs).toEqual(logs)
      expect(store.loading).toBe(false)
      expect(store.error).toBeNull()
    })

    it('sets error on failure', async () => {
      const store = useDailyLogStore()
      vi.mocked(api.getAll).mockRejectedValue(new Error('Network error'))

      await store.fetchAll()

      expect(store.error).toBe('Network error')
      expect(store.loading).toBe(false)
    })
  })
})
