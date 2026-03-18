import { describe, it, expect, vi, beforeEach } from 'vitest'
import { exportData, importData } from './data'
import client from './client'

vi.mock('./client', () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
  },
}))

const mockPayload = {
  settings: { heightCm: 175, preferredUnit: 'kg', tdeeKcal: 2000 },
  dailyLogs: [{ id: 1, date: '2024-01-10', weightKg: 75, caloriesKcal: 2000, notes: null, createdAt: '', updatedAt: '' }],
  goals: [],
  calorieGoals: [],
}

describe('exportData', () => {
  beforeEach(() => vi.clearAllMocks())

  it('calls GET /data/export', async () => {
    vi.mocked(client.get).mockResolvedValue({ data: mockPayload })

    await exportData()

    expect(client.get).toHaveBeenCalledWith('/data/export')
  })

  it('returns the response data', async () => {
    vi.mocked(client.get).mockResolvedValue({ data: mockPayload })

    const result = await exportData()

    expect(result).toEqual(mockPayload)
  })
})

describe('importData', () => {
  beforeEach(() => vi.clearAllMocks())

  it('calls POST /data/import with the provided data', async () => {
    vi.mocked(client.post).mockResolvedValue({ data: undefined })

    await importData(mockPayload)

    expect(client.post).toHaveBeenCalledWith('/data/import', mockPayload)
  })

  it('resolves without a return value', async () => {
    vi.mocked(client.post).mockResolvedValue({ data: undefined })

    const result = await importData(mockPayload)

    expect(result).toBeUndefined()
  })
})
