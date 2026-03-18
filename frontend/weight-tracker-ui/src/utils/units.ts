import type { WeightUnit } from '../types'


export const kgToLbs = (kg: number): number => Math.round(kg * 2.20462 * 10) / 10

export const lbsToKg = (lbs: number): number => Math.round((lbs / 2.20462) * 10) / 10

export const formatWeight = (kg: number, unit: WeightUnit): string => {
  if (unit === 'lbs') return `${kgToLbs(kg)} lbs`
  return `${kg} kg`
}

export const displayWeight = (kg: number, unit: WeightUnit): number =>
  unit === 'lbs' ? kgToLbs(kg) : kg

export const toKg = (value: number, unit: WeightUnit): number =>
  unit === 'lbs' ? lbsToKg(value) : value

export const bmiLabel = (bmi: number): { text: string; color: string } => {
  if (bmi < 18.5) return { text: 'Underweight', color: 'text-blue-500' }
  if (bmi < 25) return { text: 'Normal', color: 'text-green-600' }
  if (bmi < 30) return { text: 'Overweight', color: 'text-yellow-500' }
  return { text: 'Obese', color: 'text-red-500' }
}
