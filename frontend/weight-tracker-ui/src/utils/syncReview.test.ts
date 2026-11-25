import { describe, it, expect } from 'vitest'
import { applySummary, choiceSummary, groupByWeek, rowKey, syncSummary } from './syncReview'
import type { SyncRow } from '../types'

const row = (date: string, field: SyncRow['field'] = 'Calories'): SyncRow =>
  ({ date, field, kind: 'Conflict', appValue: 2000, value: 1900, setInAdvance: false, imported: false, suggested: 'Keep' })

describe('groupByWeek', () => {
  it('puts the most recent week first, and keeps the days of a week in order', () => {
    const weeks = groupByWeek([row('2026-11-15'), row('2026-11-16'), row('2026-11-16', 'Weight'), row('2026-11-22')])
    expect(weeks.map((w) => [w.monday, w.title, w.rows.map(rowKey)])).toEqual([
      ['2026-11-16', '16 Nov to 22 Nov 2026', ['2026-11-16:Calories', '2026-11-16:Weight', '2026-11-22:Calories']],
      ['2026-11-09', '9 Nov to 15 Nov 2026', ['2026-11-15:Calories']],
    ])
  })
  it('is empty without rows', () => expect(groupByWeek([])).toEqual([]))
})

describe('summaries', () => {
  it('counts the choices', () => {
    expect(choiceSummary([row('2026-11-15'), row('2026-11-16')], { '2026-11-15:Calories': 'Replace', '2026-11-16:Calories': 'Keep' })).toBe('1 to write, 1 kept')
  })
  it('says what a sync did', () => {
    const r = (cw: number, ww: number, kept = 0) => ({ from: '', to: '', calories: { written: cw, unchanged: 5, kept }, weight: { written: ww, unchanged: 0, kept: 0 } })
    expect(syncSummary(r(0, 0))).toBe('Synced: nothing new.')
    expect(syncSummary(r(12, 1))).toBe('Synced: 12 calorie values and 1 weight updated.')
    expect(syncSummary(r(1, 0, 2))).toBe('Synced: 1 calorie value and 0 weights updated, 2 values you logged yourself left alone.')
  })
  it('says what was applied', () => {
    expect(applySummary({ added: 3, replaced: 1, kept: 4, changed: 0 })).toBe('3 added, 1 replaced, 4 kept.')
    expect(applySummary({ added: 0, replaced: 0, kept: 0, changed: 1 })).toContain('1 value skipped, changed in the meantime.')
  })
})
