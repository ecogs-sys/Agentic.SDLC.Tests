import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi, type Mock } from 'vitest'
import { MemoryRouter } from 'react-router-dom'
import * as contactApi from '../api/contactApi'
import { ContactPage } from './ContactPage'

vi.mock('../api/contactApi')

const submitContact = contactApi.submitContact as Mock

beforeEach(() => {
  vi.clearAllMocks()
})

function renderPage() {
  return render(
    <MemoryRouter>
      <ContactPage />
    </MemoryRouter>,
  )
}

// ---------------------------------------------------------------------------
// AC5 — ContactPage renders ContactForm without any authentication interaction
// ---------------------------------------------------------------------------
describe('AC5: ContactPage renders the contact form', () => {
  it('happy path — renders the Submit button and form fields without authentication', () => {
    renderPage()

    expect(screen.getByRole('button', { name: /send message/i })).toBeInTheDocument()
    expect(screen.getByLabelText(/full name/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/email address/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/message/i)).toBeInTheDocument()
  })

  it('edge case — page heading is present', () => {
    renderPage()

    expect(screen.getByRole('heading', { name: /contact us/i })).toBeInTheDocument()
  })
})

// ---------------------------------------------------------------------------
// AC4 — generic error: banner shown above form; form values preserved
// ---------------------------------------------------------------------------
describe('AC4: generic error response shows banner and preserves form values', () => {
  it('happy path — renders error banner and keeps field values after Network failure', async () => {
    submitContact.mockResolvedValue({ kind: 'error', message: 'Network failure' })
    const user = userEvent.setup()
    renderPage()

    await user.type(screen.getByLabelText(/full name/i), 'Alice Smith')
    await user.type(screen.getByLabelText(/email address/i), 'alice@example.com')
    await user.type(screen.getByLabelText(/message/i), 'Hello there')
    await user.click(screen.getByRole('button', { name: /send message/i }))

    await waitFor(() =>
      expect(screen.getByRole('alert')).toBeInTheDocument(),
    )

    expect(screen.getByRole('alert')).toHaveTextContent('Network failure')

    // Form values must be preserved
    expect(screen.getByLabelText(/full name/i)).toHaveValue('Alice Smith')
    expect(screen.getByLabelText(/email address/i)).toHaveValue('alice@example.com')
    expect(screen.getByLabelText(/message/i)).toHaveValue('Hello there')
  })

  it('edge case — form (Submit button) is still present after error', async () => {
    submitContact.mockResolvedValue({ kind: 'error', message: 'Network failure' })
    const user = userEvent.setup()
    renderPage()

    await user.type(screen.getByLabelText(/full name/i), 'Bob')
    await user.type(screen.getByLabelText(/email address/i), 'bob@example.com')
    await user.type(screen.getByLabelText(/message/i), 'Hi')
    await user.click(screen.getByRole('button', { name: /send message/i }))

    await waitFor(() => expect(screen.getByRole('alert')).toBeInTheDocument())

    expect(screen.getByRole('button', { name: /send message/i })).toBeInTheDocument()
  })
})

// ---------------------------------------------------------------------------
// AC3 — successful submission shows confirmation; "Send another message" resets form
// ---------------------------------------------------------------------------
describe('AC3: successful submission shows confirmation message', () => {
  it('happy path — replaces form with success message and shows Send another message button', async () => {
    submitContact.mockResolvedValue({ kind: 'ok' })
    const user = userEvent.setup()
    renderPage()

    await user.type(screen.getByLabelText(/full name/i), 'Alice')
    await user.type(screen.getByLabelText(/email address/i), 'alice@example.com')
    await user.type(screen.getByLabelText(/message/i), 'Hello')
    await user.click(screen.getByRole('button', { name: /send message/i }))

    await waitFor(() =>
      expect(screen.queryByRole('button', { name: /send message/i })).not.toBeInTheDocument(),
    )

    expect(screen.getByRole('status')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /send another message/i })).toBeInTheDocument()
  })

  it('edge case — clicking Send another message restores the empty form', async () => {
    submitContact.mockResolvedValue({ kind: 'ok' })
    const user = userEvent.setup()
    renderPage()

    await user.type(screen.getByLabelText(/full name/i), 'Alice')
    await user.type(screen.getByLabelText(/email address/i), 'alice@example.com')
    await user.type(screen.getByLabelText(/message/i), 'Hello')
    await user.click(screen.getByRole('button', { name: /send message/i }))

    await waitFor(() =>
      expect(screen.getByRole('button', { name: /send another message/i })).toBeInTheDocument(),
    )

    await user.click(screen.getByRole('button', { name: /send another message/i }))

    expect(screen.getByRole('button', { name: /send message/i })).toBeInTheDocument()
    expect(screen.getByLabelText(/full name/i)).toHaveValue('')
    expect(screen.getByLabelText(/email address/i)).toHaveValue('')
    expect(screen.getByLabelText(/message/i)).toHaveValue('')
  })
})
