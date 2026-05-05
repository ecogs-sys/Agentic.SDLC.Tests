import { useState } from 'react'
import type { FormEvent, ChangeEvent } from 'react'
import CharCounter from './CharCounter'
import { validateContactForm } from '../validation/contactSchema'
import type { ContactFormErrors, ContactFormInput } from '../validation/contactSchema'
import { submitContact } from '../api/contactClient'
import type { ContactSubmissionRequest } from '../api/contactClient'

const MESSAGE_MAX = 1000

const EMPTY_FIELDS: ContactFormInput = {
  fullName: '',
  email: '',
  phone: '',
  subject: '',
  message: '',
}

interface ContactFormProps {
  onSuccess: () => void
  onServerErrors: (errors: Record<string, string[]>) => void
  onFailure: () => void
}

function ContactForm({ onSuccess, onServerErrors, onFailure }: ContactFormProps) {
  const [fields, setFields] = useState<ContactFormInput>(EMPTY_FIELDS)
  const [errors, setErrors] = useState<ContactFormErrors>({})
  const [isSubmitting, setIsSubmitting] = useState(false)

  function handleChange(
    e: ChangeEvent<HTMLInputElement | HTMLTextAreaElement>,
  ) {
    const { name, value } = e.target
    const updated = { ...fields, [name]: value }
    setFields(updated)

    // Field-level validation on change
    const allErrors = validateContactForm(updated)
    setErrors(prev => ({
      ...prev,
      [name]: allErrors[name as keyof ContactFormInput],
    }))
  }

  async function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()

    // Full validation before submission
    const allErrors = validateContactForm(fields)
    if (Object.keys(allErrors).length > 0) {
      setErrors(allErrors)
      return
    }

    setIsSubmitting(true)
    try {
      const payload: ContactSubmissionRequest = { ...fields }
      const result = await submitContact(payload)

      if (result.kind === 'success') {
        setFields(EMPTY_FIELDS)
        setErrors({})
        onSuccess()
      } else if (result.kind === 'validation') {
        // Map server-side errors (string[]) to first message per field
        const fieldErrors: ContactFormErrors = {}
        for (const [key, messages] of Object.entries(result.errors)) {
          const k = key as keyof ContactFormErrors
          if (messages.length > 0) {
            fieldErrors[k] = messages[0]
          }
        }
        setErrors(fieldErrors)
        onServerErrors(result.errors)
      } else {
        onFailure()
      }
    } catch {
      onFailure()
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <form onSubmit={handleSubmit} noValidate>
      <div>
        <label htmlFor="fullName">Full Name</label>
        <input
          id="fullName"
          name="fullName"
          type="text"
          value={fields.fullName}
          onChange={handleChange}
          disabled={isSubmitting}
          aria-describedby={errors.fullName ? 'fullName-error' : undefined}
          aria-invalid={!!errors.fullName}
        />
        {errors.fullName && (
          <span id="fullName-error" role="alert">
            {errors.fullName}
          </span>
        )}
      </div>

      <div>
        <label htmlFor="email">Email</label>
        <input
          id="email"
          name="email"
          type="email"
          value={fields.email}
          onChange={handleChange}
          disabled={isSubmitting}
          aria-describedby={errors.email ? 'email-error' : undefined}
          aria-invalid={!!errors.email}
        />
        {errors.email && (
          <span id="email-error" role="alert">
            {errors.email}
          </span>
        )}
      </div>

      <div>
        <label htmlFor="phone">Phone</label>
        <input
          id="phone"
          name="phone"
          type="tel"
          value={fields.phone}
          onChange={handleChange}
          disabled={isSubmitting}
          aria-describedby={errors.phone ? 'phone-error' : undefined}
          aria-invalid={!!errors.phone}
        />
        {errors.phone && (
          <span id="phone-error" role="alert">
            {errors.phone}
          </span>
        )}
      </div>

      <div>
        <label htmlFor="subject">Subject</label>
        <input
          id="subject"
          name="subject"
          type="text"
          value={fields.subject}
          onChange={handleChange}
          disabled={isSubmitting}
          aria-describedby={errors.subject ? 'subject-error' : undefined}
          aria-invalid={!!errors.subject}
        />
        {errors.subject && (
          <span id="subject-error" role="alert">
            {errors.subject}
          </span>
        )}
      </div>

      <div>
        <label htmlFor="message">Message</label>
        <textarea
          id="message"
          name="message"
          value={fields.message}
          onChange={handleChange}
          disabled={isSubmitting}
          aria-describedby={
            errors.message
              ? 'message-error'
              : 'message-counter'
          }
          aria-invalid={!!errors.message}
          rows={6}
        />
        <CharCounter current={fields.message.length} max={MESSAGE_MAX} />
        {errors.message && (
          <span id="message-error" role="alert">
            {errors.message}
          </span>
        )}
      </div>

      <button type="submit" disabled={isSubmitting}>
        {isSubmitting ? 'Sending…' : 'Send Message'}
      </button>
    </form>
  )
}

export default ContactForm
