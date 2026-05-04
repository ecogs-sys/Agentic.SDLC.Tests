// TODO: implement validateContactForm in STORY-009

export interface ContactFormInput {
  fullName: string
  email: string
  phone: string
  subject: string
  message: string
}

export type ContactFormErrors = Partial<Record<keyof ContactFormInput, string>>

export function validateContactForm(
  _input: ContactFormInput,
): ContactFormErrors {
  // TODO: remove mock when STORY-009 is complete
  return {}
}
