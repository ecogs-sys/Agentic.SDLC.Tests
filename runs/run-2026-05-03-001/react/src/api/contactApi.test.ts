import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { submitContact } from './contactApi'
import type { ContactPayload } from './contactApi'

const payload: ContactPayload = {
  fullName: 'Jane Doe',
  email: 'jane@example.com',
  message: 'Hello there',
}

function makeFetchResponse(status: number, body: unknown): Response {
  return {
    status,
    ok: status >= 200 && status < 300,
    json: () => Promise.resolve(body),
    text: () => Promise.resolve(JSON.stringify(body)),
  } as unknown as Response
}

describe('submitContact', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  // AC1: 201 response resolves to { kind: 'ok' }
  it('AC1: resolves to { kind: "ok" } on a 201 response', async () => {
    vi.mocked(fetch).mockResolvedValue(makeFetchResponse(201, {}))

    const result = await submitContact(payload)

    expect(result).toEqual({ kind: 'ok' })
  })

  // AC2: 400 response with validation errors resolves to { kind: 'validation', fieldErrors }
  it('AC2: resolves to { kind: "validation", fieldErrors } on a 400 response', async () => {
    vi.mocked(fetch).mockResolvedValue(
      makeFetchResponse(400, { errors: { email: ['bad email'] } }),
    )

    const result = await submitContact(payload)

    expect(result).toEqual({
      kind: 'validation',
      fieldErrors: { email: ['bad email'] },
    })
  })

  // AC3: 500 response resolves to { kind: 'error', message: <non-empty string> }
  it('AC3: resolves to { kind: "error", message } on a 500 response', async () => {
    vi.mocked(fetch).mockResolvedValue(makeFetchResponse(500, {}))

    const result = await submitContact(payload)

    expect(result.kind).toBe('error')
    if (result.kind === 'error') {
      expect(result.message.length).toBeGreaterThan(0)
    }
  })

  // AC4: Network failure resolves to { kind: 'error', message } and does NOT throw
  it('AC4: resolves to { kind: "error", message } when fetch rejects (network failure)', async () => {
    vi.mocked(fetch).mockRejectedValue(new TypeError('Failed to fetch'))

    const result = await submitContact(payload)

    expect(result.kind).toBe('error')
    if (result.kind === 'error') {
      expect(typeof result.message).toBe('string')
      expect(result.message.length).toBeGreaterThan(0)
    }
  })

  // AC5: Request uses POST, Content-Type: application/json, body has keys fullName, email, message
  it('AC5: sends POST with Content-Type application/json and correct body keys', async () => {
    vi.mocked(fetch).mockResolvedValue(makeFetchResponse(201, {}))

    await submitContact(payload)

    expect(fetch).toHaveBeenCalledOnce()
    const [_url, init] = vi.mocked(fetch).mock.calls[0] as [string, RequestInit]

    expect(init.method).toBe('POST')
    expect((init.headers as Record<string, string>)['Content-Type']).toBe('application/json')

    const body = JSON.parse(init.body as string) as Record<string, unknown>
    expect(body).toHaveProperty('fullName')
    expect(body).toHaveProperty('email')
    expect(body).toHaveProperty('message')
  })

  // AC2 edge case: 400 with malformed body (json() rejects) resolves to { kind: 'error' }
  it('AC2 edge: resolves to { kind: "error" } when 400 body is not valid JSON', async () => {
    const malformedResponse = {
      status: 400,
      ok: false,
      json: () => Promise.reject(new SyntaxError('Unexpected token')),
      text: () => Promise.resolve('not json'),
    } as unknown as Response
    vi.mocked(fetch).mockResolvedValue(malformedResponse)

    const result = await submitContact(payload)

    expect(result.kind).toBe('error')
  })

  // AC2 edge case: 400 body is valid JSON but has no `errors` key — fieldErrors defaults to {}
  it('AC2 edge: resolves to { kind: "validation", fieldErrors: {} } when 400 body has no errors key', async () => {
    vi.mocked(fetch).mockResolvedValue(makeFetchResponse(400, { detail: 'bad request' }))

    const result = await submitContact(payload)

    expect(result).toEqual({ kind: 'validation', fieldErrors: {} })
  })

  // AC3 edge case: 503 response also resolves to { kind: 'error', message: <non-empty string> }
  it('AC3 edge: resolves to { kind: "error", message } on a 503 response', async () => {
    vi.mocked(fetch).mockResolvedValue(makeFetchResponse(503, {}))

    const result = await submitContact(payload)

    expect(result.kind).toBe('error')
    if (result.kind === 'error') {
      expect(result.message.length).toBeGreaterThan(0)
    }
  })

  // AC5 edge case: 200 response (not 201) also resolves to { kind: 'ok' }
  it('AC5 edge: resolves to { kind: "ok" } on a 200 response', async () => {
    vi.mocked(fetch).mockResolvedValue(makeFetchResponse(200, {}))

    const result = await submitContact(payload)

    expect(result).toEqual({ kind: 'ok' })
  })

  // AC7: Module reads VITE_API_BASE_URL and uses it as the fetch URL base
  it('AC7: uses VITE_API_BASE_URL as the base of the fetch URL', async () => {
    vi.stubEnv('VITE_API_BASE_URL', 'https://api.example.com')
    vi.mocked(fetch).mockResolvedValue(makeFetchResponse(201, {}))

    await submitContact(payload)

    expect(fetch).toHaveBeenCalledOnce()
    const [url] = vi.mocked(fetch).mock.calls[0] as [string, RequestInit]
    expect(url).toMatch(/^https:\/\/api\.example\.com/)
  })
})
