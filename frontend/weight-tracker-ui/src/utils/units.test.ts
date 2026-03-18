import { describe, it, expect } from 'vitest'
import { kgToLbs, lbsToKg, formatWeight, displayWeight, toKg, bmiLabel } from './units'

describe('kgToLbs', () => {
  it('converts 1 kg to 2.2 lbs', () => {
    expect(kgToLbs(1)).toBe(2.2)
  })

  it('converts 70 kg to 154.3 lbs', () => {
    expect(kgToLbs(70)).toBe(154.3)
  })

  it('converts 0 kg to 0 lbs', () => {
    expect(kgToLbs(0)).toBe(0)
  })
})

describe('lbsToKg', () => {
  it('converts 2.2 lbs to approximately 1 kg', () => {
    expect(lbsToKg(2.2)).toBe(1)
  })

  it('converts 154.3 lbs to approximately 70 kg', () => {
    expect(lbsToKg(154.3)).toBe(70)
  })

  it('converts 0 lbs to 0 kg', () => {
    expect(lbsToKg(0)).toBe(0)
  })
})

describe('formatWeight', () => {
  it('formats kg with kg suffix', () => {
    expect(formatWeight(70, 'kg')).toBe('70 kg')
  })

  it('formats lbs with converted value and lbs suffix', () => {
    expect(formatWeight(1, 'lbs')).toBe('2.2 lbs')
  })
})

describe('displayWeight', () => {
  it('returns kg value unchanged for kg unit', () => {
    expect(displayWeight(75, 'kg')).toBe(75)
  })

  it('returns converted lbs value for lbs unit', () => {
    expect(displayWeight(1, 'lbs')).toBe(2.2)
  })
})

describe('toKg', () => {
  it('returns value unchanged for kg unit', () => {
    expect(toKg(75, 'kg')).toBe(75)
  })

  it('converts lbs input to kg for lbs unit', () => {
    expect(toKg(2.2, 'lbs')).toBe(1)
  })
})

describe('bmiLabel', () => {
  it('returns Underweight for BMI below 18.5', () => {
    const result = bmiLabel(17)
    expect(result.text).toBe('Underweight')
    expect(result.color).toBe('text-blue-500')
  })

  it('returns Normal for BMI between 18.5 and 24.9', () => {
    const result = bmiLabel(22)
    expect(result.text).toBe('Normal')
    expect(result.color).toBe('text-green-600')
  })

  it('returns Overweight for BMI between 25 and 29.9', () => {
    const result = bmiLabel(27)
    expect(result.text).toBe('Overweight')
    expect(result.color).toBe('text-yellow-500')
  })

  it('returns Obese for BMI 30 and above', () => {
    const result = bmiLabel(35)
    expect(result.text).toBe('Obese')
    expect(result.color).toBe('text-red-500')
  })

  it('uses boundary values correctly (18.5 is Normal)', () => {
    expect(bmiLabel(18.5).text).toBe('Normal')
  })

  it('uses boundary values correctly (25 is Overweight)', () => {
    expect(bmiLabel(25).text).toBe('Overweight')
  })

  it('uses boundary values correctly (30 is Obese)', () => {
    expect(bmiLabel(30).text).toBe('Obese')
  })
})
