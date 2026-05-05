export interface ContactSubmissionRequest {
  fullName: string
  email: string
  phone: string
  subject: string
  message: string
}

export type SubmitResult =
  | { kind: 'success'; id: string; receivedAt: string }
  | { kind: 'validation'; errors: Record<string, string[]> }
  | { kind: 'failure' }

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? '/api'

export async function submitContact(
  payload: ContactSubmissionRequest,
): Promise<SubmitResult> {
  let response: Response
  try {
    response = await fetch(`${API_BASE}/contact`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    })
  } catch {
    return { kind: 'failure' }
  }

  if (response.status === 200 || response.status === 201) {
    const json = (await response.json()) as { id: string; receivedAt: string }
    return { kind: 'success', id: json.id, receivedAt: json.receivedAt }
  }

  if (response.status === 400) {
    const json = (await response.json()) as {
      errors: Record<string, string[]>
    }
    return { kind: 'validation', errors: json.errors }
  }

  return { kind: 'failure' }
}
