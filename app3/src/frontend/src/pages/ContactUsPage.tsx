import { useState } from 'react'
import ContactForm from '../components/ContactForm'

type PageStatus = 'idle' | 'success' | 'error'

function ContactUsPage() {
  const [status, setStatus] = useState<PageStatus>('idle')

  function handleSuccess() {
    setStatus('success')
  }

  function handleServerErrors(_errors: Record<string, string[]>) {
    // Server-side field errors are displayed inline in ContactForm.
    // Clear any page-level banner so only the field errors are visible.
    setStatus('idle')
  }

  function handleFailure() {
    setStatus('error')
  }

  return (
    <main>
      <h1>Contact Us</h1>

      {status === 'success' && (
        <div role="status" aria-live="polite">
          Thank you! Your message has been sent successfully.
        </div>
      )}

      {status === 'error' && (
        <div role="alert">
          We couldn&apos;t send your message. Please try again later.
        </div>
      )}

      <ContactForm
        onSuccess={handleSuccess}
        onServerErrors={handleServerErrors}
        onFailure={handleFailure}
      />
    </main>
  )
}

export default ContactUsPage
