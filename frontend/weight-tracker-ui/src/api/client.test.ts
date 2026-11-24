import { describe, it, expect } from 'vitest'
import { errorMessage } from './client'

describe('errorMessage', () => {
  it('uses the ProblemDetails title', () => {
    expect(errorMessage({ response: { data: { title: 'Bad date' } }, message: 'x' })).toBe('Bad date')
  })
  it('never returns [object Object] for an object body without a title', () => {
    const m = errorMessage({ response: { data: { foo: 1 } }, message: 'Request failed' })
    expect(m).toBe('Request failed')
  })
  it('reads the first validation error', () => {
    expect(errorMessage({ response: { data: { errors: { Date: ['Date is required'] } } } })).toBe('Date is required')
  })
  it('accepts a plain string body', () => {
    expect(errorMessage({ response: { data: 'nope' } })).toBe('nope')
  })
})
