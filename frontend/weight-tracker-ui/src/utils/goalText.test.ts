import { describe, it, expect } from 'vitest'
import { goalChip, goalSentence, goalTitle } from './goalText'
import type { GoalStatus } from '../types'

const goal = (over: Partial<GoalStatus> = {}): GoalStatus => ({
  id: 1, kind: 'Reach', state: 'active', pace: 'behind', targetWeightKg: 78, startingWeightKg: 88.6, startDate: '2026-09-10',
  targetDate: '2027-02-12', endedOn: null, endReason: null, currentKg: 84.07, progressPercent: 43, remainingKg: 6.07,
  neededKgPerWeek: 0.53, paceKgPerWeek: -0.41, projectedDate: '2027-03-07', lateWeeks: 3, ...over,
})

describe('goal wording', () => {
  it('titles a goal with its date', () => expect(goalTitle(goal(), 'kg')).toBe('78 kg by 12 Feb 2027'))
  it('titles a maintenance goal', () => expect(goalTitle(goal({ kind: 'Maintain', targetDate: null }), 'kg')).toBe('Keep 78.0 kg'))

  it('says how far behind', () => expect(goalChip(goal())).toEqual({ kind: 'warn', text: '3 weeks behind' }))
  it('says ahead and on track', () => {
    expect(goalChip(goal({ pace: 'ahead', lateWeeks: -2 })).text).toBe('2 weeks ahead')
    expect(goalChip(goal({ pace: 'onTrack', lateWeeks: 0 })).text).toBe('On track')
  })
  it('marks reached and date passed', () => {
    expect(goalChip(goal({ state: 'reached' })).text).toBe('✓ Reached')
    expect(goalChip(goal({ state: 'datePassed' })).text).toBe('Date passed')
  })
  it('does not guess a pace without enough weigh-ins', () => {
    expect(goalChip(goal({ pace: null, paceKgPerWeek: null, projectedDate: null, lateWeeks: null })).text).toBe('Not enough weigh-ins')
  })

  it('describes the pace, the projection and what is needed', () => {
    expect(goalSentence(goal(), 'kg')).toBe('At your current pace you reach 78 kg around 7 Mar. You lose 0.41 kg a week and need 0.53.')
  })
  it('describes a passed date', () => {
    const s = goalSentence(goal({ state: 'datePassed', remainingKg: 1.4, targetDate: '2026-11-12' }), 'kg')
    expect(s).toContain('1.4 kg remained on 12 November')
    expect(s).toContain('around 7 Mar')
  })
})
