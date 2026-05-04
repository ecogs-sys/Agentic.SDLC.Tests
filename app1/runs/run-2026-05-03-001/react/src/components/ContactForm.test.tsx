import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi, type Mock } from 'vitest'
import * as contactApi from '../api/contactApi'
import { ContactForm } from './ContactForm'

vi.mock('../api/contactApi')

const submitContact = contactApi.submitContact as Mock

const defaultProps = {
  onSuccess: vi.fn(),
  onError: vi.fn(),
}

function renderForm(props = defaultProps) {
  return render(<ContactForm {...props} />)
}

beforeEach(() => {
  vi.clearAllMocks()
})

// ---------------------------------------------------------------------------
// AC2 — blank submit: no API call, three distinct field-level errors
// ---------------------------------------------------------------------------
describe('AC2: blank form submission', () => {
  it('happy path — shows three unique required-field errors and does not call submitContact', async () => {
    const user = userEvent.setup()
    renderForm()

    await user.click(screen.getByRole('button', { name: /send message/i }))

    expect(submitContact).not.toHaveBeenCalled()

    const alerts = screen.getAllByRole('alert')
    expect(alerts).toHaveLength(3)

    const messages = alerts.map((el) => el.textContent ?? '')
    // all three messages must be non-empty
    messages.forEach((msg) => expect(msg.length).toBeGreaterThan(0))
    // all three messages must be uniquely worded
    expect(new Set(messages).size).toBe(3)
  })

  it('edge case — error messages are individually associated with their fields', async () => {
    const user = userEvent.setup()
    renderForm()

    await user.click(screen.getByRole('button', { name: /send message/i }))

    expect(screen.getByText('Full name is required.')).toBeInTheDocument()
    expect(screen.getByText('Email address is required.')).toBeInTheDocument()
    expect(screen.getByText('Message cannot be empty.')).toBeInTheDocument()
  })
})

// ---------------------------------------------------------------------------
// AC3 — invalid email blur validation (before submit)
// ---------------------------------------------------------------------------
describe('AC3: email blur validation', () => {
  it('happy path — shows invalid-email error after blurring with a malformed value', async () => {
    const user = userEvent.setup()
    renderForm()

    const emailInput = screen.getByLabelText(/email address/i)
    await user.click(emailInput)
    await user.type(emailInput, 'not-an-email')
    await user.tab()

    expect(screen.getByText('Please enter a valid email address.')).toBeInTheDocument()
    expect(submitContact).not.toHaveBeenCalled()
  })

  it('edge case — no email error shown when a valid email is entered and blurred', async () => {
    const user = userEvent.setup()
    renderForm()

    const emailInput = screen.getByLabelText(/email address/i)
    await user.click(emailInput)
    await user.type(emailInput, 'user@example.com')
    await user.tab()

    expect(screen.queryByText('Please enter a valid email address.')).not.toBeInTheDocument()
    expect(screen.queryByText('Email address is required.')).not.toBeInTheDocument()
  })
})

// ---------------------------------------------------------------------------
// AC4 — valid submission calls submitContact once with trimmed values
// ---------------------------------------------------------------------------
describe('AC4: valid submission', () => {
  it('happy path — calls submitContact exactly once with trimmed field values', async () => {
    submitContact.mockResolvedValue({ kind: 'ok' })
    const user = userEvent.setup()
    renderForm()

    await user.type(screen.getByLabelText(/full name/i), '  Alice Smith  ')
    await user.type(screen.getByLabelText(/email address/i), '  alice@example.com  ')
    await user.type(screen.getByLabelText(/message/i), '  Hello world  ')
    await user.click(screen.getByRole('button', { name: /send message/i }))

    await waitFor(() => expect(submitContact).toHaveBeenCalledTimes(1))
    expect(submitContact).toHaveBeenCalledWith({
      fullName: 'Alice Smith',
      email: 'alice@example.com',
      message: 'Hello world',
    })
  })

  it('edge case — does not call submitContact when only some fields are filled', async () => {
    const user = userEvent.setup()
    renderForm()

    await user.type(screen.getByLabelText(/full name/i), 'Alice')
    // leave email and message blank
    await user.click(screen.getByRole('button', { name: /send message/i }))

    expect(submitContact).not.toHaveBeenCalled()
  })
})

// ---------------------------------------------------------------------------
// AC5 — submit button disabled while request is in flight
// ---------------------------------------------------------------------------
describe('AC5: in-flight button state', () => {
  it('happy path — button is disabled while submitting and re-enabled after resolution', async () => {
    let resolveSubmit!: (value: contactApi.SubmitResult) => void
    submitContact.mockReturnValue(
      new Promise<contactApi.SubmitResult>((resolve) => {
        resolveSubmit = resolve
      }),
    )

    const user = userEvent.setup()
    renderForm()

    await user.type(screen.getByLabelText(/full name/i), 'Alice')
    await user.type(screen.getByLabelText(/email address/i), 'alice@example.com')
    await user.type(screen.getByLabelText(/message/i), 'Hello')
    await user.click(screen.getByRole('button', { name: /send message/i }))

    await waitFor(() =>
      expect(screen.getByRole('button', { name: /sending/i })).toBeDisabled(),
    )

    resolveSubmit({ kind: 'ok' })

    await waitFor(() =>
      expect(screen.getByRole('button', { name: /send message/i })).toBeEnabled(),
    )
  })

  it('edge case — button remains enabled when validation prevents submission', async () => {
    const user = userEvent.setup()
    renderForm()

    await user.click(screen.getByRole('button', { name: /send message/i }))

    expect(screen.getByRole('button', { name: /send message/i })).toBeEnabled()
    expect(submitContact).not.toHaveBeenCalled()
  })
})

