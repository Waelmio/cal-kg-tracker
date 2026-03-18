import client from './client'

export async function exportData(): Promise<object> {
  const res = await client.get('/data/export')
  return res.data
}

export async function importData(data: object): Promise<void> {
  await client.post('/data/import', data)
}
