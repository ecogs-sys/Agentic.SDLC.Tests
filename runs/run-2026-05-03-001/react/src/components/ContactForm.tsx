import { type ChangeEvent, type FocusEvent, type FormEvent, useState } from 'react'
import { submitContact } from '../api/contactApi'

export interface ContactFormProps {
  onSuccess: () => void
  onError: (message: string) => void
}

interface FormValues {
  fullName: string
  email: string
  message: string
}

interface FormErrors {
  fullName: string
  email: string
  message: string
}

const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

function validateFullName(value: string): string {
  const trimmed = value.trim()
  if (trimmed.length === 0) return 'Full name is required.'
  if (trimmed.length > 200) return 'Full name must be 200 characters or fewer.'
  return ''
}

function validateEmail(value: string): string {
  const trimmed = value.trim()
  if (trimmed.length === 0) return 'Email address is required.'
  if (!EMAIL_REGEX.test(trimmed)) return 'Please enter a valid email address.'
  if (trimmed.length > 320) return 'Email address must be 320 characters or fewer.'
  return ''
}

function validateMessage(value: string): string {
  const trimmed = value.trim()
  if (trimmed.length === 0) return 'Message cannot be empty.'
  if (trimmed.length > 5000) return 'Message must be 5000 characters or fewer.'
  return ''
}

function validateAll(values: FormValues): FormErrors {
  return {
    fullName: validateFullName(values.fullName),
    email: validateEmail(values.email),
    message: validateMessage(values.message),
  }
}

const emptyValues: FormValues = { fullName: '', email: '', message: '' }
const emptyErrors: FormErrors = { fullName: '', email: '', message: '' }

export function ContactForm({ onSuccess, onError }: ContactFormProps) {
  const [values, setValues] = useState<FormValues>(emptyValues)
  const [errors, setErrors] = useState<FormErrors>(emptyErrors)
  const [submitting, setSubmitting] = useState(false)

  function handleChange(e: ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) {
    const { name, value } = e.target
    setValues((prev) => ({ ...prev, [name]: value }))
  }

  function handleBlur(e: FocusEvent<HTMLInputElement | HTMLTextAreaElement>) {
    const { name, value } = e.target
    let fieldError = ''
    if (name === 'fullName') fieldError = validateFullName(value)
    else if (name === 'email') fieldError = validateEmail(value)
    else if (name === 'message') fieldError = validateMessage(value)
    setErrors((prev) => ({ ...prev, [name]: fieldError }))
  }

  async function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()

    const nextErrors = validateAll(values)
    setErrors(nextErrors)

    const hasErrors =
      nextErrors.fullName !== '' || nextErrors.email !== '' || nextErrors.message !== ''

    if (hasErrors) return

    setSubmitting(true)

    const result = await submitContact({
      fullName: values.fullName.trim(),
      email: values.email.trim(),
      message: values.message.trim(),
    })

    setSubmitting(false)

    if (result.kind === 'ok') {
      setValues(emptyValues)
      setErrors(emptyErrors)
      onSuccess()
      return
    }

    if (result.kind === 'validation') {
      const serverErrors: FormErrors = { ...emptyErrors }
      for (const [field, messages] of Object.entries(result.fieldErrors)) {
        const key = field as keyof FormErrors
        if (key in serverErrors && messages.length > 0) {
          serverErrors[key] = messages[0]
        }
      }
      setErrors(serverErrors)
      return
    }

    // kind === 'error'
    onError(result.message)
  }

  return (
    <form onSubmit={handleSubmit} noValidate>
      <div>
        <label htmlFor="fullName">
          Full name <span aria-hidden="true">*</span>
        </label>
        <input
          id="fullName"
          name="fullName"
          type="text"
          value={values.fullName}
          onChange={handleChange}
          onBlur={handleBlur}
          aria-required="true"
          aria-describedby={errors.fullName ? 'fullName-error' : undefined}
        />
        {errors.fullName && (
          <span id="fullName-error" role="alert">
            {errors.fullName}
          </span>
        )}
      </div>

      <div>
        <label htmlFor="email">
          Email address <span aria-hidden="true">*</span>
        </label>
        <input
          id="email"
          name="email"
          type="text"
          value={values.email}
          onChange={handleChange}
          onBlur={handleBlur}
          aria-required="true"
          aria-describedby={errors.email ? 'email-error' : undefined}
        />
        {errors.email && (
          <span id="email-error" role="alert">
            {errors.email}
          </span>
        )}
      </div>

      <div>
        <label htmlFor="message">
          Message <span aria-hidden="true">*</span>
        </label>
        <textarea
          id="message"
          name="message"
          value={values.message}
          onChange={handleChange}
          onBlur={handleBlur}
          aria-required="true"
          aria-describedby={errors.message ? 'message-error' : undefined}
        />
        {errors.message && (
          <span id="message-error" role="alert">
            {errors.message}
          </span>
        )}
      </div>

      <button type="submit" disabled={submitting}>
        {submitting ? 'Sending…' : 'Send message'}
      </button>
    </form>
  )
}
