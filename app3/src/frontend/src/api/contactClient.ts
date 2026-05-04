// TODO: implement submitContact in STORY-012

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

export async function submitContact(
  _payload: ContactSubmissionRequest,
): Promise<SubmitResult> {
  // TODO: remove mock when STORY-012 is complete
  return { kind: 'failure' }
}
