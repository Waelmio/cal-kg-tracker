import { defineStore } from 'pinia'
import * as statsApi from '../api/stats'
import { useKeyedCache } from '../composables/useKeyedCache'
import { addDays, addMonths, mondayOf } from '../utils/dates'
import type { Calendar } from '../types'

/** First day of a month's calendar. It always has six weeks, so the page doesn't jump from month to month. */
export const calendarStart = (month: string) => mondayOf(`${month}-01`)

/** Calendar pages by month (yyyy-MM). Showing one preloads the months before and after it. */
export const useCalendarMonthsStore = defineStore('calendarMonths', () => {
  const cache = useKeyedCache<Calendar>((month) => {
    const from = calendarStart(month)
    return statsApi.getCalendar(from, addDays(from, 41))
  })

  async function show(month: string) {
    await cache.load(month)
    cache.load(addMonths(month, -1))
    cache.load(addMonths(month, 1))
  }

  return { error: cache.error, show, get: cache.get }
})
