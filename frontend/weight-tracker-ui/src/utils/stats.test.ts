import { describe, it, expect } from 'vitest'
import { getTargetForDate, avg7DayWeight, estimateMonthlyTdee } from './stats'
import type { CalorieGoal, DailyLog } from '../types'

// ── helpers ──────────────────────────────────────────────────────────────────

function makeGoal(targetCalories: number, createdAt: string): CalorieGoal {
  return { id: Math.random(), targetCalories, createdAt }
}

function makeLog(date: string, weightKg: number | null = null, caloriesKcal: number | null = null): DailyLog {
  return {
    id: Math.random(), date, weightKg, caloriesKcal,
    notes: null, calorieTarget: null, isCheatDay: false,
    createdAt: date + 'T00:00:00.000Z', updatedAt: date + 'T00:00:00.000Z',
  }
}

function logMap(logs: DailyLog[]): Map<string, DailyLog> {
  return new Map(logs.map(l => [l.date, l]))
}

function anchor(dateStr: string): Date {
  return new Date(dateStr + 'T00:00:00Z')
}

// ── getTargetForDate ──────────────────────────────────────────────────────────

describe('getTargetForDate', () => {
  it('returns null when no goals exist', () => {
    expect(getTargetForDate('2024-06-15', [])).toBeNull()
  })

  it('returns the target of the only goal when it predates the query date', () => {
    const goals = [makeGoal(2000, '2024-01-01T00:00:00Z')]
    expect(getTargetForDate('2024-06-15', goals)).toBe(2000)
  })

  it('returns null when the only goal starts after the query date', () => {
    const goals = [makeGoal(2000, '2024-07-01T00:00:00Z')]
    expect(getTargetForDate('2024-06-15', goals)).toBeNull()
  })

  it('returns the most recent goal that is on or before the query date', () => {
    // goals sorted newest-first, as the store provides them
    const goals = [
      makeGoal(1800, '2024-06-01T00:00:00Z'),
      makeGoal(2000, '2024-01-01T00:00:00Z'),
    ]
    expect(getTargetForDate('2024-06-15', goals)).toBe(1800)
    expect(getTargetForDate('2024-05-31', goals)).toBe(2000)
  })

  it('matches on the exact day a goal was created', () => {
    const goals = [makeGoal(1900, '2024-06-15T12:00:00Z')]
    expect(getTargetForDate('2024-06-15', goals)).toBe(1900)
  })
})

// ── avg7DayWeight ─────────────────────────────────────────────────────────────

describe('avg7DayWeight', () => {
  it('returns null when no weight entries exist in the window', () => {
    const map = logMap([makeLog('2024-06-01', null)])
    expect(avg7DayWeight(anchor('2024-06-01'), map)).toBeNull()
  })

  it('returns the single entry when only one day in the window has weight', () => {
    const map = logMap([makeLog('2024-06-01', 80)])
    expect(avg7DayWeight(anchor('2024-06-01'), map)).toBe(80)
  })

  it('averages all entries in the [anchor-6 .. anchor] window', () => {
    const map = logMap([
      makeLog('2024-05-26', 80), // anchor - 6
      makeLog('2024-05-27', 82),
      makeLog('2024-06-01', 78), // anchor
    ])
    expect(avg7DayWeight(anchor('2024-06-01'), map)).toBeCloseTo((80 + 82 + 78) / 3)
  })

  it('ignores entries outside the 7-day window', () => {
    const map = logMap([
      makeLog('2024-05-25', 90), // anchor - 7, outside window
      makeLog('2024-06-01', 80), // anchor
    ])
    expect(avg7DayWeight(anchor('2024-06-01'), map)).toBe(80)
  })

  it('ignores days with null weight', () => {
    const map = logMap([
      makeLog('2024-05-31', null),
      makeLog('2024-06-01', 80),
    ])
    expect(avg7DayWeight(anchor('2024-06-01'), map)).toBe(80)
  })

  it('crosses a month boundary correctly', () => {
    // anchor = June 3 → window is May 28 – June 3
    const map = logMap([
      makeLog('2024-05-28', 80),
      makeLog('2024-06-03', 76),
    ])
    expect(avg7DayWeight(anchor('2024-06-03'), map)).toBeCloseTo((80 + 76) / 2)
  })
})

// ── estimateMonthlyTdee ───────────────────────────────────────────────────────

describe('estimateMonthlyTdee', () => {
  it('returns null when fewer than 5 calorie days', () => {
    const entries = [
      { date: '2024-06-01', weightKg: 80 },
      { date: '2024-06-20', weightKg: 79 },
    ]
    expect(estimateMonthlyTdee(2000, 4, entries)).toBeNull()
  })

  it('returns null when fewer than 2 weight entries', () => {
    const entries = [{ date: '2024-06-01', weightKg: 80 }]
    expect(estimateMonthlyTdee(2000, 10, entries)).toBeNull()
  })

  it('returns null when weight entries span fewer than 3 days', () => {
    const entries = [
      { date: '2024-06-01', weightKg: 80 },
      { date: '2024-06-02', weightKg: 79.8 },
    ]
    expect(estimateMonthlyTdee(2000, 10, entries)).toBeNull()
  })

  it('estimates TDEE correctly for stable weight (no change)', () => {
    // No weight change → TDEE = avg calories
    const entries = [
      { date: '2024-06-01', weightKg: 80 },
      { date: '2024-06-30', weightKg: 80 },
    ]
    expect(estimateMonthlyTdee(2200, 20, entries)).toBe(2200)
  })

  it('estimates TDEE correctly when weight is lost', () => {
    // Lost 1 kg over 10 days = -0.1 kg/day
    // TDEE = 2000 - (-0.1 * 7700) = 2000 + 770 = 2770
    const entries = [
      { date: '2024-06-01', weightKg: 80 },
      { date: '2024-06-11', weightKg: 79 },
    ]
    expect(estimateMonthlyTdee(2000, 10, entries)).toBe(2770)
  })

  it('estimates TDEE correctly when weight is gained', () => {
    // Gained 0.5 kg over 10 days = +0.05 kg/day
    // TDEE = 2500 - (0.05 * 7700) = 2500 - 385 = 2115
    const entries = [
      { date: '2024-06-01', weightKg: 80 },
      { date: '2024-06-11', weightKg: 80.5 },
    ]
    expect(estimateMonthlyTdee(2500, 10, entries)).toBe(2115)
  })

  it('sorts unsorted weight entries before computing', () => {
    // Same scenario as loss test, entries in reverse order
    const entries = [
      { date: '2024-06-11', weightKg: 79 },
      { date: '2024-06-01', weightKg: 80 },
    ]
    expect(estimateMonthlyTdee(2000, 10, entries)).toBe(2770)
  })

  it('does not mutate the input array when sorting', () => {
    const entries = [
      { date: '2024-06-11', weightKg: 79 },
      { date: '2024-06-01', weightKg: 80 },
    ]
    estimateMonthlyTdee(2000, 10, entries)
    expect(entries[0].date).toBe('2024-06-11')
  })
})
