import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import LogSheet from './LogSheet.vue'
import { useAppStore } from '../../stores/app'
import { useLogSheetStore } from '../../stores/logSheet'
import { useLogWeeksStore } from '../../stores/logWeeks'
import * as logApi from '../../api/dailyLog'
import * as statsApi from '../../api/stats'
import { addDays, mondayOf } from '../../utils/dates'
import type { LogDay, LogWeek } from '../../types'

vi.mock('../../api/dailyLog', () => ({ saveDay: vi.fn(), deleteDay: vi.fn() }))
vi.mock('../../api/stats', () => ({ getLogWeek: vi.fn() }))

const TODAY = '2026-11-24'   // a Tuesday: five other days are left in the week

const blank = (date: string): LogDay => ({
  date, calories: null, weightKg: null, notes: null, isCheatDay: false, status: null,
  planned: date >= TODAY && date <= '2026-11-29' ? 1881 : null, calorieTarget: 1850, tdee: 2350, lastWeightKg: 83.8,
})

/** The server's answer for a week: `days` overrides single days. A pool of 6 x 1881 for the current week. */
function week(date: string, days: Record<string, Partial<LogDay>> = {}): LogWeek {
  const monday = mondayOf(date)
  return {
    monday,
    days: Array.from({ length: 7 }, (_, i) => ({ ...blank(addDays(monday, i)), ...days[addDays(monday, i)] })),
    thisWeek: monday === '2026-11-23' ? { todayBudget: 1881, pool: 11286, otherDays: 5 } : null,
  }
}

async function open(today: Partial<LogDay> = {}, others: Record<string, Partial<LogDay>> = {}, date = TODAY) {
  vi.mocked(statsApi.getLogWeek).mockImplementation(async (d) => week(d, { [TODAY]: today, ...others }))
  vi.mocked(logApi.saveDay).mockResolvedValue(null)
  const app = useAppStore()
  app.today = TODAY
  useLogSheetStore().open(date)
  const w = mount(LogSheet, { attachTo: document.body })
  await flushPromises()
  return w
}
const type = async (sel: string, value: string) => { q(sel).value = value; q(sel).dispatchEvent(new Event('input')); await flushPromises() }
const hint = () => document.body.querySelector('[data-test=kcal-hint]')!.textContent
const q = (sel: string) => document.body.querySelector(sel) as HTMLInputElement
const click = async (label: string) => { (document.body.querySelector(`[aria-label="${label}"]`) as HTMLElement).click(); await flushPromises() }
const saveButton = () => Array.from(document.body.querySelectorAll('button')).find((b) => /Save day|Delete entry/.test(b.textContent ?? '')) as HTMLButtonElement

beforeEach(() => { setActivePinia(createPinia()); vi.clearAllMocks() })
afterEach(() => { document.body.innerHTML = '' })

