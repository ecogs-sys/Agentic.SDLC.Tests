import { useState } from 'react'
import { Link } from 'react-router-dom'
import { ContactForm } from '../components/ContactForm'

type PageState =
  | { kind: 'idle' }
  | { kind: 'success' }
  | { kind: 'error'; message: string }

export function ContactPage() {
  const [pageState, setPageState] = useState<PageState>({ kind: 'idle' })

  function handleSuccess() {
    setPageState({ kind: 'success' })
  }

  function handleError(message: string) {
    setPageState({ kind: 'error', message })
  }

  function handleSendAnother() {
    setPageState({ kind: 'idle' })
  }

  return (
    <main>
      <h1>Contact Us</h1>

      {pageState.kind === 'success' ? (
        <div role="status">
          <p>Your message has been sent successfully. We will get back to you soon.</p>
          <button type="button" onClick={handleSendAnother}>
            Send another message
          </button>
        </div>
      ) : (
        <>
          {pageState.kind === 'error' && (
            <div role="alert" style={{ marginBottom: '1rem', color: 'red' }}>
              {pageState.message}
            </div>
          )}
          <ContactForm onSuccess={handleSuccess} onError={handleError} />
        </>
      )}

      <p>
        <Link to="/">Back to home</Link>
      </p>
    </main>
  )
}
