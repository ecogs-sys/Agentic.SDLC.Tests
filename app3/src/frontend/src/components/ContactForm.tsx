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

    const allErrors = validateContactForm(updated)
    setErrors(prev => ({
      ...prev,
      [name]: allErrors[name as keyof ContactFormInput],
    }))
  }

  async function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()

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
    <form className="contact-form" onSubmit={handleSubmit} noValidate>
      <div className="contact-form__field">
        <label className="contact-form__label" htmlFor="fullName">Full Name</label>
        <input
          className="contact-form__input"
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
          <span id="fullName-error" role="alert" className="contact-form__error">
            {errors.fullName}
          </span>
        )}
      </div>

      <div className="contact-form__field">
        <label className="contact-form__label" htmlFor="email">Email</label>
        <input
          className="contact-form__input"
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
          <span id="email-error" role="alert" className="contact-form__error">
            {errors.email}
          </span>
        )}
      </div>

      <div className="contact-form__field">
        <label className="contact-form__label" htmlFor="phone">Phone</label>
        <input
          className="contact-form__input"
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
          <span id="phone-error" role="alert" className="contact-form__error">
            {errors.phone}
          </span>
        )}
      </div>

      <div className="contact-form__field">
        <label className="contact-form__label" htmlFor="subject">Subject</label>
        <input
          className="contact-form__input"
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
          <span id="subject-error" role="alert" className="contact-form__error">
            {errors.subject}
          </span>
        )}
      </div>

      <div className="contact-form__field">
        <label className="contact-form__label" htmlFor="message">Message</label>
        <textarea
          className="contact-form__textarea"
          id="message"
          name="message"
          value={fields.message}
          onChange={handleChange}
          disabled={isSubmitting}
          aria-describedby={errors.message ? 'message-error' : 'message-counter'}
          aria-invalid={!!errors.message}
          rows={6}
        />
        <div className="contact-form__message-footer">
          {errors.message ? (
            <span id="message-error" role="alert" className="contact-form__error">
              {errors.message}
            </span>
          ) : (
            <span />
          )}
          <CharCounter id="message-counter" current={fields.message.length} max={MESSAGE_MAX} />
        </div>
      </div>

      <div className="contact-form__actions">
        <button className="contact-form__submit" type="submit" disabled={isSubmitting}>
          {isSubmitting ? 'Sending…' : 'Send Message'}
        </button>
      </div>
    </form>
  )
}

export default ContactForm
