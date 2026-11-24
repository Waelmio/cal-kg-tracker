import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import LogSheet from './LogSheet.vue'
import { useAppStore } from '../../stores/app'
import { useLogSheetStore } from '../../stores/logSheet'
import * as logApi from '../../api/dailyLog'
import * as statsApi from '../../api/stats'
import type { CalendarDay, DailyLog } from '../../types'

vi.mock('../../api/dailyLog', () => ({ getAll: vi.fn(), saveDay: vi.fn(), deleteDay: vi.fn() }))
vi.mock('../../api/stats', () => ({ getCalendar: vi.fn(), getDashboard: vi.fn() }))

const log = (over: Partial<DailyLog> = {}): DailyLog => ({
  id: 1, date: '2026-11-24', weightKg: null, caloriesKcal: null, notes: null, calorieTarget: null, isCheatDay: false,
  createdAt: '', updatedAt: '', caloriesSetOn: null, ...over,
})

async function open(existing: DailyLog[] = [], earlier: DailyLog[] = [], day: Partial<CalendarDay> = {}) {
  vi.mocked(logApi.getAll).mockImplementation(async (p) => (p?.to === '2026-11-23' ? earlier : existing))
  vi.mocked(statsApi.getCalendar).mockResolvedValue({ today: '2026-11-24', weeks: [], days: [{ date: '2026-11-24', calories: null, target: 1850, status: null, planned: 1881, plannedLogged: false, inGoal: true, weightKg: null, isCheatDay: false, hasNote: false, ...day }] })
  vi.mocked(logApi.saveDay).mockResolvedValue(log())
  // today 1881 + four other free days at 1881: a pool of 9405
  vi.mocked(statsApi.getDashboard).mockResolvedValue({ budget: { todayBudget: 1881, days: ['24', '25', '26', '27', '28'].map((d) => ({ date: `2026-11-${d}`, kcal: 1881, userLogged: false })) } } as never)
  const app = useAppStore()
  app.today = '2026-11-24'
  useLogSheetStore().open('2026-11-24')
  const w = mount(LogSheet, { attachTo: document.body })
  await flushPromises()
  return w
}
const q = (sel: string) => document.body.querySelector(sel) as HTMLInputElement
const click = async (label: string) => { (document.body.querySelector(`[aria-label="${label}"]`) as HTMLElement).click(); await flushPromises() }
const saveButton = () => Array.from(document.body.querySelectorAll('button')).find((b) => /Save day|Delete entry/.test(b.textContent ?? '')) as HTMLButtonElement

beforeEach(() => { setActivePinia(createPinia()); vi.clearAllMocks() })
afterEach(() => { document.body.innerHTML = '' })

