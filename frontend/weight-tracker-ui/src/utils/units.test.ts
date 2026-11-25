import { describe, it, expect } from 'vitest'
import { kgToLbs, lbsToKg, displayWeight, toKg } from './units'

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

  it('converts 154.3 lbs to 69.99 kg (2 decimals)', () => {
    expect(lbsToKg(154.3)).toBe(69.99)
  })

  it('converts 0 lbs to 0 kg', () => {
    expect(lbsToKg(0)).toBe(0)
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


describe('lbs round trip', () => {
  it('keeps 2 decimals instead of rounding to 0.1 kg', () => {
    expect(lbsToKg(185)).toBe(83.91)
    expect(toKg(185, 'lbs')).toBe(83.91)
  })
})
