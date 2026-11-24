import type { GoalStatus, WeightUnit } from '../types'
import { amount, shortDate, weight } from './format'
import { daysBetween } from './dates'

export type ChipKind = 'under' | 'balanced' | 'over' | 'neutral' | 'warn' | 'info'

const count = (n: number, what: string) => `${Math.abs(n)} ${what}${Math.abs(n) === 1 ? '' : 's'}`

/** The target weight, without a needless ".0": "78 kg". */
export const goalWeight = (g: GoalStatus, unit: WeightUnit): string => `${weight(g.targetWeightKg, unit).replace(/\.0$/, '')} ${unit}`

/** Title: "78 kg by 12 Feb 2027" or "Keep 78 kg". */
export function goalTitle(g: GoalStatus, unit: WeightUnit): string {
  if (g.kind === 'Maintain') return `Keep ${goalWeight(g, unit)}`
  return g.targetDate ? `${goalWeight(g, unit)} by ${shortDate(g.targetDate, true)}` : goalWeight(g, unit)
}

/** The chip next to the goal: state and pace in a few words. */
export function goalChip(g: GoalStatus, unit: WeightUnit = 'kg'): { kind: ChipKind; text: string } {
  if (g.kind === 'Maintain') {
    if (g.withinBand === false && g.currentKg != null) {
      const off = g.currentKg - g.targetWeightKg
      return { kind: 'warn', text: `${amount(Math.round(Math.abs(off) * 10) / 10, unit)} ${unit} ${off > 0 ? 'above' : 'below'}` }
    }
    return { kind: 'info', text: 'Maintaining' }
  }
  if (g.state === 'reached') return { kind: 'under', text: '✓ Reached' }
  if (g.state === 'datePassed') return { kind: 'warn', text: 'Date passed' }
  if (g.pace === 'ahead') return { kind: 'under', text: `${count(g.lateWeeks ?? 1, 'week')} ahead` }
  if (g.pace === 'onTrack') return { kind: 'under', text: 'On track' }
  if (g.pace === 'behind') return { kind: 'warn', text: g.lateWeeks != null ? `${count(g.lateWeeks, 'week')} behind` : 'Behind' }
  return { kind: 'neutral', text: 'Not enough weigh-ins' }
}

/** The headline of a reached goal: "10.6 kg lost in 146 days". */
export function reachedText(g: GoalStatus, unit: WeightUnit): { change: string; time: string } | null {
  if (g.state !== 'reached' || g.startingWeightKg == null || g.currentKg == null) return null
  const change = g.currentKg - g.startingWeightKg
  return {
    change: `${weight(Math.abs(change), unit)} ${unit} ${change <= 0 ? 'lost' : 'gained'}`,
    time: g.reachedOn ? `in ${count(daysBetween(g.startDate, g.reachedOn), 'day')}` : '',
  }
}

/** One or two sentences about where the goal stands. */
export function goalSentence(g: GoalStatus, unit: WeightUnit): string {
  const target = goalWeight(g, unit)

  if (g.kind === 'Maintain') {
    const band = `${amount(g.maintainBandKg, unit)} ${unit}`
    return g.currentKg == null ? 'Log your weight to see how you are doing.' : `You are at ${weight(g.currentKg, unit)} ${unit}. The aim is to stay within ${band} of ${target}.`
  }
  if (g.state === 'reached') {
    if (!g.reachedOn) return `Your 7-day average is at or past your ${target} goal.`
    const early = g.targetDate ? daysBetween(g.reachedOn, g.targetDate) : 0
    const when = early > 0 ? `, ${count(early, 'day')} early` : early < 0 ? `, ${count(early, 'day')} late` : ''
    return `Your 7-day average reached ${target} on ${shortDate(g.reachedOn)}${when}.`
  }
  if (g.state === 'datePassed') {
    const left = g.remainingAtTargetDateKg == null || !g.targetDate ? '' : `${weight(g.remainingAtTargetDateKg, unit)} ${unit} remained on ${shortDate(g.targetDate)}. `
    return left + (g.projectedDate ? `At the same pace, ${target} is around ${shortDate(g.projectedDate)}.` : '')
  }
  if (g.paceKgPerWeek == null) return 'Not enough weigh-ins to show a pace yet.'

  const pace = `${amount(Math.abs(g.paceKgPerWeek), unit)} ${unit}`
  const needed = g.neededKgPerWeek == null ? null : amount(g.neededKgPerWeek, unit)
  if (g.projectedDate) {
    return `At your current pace you reach ${target} around ${shortDate(g.projectedDate)}. You ${g.paceKgPerWeek < 0 ? 'lose' : 'gain'} ${pace} a week${needed ? ` and need ${needed}` : ''}.`
  }
  return 'At your current pace you are not moving towards the goal.'
}
