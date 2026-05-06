import { useState } from 'react'
import ContactForm from '../components/ContactForm'

type PageStatus = 'idle' | 'success' | 'error'

function ContactUsPage() {
  const [status, setStatus] = useState<PageStatus>('idle')

  function handleSuccess() {
    setStatus('success')
  }

  function handleServerErrors(_errors: Record<string, string[]>) {
    setStatus('idle')
  }

  function handleFailure() {
    setStatus('error')
  }

  return (
    <main className="contact-page">
      <header className="contact-page__header">
        <h1 className="contact-page__title">Contact Us</h1>
        <p className="contact-page__subtitle">
          We&apos;d love to hear from you. Send us a message and we&apos;ll respond as soon as possible.
        </p>
      </header>

      <div className="contact-page__card">
        {status === 'success' && (
          <div
            role="status"
            aria-live="polite"
            className="contact-page__banner contact-page__banner--success"
          >
            Thank you! Your message has been sent successfully.
          </div>
        )}

        {status === 'error' && (
          <div
            role="alert"
            className="contact-page__banner contact-page__banner--error"
          >
            We couldn&apos;t send your message. Please try again later.
          </div>
        )}

        <ContactForm
          onSuccess={handleSuccess}
          onServerErrors={handleServerErrors}
          onFailure={handleFailure}
        />
      </div>
    </main>
  )
}

export default ContactUsPage
