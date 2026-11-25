import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import SyncPeriodModal from './SyncPeriodModal.vue'
import { useAppStore } from '../../stores/app'
import * as api from '../../api/integrations'
import type { SyncRow } from '../../types'

vi.mock('../../api/integrations', () => ({ preview: vi.fn(), apply: vi.fn() }))

const row = (date: string, r: Partial<SyncRow> = {}): SyncRow =>
  ({ date, field: 'Calories', kind: 'Conflict', appValue: 2000, value: 1900, setInAdvance: false, imported: false, suggested: 'Keep', ...r })

const ROWS = [
  row('2026-11-05'),
  row('2026-11-06', { appValue: 2500, value: 2400, setInAdvance: true, suggested: 'Replace' }),
  row('2026-11-07', { kind: 'New', appValue: null, value: 1800, suggested: 'Replace' }),
  row('2026-11-07', { field: 'Weight', kind: 'New', appValue: null, value: 81.9, suggested: 'Replace' }),
]

const q = <T extends Element = HTMLElement>(sel: string) => document.body.querySelector<T>(sel)!
const text = () => document.body.textContent!
const button = (label: string) => Array.from(document.body.querySelectorAll('button')).find((b) => b.textContent!.trim() === label)!
const click = async (el: HTMLElement) => { el.click(); await flushPromises() }
const setDate = async (sel: string, v: string) => { const i = q<HTMLInputElement>(sel); i.value = v; i.dispatchEvent(new Event('input')); await flushPromises() }

async function open(rows: SyncRow[] | null = ROWS) {
  useAppStore().today = '2026-11-24'
  vi.mocked(api.preview).mockResolvedValue({ previewId: 'p1', from: '2026-11-01', to: '2026-11-24', rows: rows ?? [], equalCount: 10, missingCount: 2 })
  const w = mount(SyncPeriodModal, { props: { id: 'fatsecret', name: 'FatSecret' }, attachTo: document.body })
  await flushPromises()
  return w
}
async function compare(rows: SyncRow[] | null = ROWS) {
  const w = await open(rows)
  q('form').dispatchEvent(new Event('submit'))
  await flushPromises()
  return w
}
const decisions = () => vi.mocked(api.apply).mock.calls[0][2].map((d) => `${d.date}:${d.field}:${d.choice}`)

beforeEach(() => { setActivePinia(createPinia()); vi.clearAllMocks(); vi.mocked(api.apply).mockResolvedValue({ added: 2, replaced: 1, kept: 1, changed: 0 }) })
afterEach(() => { document.body.innerHTML = '' })

describe('SyncPeriodModal', () => {
  it('proposes the last month and refuses impossible periods', async () => {
    await open()
    expect([q<HTMLInputElement>('#sp-from').value, q<HTMLInputElement>('#sp-to').value]).toEqual(['2026-10-24', '2026-11-24'])
    await setDate('#sp-from', '2025-11-23')
    expect(text()).toContain('up to 12 months')
    expect(button('Compare').disabled).toBe(true)
    await setDate('#sp-from', '2025-11-24')
    expect(button('Compare').disabled).toBe(false)
    await setDate('#sp-from', '2026-11-25')
    expect(text()).toContain('before the end date')
  })

  it('the 12-month shortcut is a period that is accepted', async () => {
    await open()
    await click(button('Last 12 months'))
    expect(q<HTMLInputElement>('#sp-from').value).toBe('2025-11-25')
    expect(button('Compare').disabled).toBe(false)
  })

  it('starts from the suggested choices, and applies nothing by itself', async () => {
    await compare()
    expect(text()).toContain('3 to write, 1 kept')
    expect(text()).toContain('set in advance')
    expect(api.apply).not.toHaveBeenCalled()
  })

  it('sends one decision per row', async () => {
    const w = await compare()
    await click(q('[data-test="2026-11-05:Calories"] button:last-child'))
    await click(q('[data-test="2026-11-07:Weight"] button:first-child'))
    expect(text()).toContain('automatic sync fills empty days')
    await click(button('Apply'))
    expect(vi.mocked(api.apply).mock.calls[0].slice(0, 2)).toEqual(['fatsecret', 'p1'])
    expect(decisions()).toEqual(['2026-11-05:Calories:Replace', '2026-11-06:Calories:Replace', '2026-11-07:Calories:Replace', '2026-11-07:Weight:Keep'])
    expect(w.emitted('done')).toEqual([['2 added, 1 replaced, 1 kept.']])
  })

  it('bulk choices apply to the rows on screen', async () => {
    await compare()
    const filter = q<HTMLInputElement>('.only input')
    filter.checked = true
    filter.dispatchEvent(new Event('change'))
    await flushPromises()
    expect(document.body.querySelectorAll('[data-test]')).toHaveLength(2)
    await click(button('Keep all'))
    await click(button('Apply'))
    expect(decisions()).toEqual(['2026-11-05:Calories:Keep', '2026-11-06:Calories:Keep', '2026-11-07:Calories:Replace', '2026-11-07:Weight:Replace'])
  })

  it('says when everything matches', async () => {
    await compare([])
    expect(text()).toContain('Everything matches.')
    expect(button('Apply')).toBeUndefined()
    expect(button('Close')).toBeDefined()
  })

  it('shows weights in the unit of the user', async () => {
    useAppStore().settings.preferredUnit = 'lbs'
    await compare([row('2026-11-07', { field: 'Weight', appValue: 80, value: 81 })])
    expect(text()).toContain('176.4 lbs')
    expect(text()).toContain('178.6 lbs')
  })

  it('an error keeps the choices', async () => {
    await compare()
    vi.mocked(api.apply).mockRejectedValue(new Error('This comparison has expired. Compare again.'))
    await click(button('Apply'))
    expect(q('[role=alert]').textContent).toContain('expired')
    expect(text()).toContain('3 to write, 1 kept')
  })
})
