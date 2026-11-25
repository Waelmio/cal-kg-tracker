import { describe, it, expect } from 'vitest'
import { buildGoalChart, niceTicks } from './chart'

const base = {
  today: '2026-11-24', start: '2026-09-10', end: '2027-02-12', targetKg: 78, startKg: 88.6,
  series: [
    { date: '2026-09-10', kg: 88.6, avg7: 88.6 },
    { date: '2026-10-10', kg: 87, avg7: 87.2 },
    { date: '2026-11-24', kg: 84, avg7: 84.1 },
  ],
  currentKg: 84.1, pacePerDay: -0.41 / 7,
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

  it('has one dot per weigh-in, a trend line and a goal line from the starting weight to the target', () => {
    const g = buildGoalChart(base)
    const f = (n: number) => String(Math.round(n * 10) / 10)
    expect(g.dots).toHaveLength(3)
    expect(g.trend.startsWith('M')).toBe(true)
    expect(g.goalLine).toBe(`M${f(g.x('2026-09-10'))} ${f(g.y(88.6))}L${f(g.x('2027-02-12'))} ${f(g.y(78))}`)
    expect(g.current?.kg).toBeCloseTo(84.1)
  })

  it('projects to the end of the axis, and says what weight that is', () => {
    const g = buildGoalChart(base)
    expect(g.projection.startsWith('M')).toBe(true)
    expect(g.projectionEnd).toMatchObject({ reached: false, x: Math.round(g.x('2027-02-12') * 10) / 10 })
    expect(g.projectionEnd!.kg).toBeCloseTo(84.1 - (0.41 / 7) * 80, 5)
  })

  it('stops the projection where it meets the target', () => {
    const g = buildGoalChart({ ...base, pacePerDay: -0.2 })
    expect(g.projectionEnd).toMatchObject({ reached: true, kg: 78 })
    expect(g.projectionEnd!.x).toBeLessThan(g.x('2027-02-12'))
  })

  it('ends the average line at the weight shown on the tile, even without a weigh-in today', () => {
    const g = buildGoalChart({ ...base, series: base.series.slice(0, 2), currentKg: 85 })
    expect(g.current).toEqual({ x: Math.round(g.x('2026-11-24') * 10) / 10, y: Math.round(g.y(85) * 10) / 10, kg: 85 })
    expect(g.trend.endsWith(`L${g.current!.x} ${g.current!.y}`)).toBe(true)
  })

  it('has no projection without a pace, and shows a pace going the wrong way as it is', () => {
    expect(buildGoalChart({ ...base, pacePerDay: null }).projection).toBe('')
    expect(buildGoalChart({ ...base, pacePerDay: 0.02 }).projectionEnd!.kg).toBeGreaterThan(84.1)
  })

  it('works with no weigh-ins at all', () => {
    const g = buildGoalChart({ ...base, series: [], currentKg: null, pacePerDay: null })
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
