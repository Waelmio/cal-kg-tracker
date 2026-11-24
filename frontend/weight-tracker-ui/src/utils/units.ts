import type { WeightUnit } from '../types'

const LBS_PER_KG = 2.20462

/** In the user's unit, not rounded: for changes, rates and charts. */
export const toUnit = (kg: number, unit: WeightUnit): number => (unit === 'lbs' ? kg * LBS_PER_KG : kg)

export const kgToLbs = (kg: number): number => Math.round(toUnit(kg, 'lbs') * 10) / 10

export const lbsToKg = (lbs: number): number => Math.round((lbs / LBS_PER_KG) * 100) / 100

export const displayWeight = (kg: number, unit: WeightUnit): number =>
  unit === 'lbs' ? kgToLbs(kg) : kg

export const toKg = (value: number, unit: WeightUnit): number =>
  unit === 'lbs' ? lbsToKg(value) : value