describe('LogSheet', () => {
  it('never prefills a value that was not stored', async () => {
    await open([], [log({ date: '2026-11-23', weightKg: 83.8 })])
    expect(q('#kcal').value).toBe('')
    expect(q('#kg').value).toBe('')
    expect(q('#kg').placeholder).toBe('83.8')
    expect(saveButton().textContent).toContain('Delete entry')
  })

  it('starts calories at the target when the day has no planned value', async () => {
    await open([], [], { planned: null })
    expect(q('#kcal').value).toBe('1850')
    expect(saveButton().textContent).toContain('Save day')
  })

  it('leaves calories empty when the day is planned, or when there is no target', async () => {
    await open([], [], { planned: 1881 })
    expect(q('#kcal').value).toBe('')
    document.body.innerHTML = ''
    await open([], [], { planned: null, target: null })
    expect(q('#kcal').value).toBe('')
  })

  it('keeps stored calories instead of the target', async () => {
    await open([log({ caloriesKcal: 1240 })], [], { planned: null })
    expect(q('#kcal').value).toBe('1240')
  })

  it('finds the last weight however long ago it was', async () => {
    await open([], [log({ date: '2026-03-02', weightKg: 91.2 })])
    expect(q('#kg').placeholder).toBe('91.2')
    expect(logApi.getAll).toHaveBeenCalledWith(expect.objectContaining({ to: '2026-11-23' }))
    expect(vi.mocked(logApi.getAll).mock.calls.some(([p]) => p?.to === '2026-11-23' && p.from === undefined)).toBe(true)
  })

  it('only fills the last weight when the weight box is clicked', async () => {
    await open([], [log({ date: '2026-11-23', weightKg: 83.8 })])
    expect(q('#kg').value).toBe('')
    q('#kcal').value = '1500'; q('#kcal').dispatchEvent(new Event('input')); await flushPromises()
    saveButton().click(); await flushPromises()
    expect(logApi.saveDay).toHaveBeenCalledWith('2026-11-24', expect.objectContaining({ weightKg: null }))   // saved without a weight

    document.body.innerHTML = ''
    await open([], [log({ date: '2026-11-23', weightKg: 83.8 })])
    q('#kg').click(); await flushPromises()
    expect(q('#kg').value).toBe('83.8')
  })

  it('does not overwrite a weight that is already there when the box is clicked', async () => {
    await open([log({ weightKg: 83.94, caloriesKcal: 1200 })], [log({ date: '2026-11-23', weightKg: 80 })])
    q('#kg').click(); await flushPromises()
    expect(q('#kg').value).toBe('83.9')
  })

  it('shows what is stored for the day', async () => {
    await open([log({ caloriesKcal: 1240, weightKg: 83.94, notes: 'dinner out', isCheatDay: true })])
    expect(q('#kcal').value).toBe('1240')
    expect(q('#kg').value).toBe('83.9')
    expect(q('#note').value).toBe('dinner out')
    expect((q('#cheat') as HTMLInputElement).checked).toBe(true)
  })

  it('saves 0 kcal as a fasting day, not as empty', async () => {
    await open()
    q('#kcal').value = '0'; q('#kcal').dispatchEvent(new Event('input'))
    await flushPromises()
    expect(document.body.querySelector('[data-test=kcal-hint]')!.textContent).toBe('0 kcal, saved as a fasting day')
    saveButton().click(); await flushPromises()
    expect(logApi.saveDay).toHaveBeenCalledWith('2026-11-24', { weightKg: null, caloriesKcal: 0, notes: null, isCheatDay: false })
  })

  it('quick adds and the stepper change calories by the right amounts', async () => {
    await open([log({ caloriesKcal: 1000 })])
    await click('50 kcal more'); expect(q('#kcal').value).toBe('1050')
    await click('50 kcal less'); await click('50 kcal less'); expect(q('#kcal').value).toBe('950')
    ;(Array.from(document.body.querySelectorAll('.quick button')).find((b) => b.textContent === '+250') as HTMLElement).click()
    await flushPromises()
    expect(q('#kcal').value).toBe('1200')
  })

  it('shows what is left of the budget, and what is over it', async () => {
    await open([log({ caloriesKcal: 1240 })])
    expect(document.body.querySelector('[data-test=kcal-hint]')!.textContent).toBe('641 left of 1881')
    q('#kcal').value = '2000'; q('#kcal').dispatchEvent(new Event('input')); await flushPromises()
    expect(document.body.querySelector('[data-test=kcal-hint]')!.textContent).toBe('119 over 1881')
  })

  it('shows what the rest of the week gets, as the server shares it', async () => {
    await open([log({ caloriesKcal: 1240 })])
    expect(document.body.textContent).toContain('Rest of the week: 1881 a day')
    q('#kcal').value = '2400'; q('#kcal').dispatchEvent(new Event('input')); await flushPromises()
    expect(document.body.textContent).toContain('Rest of the week: 1751 a day')   // (9405 - 2400) / 4
  })

  it('Remove turns into Undo and nothing is deleted before Save', async () => {
    await open([log({ caloriesKcal: 1240, weightKg: 83.9 })])
    await click('Remove calories')
    expect(q('#kcal').value).toBe('')
    expect(document.body.querySelector('[aria-label="Undo removing calories"]')).not.toBeNull()
    expect(logApi.deleteDay).not.toHaveBeenCalled()
    expect(logApi.saveDay).not.toHaveBeenCalled()

    await click('Undo removing calories')
    expect(q('#kcal').value).toBe('1240')
  })

  it('removing both fields offers "Delete entry" and deletes the whole day', async () => {
    await open([log({ caloriesKcal: 1240, weightKg: 83.9, notes: 'x', isCheatDay: true })])
    await click('Remove calories'); await click('Remove weight')
    expect(saveButton().textContent).toContain('Delete entry')
    expect(document.body.querySelector('[data-test=delete-hint]')!.textContent).toContain('note and cheat day')
    saveButton().click(); await flushPromises()
    expect(logApi.deleteDay).toHaveBeenCalledWith('2026-11-24')
    expect(logApi.saveDay).not.toHaveBeenCalled()
  })

  it('converts a weight typed in lbs to kg with 2 decimals', async () => {
    const app = useAppStore(); app.settings.preferredUnit = 'lbs'
    await open()
    q('#kg').value = '185'; q('#kg').dispatchEvent(new Event('input')); await flushPromises()
    saveButton().click(); await flushPromises()
    expect(logApi.saveDay).toHaveBeenCalledWith('2026-11-24', expect.objectContaining({ weightKg: 83.91 }))
  })

  it('Enter saves, and a failed save shows the message and keeps the sheet open', async () => {
    await open([log({ caloriesKcal: 1200 })])
    vi.mocked(logApi.saveDay).mockRejectedValueOnce(new Error('Weight must be between 1 and 700'))
    q('#kcal').dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true })); await flushPromises()
    expect(document.body.textContent).toContain('Weight must be between 1 and 700')
    expect(useLogSheetStore().isOpen).toBe(true)

    q('#kcal').dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true })); await flushPromises()
    expect(useLogSheetStore().isOpen).toBe(false)
  })

  it('refreshes the app after saving so statistics are refetched', async () => {
    await open([log({ caloriesKcal: 1200 })])
    const before = useAppStore().version
    saveButton().click(); await flushPromises()
    expect(useAppStore().version).toBe(before + 1)
  })

  it('moves to the previous day and loads it', async () => {
    await open()
    await click('Previous day')
    expect(logApi.getAll).toHaveBeenCalledWith(expect.objectContaining({ from: '2026-11-23' }))
  })
})
