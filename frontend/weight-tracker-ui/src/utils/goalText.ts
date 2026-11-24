import type { GoalStatus, WeightUnit } from '../types'
import { fullDate, shortDate, weight } from './format'
import { displayWeight } from './units'

export type ChipKind = 'under' | 'balanced' | 'over' | 'neutral' | 'warn' | 'info'

const wk = (n: number) => `${Math.abs(n)} ${Math.abs(n) === 1 ? 'week' : 'weeks'}`
const kgText = (kg: number, unit: WeightUnit) => `${Math.round(displayWeight(kg, unit) * 100) / 100}`

/** Title: "78 kg by 12 Feb 2027" or "Keep 78 kg". */
export function goalTitle(g: GoalStatus, unit: WeightUnit): string {
  const target = `${weight(g.targetWeightKg, unit)} ${unit}`
  if (g.kind === 'Maintain') return `Keep ${target}`
  return g.targetDate ? `${target.replace(/\.0 /, ' ')} by ${shortDate(g.targetDate, true)}` : target
}

/** The chip next to the goal: state and pace in a few words. */
export function goalChip(g: GoalStatus): { kind: ChipKind; text: string } {
  if (g.kind === 'Maintain') return { kind: 'info', text: 'Maintaining' }
  if (g.state === 'reached') return { kind: 'under', text: '✓ Reached' }
  if (g.state === 'datePassed') return { kind: 'warn', text: 'Date passed' }
  if (g.pace === 'ahead') return { kind: 'under', text: `${wk(g.lateWeeks ?? 1)} ahead` }
  if (g.pace === 'onTrack') return { kind: 'under', text: 'On track' }
  if (g.pace === 'behind') return g.lateWeeks != null ? { kind: 'warn', text: `${wk(g.lateWeeks)} behind` } : { kind: 'warn', text: 'Behind' }
  return { kind: 'neutral', text: 'Not enough weigh-ins' }
}

/** One or two sentences about where the goal stands. */
export function goalSentence(g: GoalStatus, unit: WeightUnit): string {
  const target = `${kgText(g.targetWeightKg, unit)} ${unit}`
  const pace = g.paceKgPerWeek == null ? null : `${kgText(Math.abs(g.paceKgPerWeek), unit)} ${unit}`
  const needed = g.neededKgPerWeek == null ? null : `${kgText(g.neededKgPerWeek, unit)}`

  if (g.kind === 'Maintain') {
    return g.currentKg == null ? 'Log your weight to see how you are doing.' : `You are at ${weight(g.currentKg, unit)} ${unit}. The aim is to stay within 1 ${unit} of ${target}.`
  }
  if (g.state === 'reached') {
    return g.currentKg == null ? '' : `Your 7-day average is ${weight(g.currentKg, unit)} ${unit}, at or past your ${target} goal.`
  }
  if (g.state === 'datePassed') {
    const left = g.remainingKg == null ? '' : `${kgText(g.remainingKg, unit)} ${unit} remained on ${g.targetDate ? fullDate(g.targetDate).replace(/ \d{4}$/, '') : 'the target date'}. `
    return left + (g.projectedDate ? `At the same pace, ${target} is around ${shortDate(g.projectedDate)}.` : '')
  }
  if (g.projectedDate && pace) {
    return `At your current pace you reach ${target} around ${shortDate(g.projectedDate)}. You ${g.paceKgPerWeek! < 0 ? 'lose' : 'gain'} ${pace} a week${needed ? ` and need ${needed}` : ''}.`
  }
  return g.paceKgPerWeek == null ? 'Not enough weigh-ins to show a pace yet.' : 'At your current pace you are not moving towards the goal.'
}
