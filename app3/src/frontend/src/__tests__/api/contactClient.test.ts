/**
 * Vitest unit tests for submitContact (STORY-012)
 *
 * Acceptance criteria covered:
 *   AC1 — HTTP 200 → { kind: 'success', id, receivedAt }
 *   AC1 — HTTP 201 → { kind: 'success', id, receivedAt }
 *   AC2 — HTTP 400 → { kind: 'validation', errors }
 *   AC3 — HTTP >= 500 → { kind: 'failure' }
 *   AC4 — Network error (fetch rejects) → { kind: 'failure' }
 *
 * Also verifies:
 *   - POST to /api/contact
 *   - Content-Type: application/json header
 *   - JSON-encoded body
 */

import { describe, it, expect, vi, beforeEach, afterEach, type Mock } from 'vitest'
import { submitContact, type ContactSubmissionRequest } from '../../api/contactClient'

// ── helpers ────────────────────────────────────────────────────────────────────

/** Baseline valid payload used across all tests. */
const payload: ContactSubmissionRequest = {
  fullName: 'Jane Doe',
  email: 'jane@example.com',
  phone: '+1-800-555-0100',
  subject: 'Test subject',
  message: 'Hello from the test suite.',
}

/** Build a minimal Response-like object that fetch would return. */
function makeResponse(status: number, body: unknown): Response {
  return {
    status,
    json: vi.fn().mockResolvedValue(body),
  } as unknown as Response
}

// ── setup / teardown ──────────────────────────────────────────────────────────

beforeEach(() => {
  vi.stubGlobal('fetch', vi.fn())
})

afterEach(() => {
  vi.unstubAllGlobals()
})

// ── AC1 — HTTP 200 → { kind: 'success' } ──────────────────────────────────────

describe('submitContact — HTTP 200 (success)', () => {
  it('returns { kind: "success" } with id and receivedAt on 200 (happy path)', async () => {
    const mockFetch = fetch as Mock
    mockFetch.mockResolvedValue(
      makeResponse(200, { id: 'abc-123', receivedAt: '2026-05-05T10:00:00Z' }),
    )

    const result = await submitContact(payload)

    expect(result).toEqual({
      kind: 'success',
      id: 'abc-123',
      receivedAt: '2026-05-05T10:00:00Z',
    })
  })

  it('does NOT return kind "failure" on a 200 response (negative)', async () => {
    const mockFetch = fetch as Mock
    mockFetch.mockResolvedValue(
      makeResponse(200, { id: 'x', receivedAt: '2026-01-01T00:00:00Z' }),
    )

    const result = await submitContact(payload)

    expect(result.kind).not.toBe('failure')
    expect(result.kind).not.toBe('validation')
  })
})

// ── AC1 — HTTP 201 → { kind: 'success' } ──────────────────────────────────────

describe('submitContact — HTTP 201 (success)', () => {
  it('returns { kind: "success" } with id and receivedAt on 201 (happy path)', async () => {
    const mockFetch = fetch as Mock
    mockFetch.mockResolvedValue(
      makeResponse(201, { id: 'def-456', receivedAt: '2026-05-05T11:00:00Z' }),
    )

    const result = await submitContact(payload)

    expect(result).toEqual({
      kind: 'success',
      id: 'def-456',
      receivedAt: '2026-05-05T11:00:00Z',
    })
  })

  it('does NOT return kind "failure" or "validation" on a 201 response (negative)', async () => {
    const mockFetch = fetch as Mock
    mockFetch.mockResolvedValue(
      makeResponse(201, { id: 'y', receivedAt: '2026-01-01T00:00:00Z' }),
    )

    const result = await submitContact(payload)

    expect(result.kind).toBe('success')
  })
})

// ── AC2 — HTTP 400 → { kind: 'validation', errors } ──────────────────────────

describe('submitContact — HTTP 400 (validation error)', () => {
  it('returns { kind: "validation" } with errors map on 400 (happy path)', async () => {
    const mockFetch = fetch as Mock
    const errors = {
      email: ['Email is required', 'Email is not a valid format'],
      fullName: ['Full name is required'],
    }
    mockFetch.mockResolvedValue(makeResponse(400, { errors }))

    const result = await submitContact(payload)

    expect(result).toEqual({ kind: 'validation', errors })
  })

  it('does NOT return kind "success" or "failure" on a 400 response (negative)', async () => {
    const mockFetch = fetch as Mock
    mockFetch.mockResolvedValue(makeResponse(400, { errors: {} }))

    const result = await submitContact(payload)

    expect(result.kind).toBe('validation')
    expect(result.kind).not.toBe('success')
    expect(result.kind).not.toBe('failure')
  })

  it('preserves all error arrays exactly as returned by the server', async () => {
    const mockFetch = fetch as Mock
    const errors = {
      message: ['Message is too long'],
      phone: ['Phone number is invalid'],
      subject: ['Subject must not be blank'],
    }
    mockFetch.mockResolvedValue(makeResponse(400, { errors }))

    const result = await submitContact(payload)

    if (result.kind !== 'validation') throw new Error('Expected validation kind')
    expect(result.errors).toStrictEqual(errors)
  })
})

