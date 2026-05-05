export interface ContactFormInput {
  fullName: string
  email: string
  phone: string
  subject: string
  message: string
}

export type ContactFormErrors = Partial<Record<keyof ContactFormInput, string>>

/**
 * RFC 5322-style email regex.
 * Covers the common internet email address format:
 *   local-part@domain.tld
 * where local-part allows word chars, dots, plus, percent, hyphens,
 * and the domain must have at least one dot with a 2+ char TLD.
 */
const EMAIL_REGEX =
  /^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*\.[a-zA-Z]{2,}$/

/**
 * Validates the contact form fields against the rules defined in TECH-001.
 *
 * Returns an empty object when all fields are valid (success), or a partial
 * record mapping each invalid field name to its human-readable error message.
 */
export function validateContactForm(input: ContactFormInput): ContactFormErrors {
  const errors: ContactFormErrors = {}

  // fullName: required, trimmed length >= 1, max 200
  if (input.fullName.trim().length === 0) {
    errors.fullName = 'Full name is required.'
  } else if (input.fullName.trim().length > 200) {
    errors.fullName = 'Full name must be 200 characters or fewer.'
  }

  // email: required, RFC 5322-style regex
  if (input.email.trim().length === 0) {
    errors.email = 'Email is required.'
  } else if (!EMAIL_REGEX.test(input.email.trim())) {
    errors.email = 'Email is not a valid format.'
  }

  // phone: required, trimmed length >= 1, max 50
  if (input.phone.trim().length === 0) {
    errors.phone = 'Phone is required.'
  } else if (input.phone.trim().length > 50) {
    errors.phone = 'Phone must be 50 characters or fewer.'
  }

  // subject: required, trimmed length >= 1, max 200
  if (input.subject.trim().length === 0) {
    errors.subject = 'Subject is required.'
  } else if (input.subject.trim().length > 200) {
    errors.subject = 'Subject must be 200 characters or fewer.'
  }

  // message: required, trimmed length >= 1, max 1000
  if (input.message.trim().length === 0) {
    errors.message = 'Message is required.'
  } else if (input.message.trim().length > 1000) {
    errors.message = 'Message must be 1000 characters or fewer.'
  }

  return errors
}
