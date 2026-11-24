import { describe, it, expect } from 'vitest'
import { buildGoalChart, niceTicks } from './chart'

const base = {
  today: '2026-11-24', start: '2026-09-10', end: '2027-02-12', targetKg: 78, startKg: 88.6,
  series: [
    { date: '2026-09-10', kg: 88.6, avg7: 88.6 },
    { date: '2026-10-10', kg: 87, avg7: 87.2 },
    { date: '2026-11-24', kg: 84, avg7: 84.1 },
  ],
  projection: { date: '2027-03-07', kg: 78 },
}

describe('buildGoalChart', () => {
  it('puts x on a real time axis: equal day gaps are equal distances', () => {
    const g = buildGoalChart({ ...base, end: '2026-12-31' })
    const a = g.x('2026-10-01') - g.x('2026-09-10')
    const b = g.x('2026-10-22') - g.x('2026-10-01')
    expect(a).toBeCloseTo(b, 5)    // both are 21 days
  })

  it('draws heavier weights lower on the page', () => {
    const g = buildGoalChart(base)
    expect(g.y(88)).toBeLessThan(g.y(80))
  })

  it('has one dot per weigh-in, a trend line, a goal line at the target and a dashed projection', () => {
    const g = buildGoalChart(base)
    expect(g.dots).toHaveLength(3)
    expect(g.trend.startsWith('M')).toBe(true)
    expect(g.goalLine).toContain(`${g.y(78).toFixed(1)}`.replace(/\.0$/, ''))
    expect(g.projection.startsWith('M')).toBe(true)
    expect(g.current?.kg).toBeCloseTo(84.1)
  })

  it('has no projection when the pace goes the wrong way', () => {
    expect(buildGoalChart({ ...base, projection: null }).projection).toBe('')
  })

  it('works with no weigh-ins at all', () => {
    const g = buildGoalChart({ ...base, series: [], projection: null })
    expect(g.dots).toEqual([])
    expect(g.current).toBeNull()
  })

  it('marks the first of each month', () => {
    const g = buildGoalChart(base)
    expect(g.xTicks.map((t) => t.date)).toContain('2026-11-01')
    expect(g.xTicks.map((t) => t.date)).toContain('2027-02-01')
  })

  it('keeps every ymin/ymax tick round', () => {
    expect(niceTicks(78, 88.6)).toEqual([78, 80, 82, 84, 86, 88, 90])
  })
})