// ── AC3 — HTTP >= 500 → { kind: 'failure' } ───────────────────────────────────

describe('submitContact — HTTP >= 500 (server error → failure)', () => {
  it('returns { kind: "failure" } on HTTP 500 (happy path)', async () => {
    const mockFetch = fetch as Mock
    mockFetch.mockResolvedValue(makeResponse(500, null))

    const result = await submitContact(payload)

    expect(result).toEqual({ kind: 'failure' })
  })

  it('returns { kind: "failure" } on HTTP 503 (edge: another 5xx code)', async () => {
    const mockFetch = fetch as Mock
    mockFetch.mockResolvedValue(makeResponse(503, null))

    const result = await submitContact(payload)

    expect(result).toEqual({ kind: 'failure' })
  })

  it('does NOT return kind "success" or "validation" on a 500 response (negative)', async () => {
    const mockFetch = fetch as Mock
    mockFetch.mockResolvedValue(makeResponse(500, null))

    const result = await submitContact(payload)

    expect(result.kind).not.toBe('success')
    expect(result.kind).not.toBe('validation')
  })

  it('returns { kind: "failure" } on an unexpected 4xx (e.g. 422) that is not 400', async () => {
    const mockFetch = fetch as Mock
    mockFetch.mockResolvedValue(makeResponse(422, null))

    const result = await submitContact(payload)

    expect(result).toEqual({ kind: 'failure' })
  })
})

// ── AC4 — Network error (fetch rejects) → { kind: 'failure' } ────────────────

describe('submitContact — network error (fetch rejects)', () => {
  it('returns { kind: "failure" } when fetch throws a TypeError (happy path)', async () => {
    const mockFetch = fetch as Mock
    mockFetch.mockRejectedValue(new TypeError('Failed to fetch'))

    const result = await submitContact(payload)

    expect(result).toEqual({ kind: 'failure' })
  })

  it('returns { kind: "failure" } when fetch rejects with a generic Error (edge case)', async () => {
    const mockFetch = fetch as Mock
    mockFetch.mockRejectedValue(new Error('Network disconnected'))

    const result = await submitContact(payload)

    expect(result).toEqual({ kind: 'failure' })
  })

  it('does NOT throw; it resolves to { kind: "failure" } rather than propagating (negative)', async () => {
    const mockFetch = fetch as Mock
    mockFetch.mockRejectedValue(new TypeError('offline'))

    await expect(submitContact(payload)).resolves.toEqual({ kind: 'failure' })
  })
})

// ── Request shape verification ─────────────────────────────────────────────────

describe('submitContact — request shape', () => {
  it('sends a POST request to /api/contact (happy path)', async () => {
    const mockFetch = fetch as Mock
    mockFetch.mockResolvedValue(
      makeResponse(200, { id: 'r1', receivedAt: '2026-05-05T00:00:00Z' }),
    )

    await submitContact(payload)

    expect(mockFetch).toHaveBeenCalledOnce()
    const [url, init] = mockFetch.mock.calls[0] as [string, RequestInit]
    expect(url).toBe('/api/contact')
    expect(init.method).toBe('POST')
  })

  it('sets Content-Type: application/json header', async () => {
    const mockFetch = fetch as Mock
    mockFetch.mockResolvedValue(
      makeResponse(200, { id: 'r2', receivedAt: '2026-05-05T00:00:00Z' }),
    )

    await submitContact(payload)

    const [, init] = mockFetch.mock.calls[0] as [string, RequestInit]
    const headers = init.headers as Record<string, string>
    expect(headers['Content-Type']).toBe('application/json')
  })

  it('sends the payload as a JSON-encoded body string', async () => {
    const mockFetch = fetch as Mock
    mockFetch.mockResolvedValue(
      makeResponse(200, { id: 'r3', receivedAt: '2026-05-05T00:00:00Z' }),
    )

    await submitContact(payload)

    const [, init] = mockFetch.mock.calls[0] as [string, RequestInit]
    expect(init.body).toBe(JSON.stringify(payload))
  })

  it('does NOT include unexpected HTTP methods (negative: not GET/PUT/DELETE)', async () => {
    const mockFetch = fetch as Mock
    mockFetch.mockResolvedValue(
      makeResponse(200, { id: 'r4', receivedAt: '2026-05-05T00:00:00Z' }),
    )

    await submitContact(payload)

    const [, init] = mockFetch.mock.calls[0] as [string, RequestInit]
    expect(init.method).not.toBe('GET')
    expect(init.method).not.toBe('PUT')
    expect(init.method).not.toBe('DELETE')
  })
})
