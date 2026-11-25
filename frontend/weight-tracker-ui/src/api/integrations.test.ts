import { describe, it, expect, vi, beforeEach } from 'vitest'
import * as api from './integrations'
import client from './client'
import { localToday } from '../utils/dates'

vi.mock('./client', () => ({ default: { get: vi.fn(), post: vi.fn(), delete: vi.fn() } }))
const today = { params: { today: localToday() } }

beforeEach(() => {
  vi.clearAllMocks()
  vi.mocked(client.get).mockResolvedValue({ data: [] })
  vi.mocked(client.post).mockResolvedValue({ data: { authorizeUrl: 'https://authorize' } })
  vi.mocked(client.delete).mockResolvedValue({})
})

describe('integrations api', () => {
  it('lists the providers', async () => {
    expect(await api.getAll()).toEqual([])
    expect(client.get).toHaveBeenCalledWith('/integrations')
  })
  it('connects, with a redirect or a code', async () => {
    expect(await api.connect('fatsecret')).toBe('https://authorize')
    expect(client.post).toHaveBeenLastCalledWith('/integrations/fatsecret/connect', { usePin: false })
    await api.connect('fatsecret', true)
    expect(client.post).toHaveBeenLastCalledWith('/integrations/fatsecret/connect', { usePin: true })
    await api.verifyPin('fatsecret', '1234')
    expect(client.post).toHaveBeenLastCalledWith('/integrations/fatsecret/verify', { verifier: '1234' })
  })
  it('sends the local date with everything that depends on it', async () => {
    await api.sync('fatsecret')
    expect(client.post).toHaveBeenLastCalledWith('/integrations/fatsecret/sync', null, today)
    await api.preview('fatsecret', '2026-10-01', '2026-10-31')
    expect(client.post).toHaveBeenLastCalledWith('/integrations/fatsecret/sync/preview', { from: '2026-10-01', to: '2026-10-31' }, today)
    const decisions = [{ date: '2026-10-01', field: 'Calories' as const, choice: 'Keep' as const }]
    await api.apply('fatsecret', 'p1', decisions)
    expect(client.post).toHaveBeenLastCalledWith('/integrations/fatsecret/sync/apply', { previewId: 'p1', decisions }, today)
  })
  it('disconnects', async () => {
    await api.disconnect('fatsecret')
    expect(client.delete).toHaveBeenCalledWith('/integrations/fatsecret')
  })
})
