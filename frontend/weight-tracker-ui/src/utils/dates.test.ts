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
