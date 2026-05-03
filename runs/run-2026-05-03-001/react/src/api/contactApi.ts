export type SubmitResult =
  | { kind: 'ok' }
  | { kind: 'validation'; fieldErrors: Record<string, string[]> }
  | { kind: 'error'; message: string }

export interface ContactPayload {
  fullName: string
  email: string
  message: string
}

export async function submitContact(payload: ContactPayload): Promise<SubmitResult> {
  const baseUrl = import.meta.env.VITE_API_BASE_URL as string | undefined
  const url = `${baseUrl ?? ''}/api/contact`

  let response: Response

  try {
    response = await fetch(url, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        fullName: payload.fullName,
        email: payload.email,
        message: payload.message,
      }),
    })
  } catch {
    return { kind: 'error', message: 'Network error. Please check your connection and try again.' }
  }

  if (response.status === 201 || response.status === 200) {
    return { kind: 'ok' }
  }

  if (response.status === 400) {
    try {
      const body = (await response.json()) as { errors?: Record<string, string[]> }
      const fieldErrors: Record<string, string[]> = body.errors ?? {}
      return { kind: 'validation', fieldErrors }
    } catch {
      return { kind: 'error', message: 'The server returned an invalid response.' }
    }
  }

  if (response.status >= 500) {
    return { kind: 'error', message: 'A server error occurred. Please try again later.' }
  }

  return { kind: 'error', message: `Unexpected response status: ${response.status}` }
}
