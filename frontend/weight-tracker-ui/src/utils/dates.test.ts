import { describe, it, expect, vi, afterEach } from 'vitest'
import { addDays, localToday, toLocalDateString } from './dates'

afterEach(() => vi.useRealTimers())

describe('dates', () => {
  it('formats a local date without shifting to UTC', () => {
    // 00:30 local on 1 Nov: toISOString would give 31 Oct in any time zone east of UTC
    expect(toLocalDateString(new Date(2026, 10, 1, 0, 30))).toBe('2026-11-01')
    expect(toLocalDateString(new Date(2026, 10, 1, 23, 30))).toBe('2026-11-01')
  })

  it('localToday follows the system clock', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date(2026, 10, 24, 0, 5))
    expect(localToday()).toBe('2026-11-24')
  })

  it('adds days across month, year and DST boundaries', () => {
    expect(addDays('2026-11-30', 1)).toBe('2026-12-01')
    expect(addDays('2026-12-31', 1)).toBe('2027-01-01')
    expect(addDays('2026-03-01', -1)).toBe('2026-02-28')
    expect(addDays('2026-03-29', 1)).toBe('2026-03-30')
  })
})

import { mondayOf, addMonths, lastDayOfMonth, monthOf } from './dates'

describe('week and month helpers', () => {
  it('finds the Monday of a week', () => {
    expect(mondayOf('2026-11-24')).toBe('2026-11-23')   // Tuesday
    expect(mondayOf('2026-11-22')).toBe('2026-11-16')   // Sunday belongs to the week before
    expect(mondayOf('2026-11-23')).toBe('2026-11-23')
  })

  it('moves between months', () => {
    expect(addMonths('2026-11', 1)).toBe('2026-12')
    expect(addMonths('2026-12', 1)).toBe('2027-01')
    expect(addMonths('2026-01', -1)).toBe('2025-12')
    expect(lastDayOfMonth('2026-02')).toBe('2026-02-28')
    expect(monthOf('2026-11-24')).toBe('2026-11')
  })
})