describe('LogSheet', () => {
  it('never prefills a value that could be saved by accident: the plan and the last weight are placeholders', async () => {
    await open()
    expect(q('#kcal').value).toBe('')
    expect(q('#kcal').placeholder).toBe('1881')
    expect(q('#kg').value).toBe('')
    expect(q('#kg').placeholder).toBe('83.8')
    expect(saveButton().textContent).toContain('Save day')     // nothing is stored, so there is nothing to delete
    expect(saveButton().disabled).toBe(true)
  })

  it('starts calories at the target when the day has no planned value', async () => {
    await open({ planned: null })
    expect(q('#kcal').value).toBe('1850')
    expect(saveButton().disabled).toBe(false)
  })

  it('starts a cheat day without a plan at its own target', async () => {
    await open({ planned: null, isCheatDay: true })
    expect(q('#kcal').value).toBe('2350')
  })

  it('stays empty without a target', async () => {
    await open({ planned: null, calorieTarget: null })
    expect(q('#kcal').value).toBe('')
    expect(q('#kcal').placeholder).toBe('Not logged')
  })

  it('fills the plan and the last weight only when their box is clicked', async () => {
    await open()
    q('#kg').click(); await flushPromises()
    expect(q('#kg').value).toBe('83.8')
    expect(q('#kcal').value).toBe('')
    q('#kcal').click(); await flushPromises()
    expect(q('#kcal').value).toBe('1881')
  })

  it('saves a weight alone, without calories, and calories without a weight', async () => {
    const w = await open()
    await type('#kg', '84.2')
    saveButton().click(); await flushPromises()
    expect(logApi.saveDay).toHaveBeenLastCalledWith(TODAY, { weightKg: 84.2, caloriesKcal: null, notes: null, isCheatDay: false })
    w.unmount()
    await open()
    await type('#kcal', '1500')
    saveButton().click(); await flushPromises()
    expect(logApi.saveDay).toHaveBeenLastCalledWith(TODAY, { weightKg: null, caloriesKcal: 1500, notes: null, isCheatDay: false })
  })

  it('does not refill a weight after Remove, and does not overwrite one that is there', async () => {
    await open({ weightKg: 83.9 })
    q('#kg').click(); await flushPromises()
    expect(q('#kg').value).toBe('83.9')
    await click('Remove weight')
    q('#kg').click(); await flushPromises()
    expect(q('#kg').value).toBe('')
  })

  it('saves only a note or a cheat mark, and never deletes the day for it', async () => {
    const w = await open()
    await type('#note', 'dinner out')
    expect(saveButton().textContent).toContain('Save day')
    saveButton().click(); await flushPromises()
    expect(logApi.saveDay).toHaveBeenLastCalledWith(TODAY, { weightKg: null, caloriesKcal: null, notes: 'dinner out', isCheatDay: false })
    w.unmount()

    await open({ notes: 'dinner out', isCheatDay: true })      // a day with nothing but a note and a cheat mark
    await type('#note', 'dinner out, travel')
    saveButton().click(); await flushPromises()
    expect(logApi.deleteDay).not.toHaveBeenCalled()
    expect(logApi.saveDay).toHaveBeenLastCalledWith(TODAY, { weightKg: null, caloriesKcal: null, notes: 'dinner out, travel', isCheatDay: true })
  })

  it('refuses what is not a number instead of saving it as nothing', async () => {
    await open({ calories: 1240, weightKg: 83.9 })
    await type('#kcal', '12a0')
    expect(saveButton().disabled).toBe(true)
    expect(document.body.textContent).toContain('Enter a number from 0 to 30000.')
    await type('#kcal', '1240'); await type('#kg', '83..9')
    expect(saveButton().disabled).toBe(true)
    expect(document.body.textContent).toContain('Enter a weight')
    q('#kcal').dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true })); await flushPromises()
    expect(logApi.saveDay).not.toHaveBeenCalled()
  })

  it('shows what is stored for the day, and keeps the stored weight to the gram when it is not edited', async () => {
    await open({ calories: 1240, weightKg: 83.94, notes: 'dinner out', isCheatDay: true })
    expect(q('#kcal').value).toBe('1240')
    expect(q('#kg').value).toBe('83.9')
    expect(q('#note').value).toBe('dinner out')
    expect(q('#cheat').checked).toBe(true)
    await type('#kcal', '1300')
    saveButton().click(); await flushPromises()
    expect(logApi.saveDay).toHaveBeenLastCalledWith(TODAY, { weightKg: 83.94, caloriesKcal: 1300, notes: 'dinner out', isCheatDay: true })
  })

  it('saves 0 kcal as a fasting day, not as empty', async () => {
    await open()
    await type('#kcal', '0')
    expect(hint()).toBe('0 kcal, saved as a fasting day')
    saveButton().click(); await flushPromises()
    expect(logApi.saveDay).toHaveBeenCalledWith(TODAY, { weightKg: null, caloriesKcal: 0, notes: null, isCheatDay: false })
  })

  it('quick adds and the stepper change calories by the right amounts', async () => {
    await open({ calories: 1000 })
    await click('50 kcal more'); expect(q('#kcal').value).toBe('1050')
    await click('50 kcal less'); await click('50 kcal less'); expect(q('#kcal').value).toBe('950')
    ;(Array.from(document.body.querySelectorAll('.quick button')).find((b) => b.textContent === '+250') as HTMLElement).click()
    await flushPromises()
    expect(q('#kcal').value).toBe('1200')
  })

  it('the weight stepper starts from the last weight, then moves by 0.1', async () => {
    await open()
    await click('0.1 kg more'); expect(q('#kg').value).toBe('83.8')
    await click('0.1 kg more'); expect(q('#kg').value).toBe('83.9')
  })

  it('shows what is left of the budget, and what is over it', async () => {
    await open({ calories: 1240 })
    expect(hint()).toBe('641 left of 1881')
    await type('#kcal', '2000')
    expect(hint()).toBe('119 over 1881')
  })

  it('shows what the rest of the week gets, as the server shares it', async () => {
    await open({ calories: 1240 })
    expect(document.body.textContent).toContain('Rest of the week: 1881 a day')
    await type('#kcal', '2400')
    expect(document.body.textContent).toContain('Rest of the week: 1777 a day')   // (11286 - 2400) / 5
  })

  it('gives the same rest of the week when an over-budget day is opened again', async () => {
    // The server already planned the other days lower; the pool does not change.
    const others = Object.fromEntries(['25', '26', '27', '28', '29'].map((d) => [`2026-11-${d}`, { planned: 1777 }]))
    await open({ calories: 2400 }, others)
    expect(document.body.textContent).toContain('Rest of the week: 1777 a day')
    await type('#kcal', '1500')
    expect(document.body.textContent).toContain('Rest of the week: 1881 a day')
  })

  it('the cheat day switch moves the budget, not the other days', async () => {
    await open({ calories: 1240 })
    q('#cheat').click(); await flushPromises()
    expect(hint()).toBe('1141 left of 2381')
    expect(document.body.textContent).toContain('Rest of the week: 1881 a day')
    await type('#kcal', '2881')
    expect(document.body.textContent).toContain('Rest of the week: 1781 a day')   // (11286 + 500 - 2881) / 5
  })

  it('switching a saved cheat day off gives its extra back', async () => {
    await open({ calories: 2300, isCheatDay: true, planned: 2381 })
    expect(hint()).toBe('81 left of 2381')
    q('#cheat').click(); await flushPromises()
    expect(hint()).toBe('419 over 1881')
  })

  it('a future day that is logged is compared with its target, not with itself', async () => {
    await open({}, { '2026-11-28': { calories: 2500, planned: 2500 } }, '2026-11-28')
    expect(hint()).toBe('650 over 1850')
  })

  it('shows the week with its dates, what was eaten and what is planned', async () => {
    await open({}, { '2026-11-23': { calories: 1689, status: 'under' } })
    const days = Array.from(document.body.querySelectorAll<HTMLElement>('.week .day'))
    expect(days.map((d) => d.textContent!.trim())).toEqual(['Mon 231689', 'Tue 241881', 'Wed 251881', 'Thu 261881', 'Fri 271881', 'Sat 281881', 'Sun 291881'])
    expect(days[0].classList.contains('plan')).toBe(false)
    expect(days[2].classList.contains('plan')).toBe(true)
    await type('#kcal', '2400')
    expect(days[2].textContent).toContain('1777')
  })

  it('Remove turns into Undo and nothing is deleted before Save', async () => {
    await open({ calories: 1240, weightKg: 83.9 })
    await click('Remove calories')
    expect(q('#kcal').value).toBe('')
    expect(hint()).toBe('Calories are removed when you save')
    expect(logApi.deleteDay).not.toHaveBeenCalled()
    expect(logApi.saveDay).not.toHaveBeenCalled()

    await click('Undo removing calories')
    expect(q('#kcal').value).toBe('1240')
  })

  it('removing both fields offers "Delete entry" and deletes the whole day', async () => {
    await open({ calories: 1240, weightKg: 83.9, notes: 'x', isCheatDay: true })
    await click('Remove calories'); await click('Remove weight')
    expect(saveButton().textContent).toContain('Delete entry')
    expect(document.body.querySelector('[data-test=delete-hint]')!.textContent).toContain('note and cheat day')
    saveButton().click(); await flushPromises()
    expect(logApi.deleteDay).toHaveBeenCalledWith(TODAY)
    expect(logApi.saveDay).not.toHaveBeenCalled()
  })

  it('converts a weight typed in lbs to kg with 2 decimals', async () => {
    const app = useAppStore(); app.settings.preferredUnit = 'lbs'
    await open()
    await type('#kg', '185')
    saveButton().click(); await flushPromises()
    expect(logApi.saveDay).toHaveBeenCalledWith(TODAY, expect.objectContaining({ weightKg: 83.91 }))
  })

  it('Enter saves, and a failed save shows the message and keeps the sheet open', async () => {
    await open({ calories: 1200 })
    vi.mocked(logApi.saveDay).mockRejectedValueOnce(new Error('Weight must be between 1 and 700'))
    q('#kcal').dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true })); await flushPromises()
    expect(document.body.textContent).toContain('Weight must be between 1 and 700')
    expect(useLogSheetStore().isOpen).toBe(true)

    q('#kcal').dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true })); await flushPromises()
    expect(useLogSheetStore().isOpen).toBe(false)
  })

  it('refreshes the app after saving so statistics are refetched', async () => {
    await open({ calories: 1200 })
    const before = useAppStore().version
    saveButton().click(); await flushPromises()
    expect(useAppStore().version).toBe(before + 1)
  })
})