// ---------------------------------------------------------------------------
// AC6 — server validation errors rendered next to the relevant field
// ---------------------------------------------------------------------------
describe('AC6: server validation error response', () => {
  it('happy path — renders server email error next to the email field', async () => {
    submitContact.mockResolvedValue({
      kind: 'validation',
      fieldErrors: { email: ['server says no'] },
    })

    const user = userEvent.setup()
    renderForm()

    await user.type(screen.getByLabelText(/full name/i), 'Alice')
    await user.type(screen.getByLabelText(/email address/i), 'alice@example.com')
    await user.type(screen.getByLabelText(/message/i), 'Hello')
    await user.click(screen.getByRole('button', { name: /send message/i }))

    await waitFor(() =>
      expect(screen.getByText('server says no')).toBeInTheDocument(),
    )
  })

  it('edge case — other fields have no errors when only email has a server error', async () => {
    submitContact.mockResolvedValue({
      kind: 'validation',
      fieldErrors: { email: ['server says no'] },
    })

    const user = userEvent.setup()
    renderForm()

    await user.type(screen.getByLabelText(/full name/i), 'Alice')
    await user.type(screen.getByLabelText(/email address/i), 'alice@example.com')
    await user.type(screen.getByLabelText(/message/i), 'Hello')
    await user.click(screen.getByRole('button', { name: /send message/i }))

    await waitFor(() => expect(screen.getByText('server says no')).toBeInTheDocument())

    const alerts = screen.getAllByRole('alert')
    expect(alerts).toHaveLength(1)
    expect(alerts[0]).toHaveTextContent('server says no')
  })
})

// ---------------------------------------------------------------------------
// AC7 — successful submission clears form and calls onSuccess
// ---------------------------------------------------------------------------
describe('AC7: successful submission', () => {
  it('happy path — calls onSuccess and clears all field values', async () => {
    submitContact.mockResolvedValue({ kind: 'ok' })
    const onSuccess = vi.fn()
    const onError = vi.fn()

    const user = userEvent.setup()
    render(<ContactForm onSuccess={onSuccess} onError={onError} />)

    await user.type(screen.getByLabelText(/full name/i), 'Alice')
    await user.type(screen.getByLabelText(/email address/i), 'alice@example.com')
    await user.type(screen.getByLabelText(/message/i), 'Hello')
    await user.click(screen.getByRole('button', { name: /send message/i }))

    await waitFor(() => expect(onSuccess).toHaveBeenCalledTimes(1))

    expect(screen.getByLabelText(/full name/i)).toHaveValue('')
    expect(screen.getByLabelText(/email address/i)).toHaveValue('')
    expect(screen.getByLabelText(/message/i)).toHaveValue('')
    expect(screen.queryAllByRole('alert')).toHaveLength(0)
  })

  it('edge case — onError is not called on a successful submission', async () => {
    submitContact.mockResolvedValue({ kind: 'ok' })
    const onSuccess = vi.fn()
    const onError = vi.fn()

    const user = userEvent.setup()
    render(<ContactForm onSuccess={onSuccess} onError={onError} />)

    await user.type(screen.getByLabelText(/full name/i), 'Alice')
    await user.type(screen.getByLabelText(/email address/i), 'alice@example.com')
    await user.type(screen.getByLabelText(/message/i), 'Hello')
    await user.click(screen.getByRole('button', { name: /send message/i }))

    await waitFor(() => expect(onSuccess).toHaveBeenCalledTimes(1))
    expect(onError).not.toHaveBeenCalled()
  })
})

// ---------------------------------------------------------------------------
// AC8 — error response preserves field values and calls onError
// ---------------------------------------------------------------------------
describe('AC8: error response', () => {
  it('happy path — calls onError with the message and preserves field values', async () => {
    submitContact.mockResolvedValue({ kind: 'error', message: 'Something went wrong' })
    const onSuccess = vi.fn()
    const onError = vi.fn()

    const user = userEvent.setup()
    render(<ContactForm onSuccess={onSuccess} onError={onError} />)

    await user.type(screen.getByLabelText(/full name/i), 'Alice')
    await user.type(screen.getByLabelText(/email address/i), 'alice@example.com')
    await user.type(screen.getByLabelText(/message/i), 'Hello')
    await user.click(screen.getByRole('button', { name: /send message/i }))

    await waitFor(() => expect(onError).toHaveBeenCalledWith('Something went wrong'))

    expect(screen.getByLabelText(/full name/i)).toHaveValue('Alice')
    expect(screen.getByLabelText(/email address/i)).toHaveValue('alice@example.com')
    expect(screen.getByLabelText(/message/i)).toHaveValue('Hello')
  })

  it('edge case — onSuccess is not called on an error response', async () => {
    submitContact.mockResolvedValue({ kind: 'error', message: 'Boom' })
    const onSuccess = vi.fn()
    const onError = vi.fn()

    const user = userEvent.setup()
    render(<ContactForm onSuccess={onSuccess} onError={onError} />)

    await user.type(screen.getByLabelText(/full name/i), 'Alice')
    await user.type(screen.getByLabelText(/email address/i), 'alice@example.com')
    await user.type(screen.getByLabelText(/message/i), 'Hello')
    await user.click(screen.getByRole('button', { name: /send message/i }))

    await waitFor(() => expect(onError).toHaveBeenCalledTimes(1))
    expect(onSuccess).not.toHaveBeenCalled()
  })
})
