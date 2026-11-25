import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import IntegrationsSection from './IntegrationsSection.vue'
import { useAppStore } from '../../stores/app'
import * as api from '../../api/integrations'
import type { Integration, SyncResult } from '../../types'

vi.mock('../../api/integrations', () => ({ getAll: vi.fn(), connect: vi.fn(), verifyPin: vi.fn(), sync: vi.fn(), preview: vi.fn(), apply: vi.fn(), disconnect: vi.fn() }))

const base: Integration = {
  id: 'fatsecret', name: 'FatSecret', configured: true, requiredSettings: ['FATSECRET_CONSUMER_KEY', 'FATSECRET_CONSUMER_SECRET'],
  attribution: { text: 'Powered by fatsecret', url: 'https://platform.fatsecret.com' },
  status: null, connectedAt: null, lastSyncAt: null, lastSyncStatus: null, lastError: null,
}
const result: SyncResult = { from: '', to: '', calories: { written: 3, unchanged: 0, kept: 0 }, weight: { written: 1, unchanged: 0, kept: 0 } }

async function open(p: Partial<Integration> = {}, query = '') {
  vi.mocked(api.getAll).mockResolvedValue([{ ...base, ...p }])
  const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/settings', component: { template: '<i />' } }] })
  await router.push('/settings' + query)
  const w = mount(IntegrationsSection, { global: { plugins: [router] }, attachTo: document.body })
  await flushPromises()
  return { w, router }
}
const button = (text: string) => Array.from(document.body.querySelectorAll('button')).find((b) => b.textContent!.trim() === text)
const click = async (text: string) => { button(text)!.click(); await flushPromises() }

beforeEach(() => { setActivePinia(createPinia()); vi.clearAllMocks(); vi.mocked(api.sync).mockResolvedValue(result) })
afterEach(() => { document.body.innerHTML = '' })

describe('IntegrationsSection', () => {
  it('disabled without keys: names the variables and nothing can be clicked', async () => {
    const { w } = await open({ configured: false })
    expect(w.get('.top').text()).toContain('Disabled')
    expect(w.text()).toContain('FATSECRET_CONSUMER_KEY and FATSECRET_CONSUMER_SECRET')
    expect(w.findAll('button').map((b) => [b.text(), b.element.disabled])).toEqual([['Connect', true]])
  })

  it('disabled while connected: says the connection is kept', async () => {
    const { w } = await open({ configured: false, status: 'Connected' })
    expect(w.get('.top').text()).toContain('Disabled')
    expect(w.text()).toContain('Your connection is kept')
    expect(w.findAll('button')).toHaveLength(0)
  })

  it('always shows the required attribution', async () => {
    const { w } = await open()
    expect(w.get('.credit').attributes('href')).toBe('https://platform.fatsecret.com')
    expect(w.get('.credit').text()).toBe('Powered by fatsecret')
  })

  it('connects with a code when the redirect does not work, then syncs', async () => {
    vi.mocked(api.connect).mockResolvedValue('https://authorize')
    const { w } = await open()
    await click("FatSecret doesn't come back here? Use a code")
    expect(api.connect).toHaveBeenCalledWith('fatsecret', true)
    expect(w.get('.pin a').attributes('href')).toBe('https://authorize')
    await w.get('.pin input').setValue('1234')
    await w.get('.pin').trigger('submit')
    await flushPromises()
    expect(api.verifyPin).toHaveBeenCalledWith('fatsecret', '1234')
    expect(api.sync).toHaveBeenCalledWith('fatsecret')
  })

  it('syncs, says what happened and refreshes everything', async () => {
    const { w } = await open({ status: 'Connected' })
    const before = useAppStore().version
    await click('Sync now')
    expect(w.text()).toContain('Synced: 3 calorie values and 1 weight updated.')
    expect(useAppStore().version).toBe(before + 1)
  })

  it('shows a failure once, and refreshes the status too', async () => {
    const { w } = await open({ status: 'Connected' })
    vi.mocked(api.sync).mockRejectedValue(new Error("FatSecret can't be reached."))
    vi.mocked(api.getAll).mockResolvedValue([{ ...base, status: 'Connected', lastSyncAt: new Date().toISOString(), lastSyncStatus: 'Failed', lastError: "FatSecret can't be reached." }])
    await click('Sync now')
    expect(w.text().split("FatSecret can't be reached.")).toHaveLength(2)
    expect(w.text()).toContain(', failed.')
  })

  it('needs reconnect: explains, and offers to connect again instead of syncing', async () => {
    const { w } = await open({ status: 'NeedsReconnect', lastError: 'FatSecret no longer accepts the connection.' })
    expect(w.text()).toContain('no longer accepts')
    expect(button('Sync now')).toBeUndefined()
    expect(button('Connect again')).toBeDefined()
  })

  it('back from the provider: runs the first sync, once', async () => {
    const { w, router } = await open({ status: 'Connected' }, '?integration=fatsecret&result=connected')
    expect(api.sync).toHaveBeenCalledTimes(1)
    expect(w.text()).toContain('Synced')
    expect(router.currentRoute.value.query).toEqual({})
  })

  it.each([['denied', 'not approved'], ['expired', 'too long'], ['failed', 'failed'], ['whatever', 'failed']])('back from the provider with %s: says so and syncs nothing', async (r, text) => {
    const { w } = await open({}, `?integration=fatsecret&result=${r}`)
    expect(w.get('[role=alert]').text()).toContain(text)
    expect(api.sync).not.toHaveBeenCalled()
  })

  it('asks before disconnecting', async () => {
    await open({ status: 'Connected' })
    await click('Disconnect')
    expect(api.disconnect).not.toHaveBeenCalled()
    document.body.querySelector<HTMLElement>('.danger')!.click()
    await flushPromises()
    expect(api.disconnect).toHaveBeenCalledWith('fatsecret')
  })
})