describe('loading', () => {
  it('one request per week; neighbouring weeks are ready before they are opened', async () => {
    await open({}, { '2026-11-23': { calories: 1689 } })
    expect(vi.mocked(statsApi.getLogWeek).mock.calls.map((c) => c[0])).toEqual(['2026-11-23', '2026-11-16', '2026-11-30'])
    await click('Previous day')
    expect(q('#kcal').value).toBe('1689')
    expect(statsApi.getLogWeek).toHaveBeenCalledTimes(3)
  })

  it('opens instantly when the week is there: the form is in the very first render', async () => {
    const w = await open({ calories: 1240 })
    w.unmount()
    mount(LogSheet, { attachTo: document.body })
    expect(document.body.querySelector('[data-test=skeleton]')).toBeNull()
    expect(q('#kcal').value).toBe('1240')
  })

  it('shows a skeleton until the day is there, and saves nothing meanwhile', async () => {
    let release!: (w: LogWeek) => void
    vi.mocked(statsApi.getLogWeek).mockImplementation(() => new Promise((r) => { release = r }))
    useAppStore().today = TODAY
    useLogSheetStore().open(TODAY)
    mount(LogSheet, { attachTo: document.body })
    await flushPromises()
    expect(document.body.querySelector('[data-test=skeleton]')).not.toBeNull()
    expect(document.body.querySelector('#kcal')).toBeNull()
    expect(saveButton().disabled).toBe(true)

    release(week(TODAY, { [TODAY]: { calories: 1240 } }))
    await flushPromises()
    expect(document.body.querySelector('[data-test=skeleton]')).toBeNull()
    expect(q('#kcal').value).toBe('1240')
  })

  it('never hides or overwrites what is being typed when the app refreshes', async () => {
    await open({ calories: 1240 })
    await type('#kcal', '1999')
    useAppStore().refresh(); useAppStore().today = TODAY
    await flushPromises()
    expect(q('#kcal').value).toBe('1999')
  })

  it('never opens on data from before the last change', async () => {
    const w = await open({ calories: 1240 })
    w.unmount()
    useAppStore().refresh(); useAppStore().today = TODAY
    vi.mocked(statsApi.getLogWeek).mockImplementation(async (d) => week(d, { [TODAY]: { calories: 1500 } }))
    mount(LogSheet, { attachTo: document.body })
    expect(document.body.querySelector('[data-test=skeleton]')).not.toBeNull()
    await flushPromises()
    expect(q('#kcal').value).toBe('1500')
  })

  it('drops an answer that was asked for before a change', async () => {
    const store = useLogWeeksStore()
    let release!: (w: LogWeek) => void
    vi.mocked(statsApi.getLogWeek).mockImplementationOnce(() => new Promise((r) => { release = r }))
    const first = store.load(TODAY)
    useAppStore().refresh()
    vi.mocked(statsApi.getLogWeek).mockResolvedValue(week(TODAY, { [TODAY]: { calories: 2 } }))
    await store.load(TODAY)
    release(week(TODAY, { [TODAY]: { calories: 1 } }))
    await first
    expect(store.day(TODAY)?.calories).toBe(2)
  })

  it('shares one request between callers asking at the same time', async () => {
    const store = useLogWeeksStore()
    vi.mocked(statsApi.getLogWeek).mockResolvedValue(week(TODAY))
    await Promise.all([store.load(TODAY), store.load('2026-11-25'), store.load(TODAY)])
    expect(statsApi.getLogWeek).toHaveBeenCalledTimes(1)
  })

  it('shows a failed load with a way to try again', async () => {
    vi.mocked(statsApi.getLogWeek).mockRejectedValue(new Error('Network Error'))
    useAppStore().today = TODAY
    useLogSheetStore().open(TODAY)
    mount(LogSheet, { attachTo: document.body })
    await flushPromises()
    expect(document.body.textContent).toContain('Network Error')
    vi.mocked(statsApi.getLogWeek).mockResolvedValue(week(TODAY, { [TODAY]: { calories: 1240 } }))
    ;(Array.from(document.body.querySelectorAll('button')).find((b) => b.textContent === 'Try again') as HTMLElement).click()
    await flushPromises()
    expect(q('#kcal').value).toBe('1240')
  })
})
