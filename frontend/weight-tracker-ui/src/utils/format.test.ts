import { describe, it, expect } from 'vitest'
import { kcal, longDate, relativeDay, shortDate, signed, weight, weightChange } from './format'

describe('format', () => {
  it('writes dates the way the mockups do', () => {
    expect(longDate('2026-11-24')).toBe('Tuesday 24 November')
    expect(shortDate('2026-11-24')).toBe('24 Nov')
    expect(shortDate('2026-09-10', true)).toBe('10 Sep 2026')
  })

  it('names relative days', () => {
    expect(relativeDay('2026-11-24', '2026-11-24')).toBe('Today')
    expect(relativeDay('2026-11-23', '2026-11-24')).toBe('Yesterday')
    expect(relativeDay('2026-11-25', '2026-11-24')).toBe('Tomorrow')
    expect(relativeDay('2026-11-20', '2026-11-24')).toBe('Fri 20 Nov')
  })

  it('never uses thousand separators', () => {
    expect(kcal(26839)).toBe('26839')
    expect(kcal(1881.4)).toBe('1881')
    expect(kcal(null)).toBe('–')
  })

  it('shows weight in the chosen unit with one decimal', () => {
    expect(weight(84.07, 'kg')).toBe('84.1')
    expect(weight(84.07, 'lbs', true)).toBe('185.3 lbs')
    expect(weight(null, 'kg')).toBe('–')
  })

  it('puts the triangle on weight changes only', () => {
    expect(weightChange(-0.41, 'kg', 2)).toBe('▼ 0.41')
    expect(weightChange(0.25, 'kg', 2)).toBe('▲ 0.25')
    expect(weightChange(0.001, 'kg', 1)).toBe('0.0')
    expect(weightChange(null, 'kg')).toBe('')
  })

  it('uses a real minus sign', () => {
    expect(signed(-26)).toBe('−26')
    expect(signed(39)).toBe('+39')
    expect(signed(0)).toBe('0')
  })
})
