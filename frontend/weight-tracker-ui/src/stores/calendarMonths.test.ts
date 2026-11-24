import { describe, it, expect, vi, beforeEach } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useCalendarMonthsStore } from './calendarMonths'
import { useAppStore } from './app'
import * as statsApi from '../api/stats'
import type { Calendar } from '../types'

vi.mock('../api/stats', () => ({ getCalendar: vi.fn() }))
const page = (from: string) => ({ today: from }) as Calendar

beforeEach(() => {
  setActivePinia(createPinia())
  vi.clearAllMocks()
  vi.mocked(statsApi.getCalendar).mockImplementation(async (from) => page(from))
})
const asked = () => vi.mocked(statsApi.getCalendar).mock.calls

describe('calendar months', () => {
  it('loads the month first, then the ones before and after it', async () => {
    const store = useCalendarMonthsStore()
    await store.show('2026-11')
    expect(asked()).toEqual([['2026-10-26', '2026-12-06'], ['2026-09-28', '2026-11-08'], ['2026-11-30', '2027-01-10']])
  })

  it('a preloaded month is there at once, and only its other neighbour is asked for', async () => {
    const store = useCalendarMonthsStore()
    await store.show('2026-11')
    await vi.waitFor(() => expect(store.get('2026-12')).toBeDefined())
    vi.clearAllMocks()
    await store.show('2026-12')
    expect(asked()).toEqual([['2026-12-28', '2027-02-07']])
  })

  it('forgets every month when something changes', async () => {
    const store = useCalendarMonthsStore()
    await store.show('2026-11')
    useAppStore().refresh()
    expect(store.get('2026-11')).toBeUndefined()
    vi.clearAllMocks()
    await store.show('2026-11')
    expect(asked()).toHaveLength(3)
  })
})
