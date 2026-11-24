import axios from 'axios'

/** Readable message from a ProblemDetails body, a plain string body, or the transport error. */
export function errorMessage(err: any): string {
  const data = err?.response?.data
  if (typeof data === 'string' && data.trim()) return data
  if (data && typeof data === 'object') {
    if (typeof data.title === 'string' && data.title) return data.title
    if (typeof data.detail === 'string' && data.detail) return data.detail
    if (data.errors && typeof data.errors === 'object') {
      const first = Object.values(data.errors).flat()[0]
      if (typeof first === 'string') return first
    }
  }
  return err?.message || 'Unknown error'
}

const client = axios.create({
  baseURL: '/api',
  headers: { 'Content-Type': 'application/json' },
})

client.interceptors.response.use(
  (res) => res,
  (err) => {
    return Promise.reject(new Error(errorMessage(err)))
  },
)

export default client
