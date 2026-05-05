/**
 * STORY-011 / STORY-014 component tests
 * Covers the six acceptance criteria from STORY-011 (ContactUsPage, ContactForm,
 * CharCounter) as called out in the STORY-014 test-story brief:
 *
 *  AC-1  Submit blocked when all fields are empty; inline errors shown per field.
 *  AC-2  Live CharCounter updates as user types; flips red when length > 1000.
 *  AC-3  Successful submission (kind:'success') clears the form + shows success banner.
 *  AC-4  HTTP 400 (kind:'validation') maps server errors onto inline field errors.
 *  AC-5  HTTP 500 / failure (kind:'failure') shows the generic error banner.
 *  AC-6  Network error (kind:'failure') shows the same generic error banner.
 */

import { render, screen, within, waitFor, fireEvent } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi, beforeEach, type Mock } from 'vitest'
import * as contactClientModule from '../../api/contactClient'
import ContactUsPage from '../../pages/ContactUsPage'

// ---------------------------------------------------------------------------
// Mock the API client module so no real network calls are made.
// ---------------------------------------------------------------------------
vi.mock('../../api/contactClient')

// Clear all mock state between tests so call counts don't leak across cases.
beforeEach(() => {
  vi.clearAllMocks()
})

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const VALID_FIELDS = {
  fullName: 'Jane Doe',
  email: 'jane@example.com',
  phone: '555-0100',
  subject: 'Hello',
  message: 'This is a test message.',
} as const

/** Fill all five form fields with the provided values. */
async function fillForm(
  user: ReturnType<typeof userEvent.setup>,
  overrides: Partial<typeof VALID_FIELDS> = {},
) {
  const values = { ...VALID_FIELDS, ...overrides }
  await user.type(screen.getByLabelText(/full name/i), values.fullName)
  await user.type(screen.getByLabelText(/email/i), values.email)
  await user.type(screen.getByLabelText(/phone/i), values.phone)
  await user.type(screen.getByLabelText(/subject/i), values.subject)
  await user.type(screen.getByLabelText(/message/i), values.message)
}

// ---------------------------------------------------------------------------
// AC-1 — Submit blocked when fields are empty; inline errors shown per field
// ---------------------------------------------------------------------------
describe('AC-1: empty-form submission is blocked and shows inline errors', () => {
  it('happy path: all five inline errors appear and the API is NOT called', async () => {
    const user = userEvent.setup()
    render(<ContactUsPage />)

    await user.click(screen.getByRole('button', { name: /send message/i }))

    // One role="alert" span per field
    const alerts = screen.getAllByRole('alert')
    const alertTexts = alerts.map(a => a.textContent ?? '')

    expect(alertTexts.some(t => /full name/i.test(t))).toBe(true)
    expect(alertTexts.some(t => /email/i.test(t))).toBe(true)
    expect(alertTexts.some(t => /phone/i.test(t))).toBe(true)
    expect(alertTexts.some(t => /subject/i.test(t))).toBe(true)
    expect(alertTexts.some(t => /message/i.test(t))).toBe(true)

    // The API client must not have been invoked
    expect(contactClientModule.submitContact).not.toHaveBeenCalled()
  })

  it('edge case: partially filled form (only email missing) still blocks and shows the email error', async () => {
    const user = userEvent.setup()
    render(<ContactUsPage />)

    // Fill every field except email
    await user.type(screen.getByLabelText(/full name/i), 'Jane Doe')
    await user.type(screen.getByLabelText(/phone/i), '555-0100')
    await user.type(screen.getByLabelText(/subject/i), 'Hello')
    await user.type(screen.getByLabelText(/message/i), 'Some message.')

    await user.click(screen.getByRole('button', { name: /send message/i }))

    const alerts = screen.getAllByRole('alert')
    expect(alerts.some(a => /email/i.test(a.textContent ?? ''))).toBe(true)
    expect(contactClientModule.submitContact).not.toHaveBeenCalled()
  })
})

// ---------------------------------------------------------------------------
// AC-2 — Live CharCounter updates and flips red over 1000 characters
// ---------------------------------------------------------------------------
describe('AC-2: CharCounter updates live and turns red when over 1000 chars', () => {
  it('happy path: counter reflects the current character count as user types', async () => {
    const user = userEvent.setup()
    render(<ContactUsPage />)

    const textarea = screen.getByLabelText(/message/i)
    const counter = screen.getByTestId('char-counter')

    // Initially 0 / 1000
    expect(counter).toHaveTextContent('0 / 1000')

    await user.type(textarea, 'Hello')
    expect(counter).toHaveTextContent('5 / 1000')
  })

  it('happy path: counter does NOT have red color when length is exactly 1000', () => {
    render(<ContactUsPage />)

    const textarea = screen.getByLabelText(/message/i)
    const counter = screen.getByTestId('char-counter')

    // Use fireEvent for large strings to avoid slow character-by-character typing
    fireEvent.change(textarea, { target: { value: 'a'.repeat(1000) } })

    expect(counter).toHaveTextContent('1000 / 1000')
    // style.color should be empty / not red when at exactly the limit
    expect(counter).not.toHaveStyle({ color: 'red' })
  })

  it('edge case: counter turns red when input length exceeds 1000', () => {
    render(<ContactUsPage />)

    const textarea = screen.getByLabelText(/message/i)
    const counter = screen.getByTestId('char-counter')

    fireEvent.change(textarea, { target: { value: 'a'.repeat(1001) } })

    expect(counter).toHaveTextContent('1001 / 1000')
    // jsdom normalises 'red' -> 'rgb(255, 0, 0)' in computed styles
    expect(counter).toHaveStyle({ color: 'rgb(255, 0, 0)' })
  })

  it('edge case: counter returns to normal color after removing excess characters', () => {
    render(<ContactUsPage />)

    const textarea = screen.getByLabelText(/message/i)
    const counter = screen.getByTestId('char-counter')

    // Set to 1001 chars first, then drop back to 1000
    fireEvent.change(textarea, { target: { value: 'a'.repeat(1001) } })
    expect(counter).toHaveStyle({ color: 'rgb(255, 0, 0)' })

    fireEvent.change(textarea, { target: { value: 'a'.repeat(1000) } })
    expect(counter).not.toHaveStyle({ color: 'rgb(255, 0, 0)' })
  })
})

// ---------------------------------------------------------------------------
// AC-3 — Successful submission: form cleared + success banner shown
// ---------------------------------------------------------------------------
describe('AC-3: successful submission clears the form and shows the success banner', () => {
  it('happy path: fields are emptied and role="status" banner appears', async () => {
    ;(contactClientModule.submitContact as Mock).mockResolvedValue({
      kind: 'success',
      id: 'abc-123',
      receivedAt: '2026-05-05T00:00:00Z',
    })

    const user = userEvent.setup()
    render(<ContactUsPage />)

    await fillForm(user)
    await user.click(screen.getByRole('button', { name: /send message/i }))

    // Success banner
    await waitFor(() =>
      expect(screen.getByRole('status')).toHaveTextContent(
        /thank you.*message has been sent/i,
      ),
    )

    // All input fields cleared
    expect(screen.getByLabelText(/full name/i)).toHaveValue('')
    expect(screen.getByLabelText(/email/i)).toHaveValue('')
    expect(screen.getByLabelText(/phone/i)).toHaveValue('')
    expect(screen.getByLabelText(/subject/i)).toHaveValue('')
    expect(screen.getByLabelText(/message/i)).toHaveValue('')

    // No error banner
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('edge case: success banner is NOT present before a successful submission', async () => {
    render(<ContactUsPage />)
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })
})

// ---------------------------------------------------------------------------
// AC-4 — HTTP 400 (kind:'validation') maps server errors onto inline field errors
// ---------------------------------------------------------------------------
describe('AC-4: HTTP 400 validation response maps server errors onto inline field errors', () => {
  it('happy path: server email error is shown as an inline alert under the email field', async () => {
    ;(contactClientModule.submitContact as Mock).mockResolvedValue({
      kind: 'validation',
      errors: { email: ['Email is not a valid format.'] },
    })

    const user = userEvent.setup()
    render(<ContactUsPage />)

    await fillForm(user)
    await user.click(screen.getByRole('button', { name: /send message/i }))

    await waitFor(() => {
      const alerts = screen.getAllByRole('alert')
      expect(
        alerts.some(a => /email is not a valid format/i.test(a.textContent ?? '')),
      ).toBe(true)
    })

    // No page-level error banner
    expect(screen.queryByRole('alert', { name: /couldn't send/i })).not.toBeInTheDocument()
    // No success banner
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('happy path: multiple server-side field errors are all displayed', async () => {
    ;(contactClientModule.submitContact as Mock).mockResolvedValue({
      kind: 'validation',
      errors: {
        fullName: ['Full name is required.'],
        email: ['Email is not a valid format.'],
        message: ['Message must be 1000 characters or fewer.'],
      },
    })

    const user = userEvent.setup()
    render(<ContactUsPage />)

    await fillForm(user)
    await user.click(screen.getByRole('button', { name: /send message/i }))

    await waitFor(() => {
      const alertTexts = screen.getAllByRole('alert').map(a => a.textContent ?? '')
      expect(alertTexts.some(t => /full name is required/i.test(t))).toBe(true)
      expect(alertTexts.some(t => /email is not a valid format/i.test(t))).toBe(true)
      expect(alertTexts.some(t => /message must be 1000/i.test(t))).toBe(true)
    })
  })

  it('edge case: validation response with empty errors array for a field shows nothing for that field', async () => {
    ;(contactClientModule.submitContact as Mock).mockResolvedValue({
      kind: 'validation',
      errors: { phone: [] },   // empty array — no message to display
    })

    const user = userEvent.setup()
    render(<ContactUsPage />)

    await fillForm(user)
    await user.click(screen.getByRole('button', { name: /send message/i }))

    // Allow async update to settle; phone error should not appear
    await waitFor(() => {
      expect(screen.queryByText(/phone/i, { selector: '[role="alert"]' })).not.toBeInTheDocument()
    })
  })
})

// ---------------------------------------------------------------------------
// AC-5 — HTTP 500 / failure shows the generic error banner
// ---------------------------------------------------------------------------
describe('AC-5: HTTP 500 / kind:failure shows the generic error banner', () => {
  it('happy path: generic error banner appears with the correct text', async () => {
    ;(contactClientModule.submitContact as Mock).mockResolvedValue({ kind: 'failure' })

    const user = userEvent.setup()
    render(<ContactUsPage />)

    await fillForm(user)
    await user.click(screen.getByRole('button', { name: /send message/i }))

    await waitFor(() =>
      expect(screen.getByRole('alert')).toHaveTextContent(
        "We couldn't send your message. Please try again later.",
      ),
    )

    // No success banner
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('edge case: error banner is NOT present before any submission attempt', () => {
    render(<ContactUsPage />)
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })
})

// ---------------------------------------------------------------------------
// AC-6 — Network error shows the generic error banner
// ---------------------------------------------------------------------------
describe('AC-6: network error (rejected promise) shows the generic error banner', () => {
  it('happy path: banner appears even when submitContact rejects', async () => {
    ;(contactClientModule.submitContact as Mock).mockRejectedValue(
      new Error('Network request failed'),
    )

    const user = userEvent.setup()
    render(<ContactUsPage />)

    await fillForm(user)
    await user.click(screen.getByRole('button', { name: /send message/i }))

    await waitFor(() =>
      expect(screen.getByRole('alert')).toHaveTextContent(
        "We couldn't send your message. Please try again later.",
      ),
    )
  })

  it('edge case: submit button re-enables after a network error', async () => {
    ;(contactClientModule.submitContact as Mock).mockRejectedValue(
      new Error('Network request failed'),
    )

    const user = userEvent.setup()
    render(<ContactUsPage />)

    await fillForm(user)

    const button = screen.getByRole('button', { name: /send message/i })
    await user.click(button)

    await waitFor(() => expect(screen.getByRole('alert')).toBeInTheDocument())

    // Button must be re-enabled so the user can retry
    expect(
      screen.getByRole('button', { name: /send message/i }),
    ).not.toBeDisabled()
  })
})

// ---------------------------------------------------------------------------
// Bonus: loading state during in-flight submission
// ---------------------------------------------------------------------------
describe('loading state: submit button is disabled while request is in flight', () => {
  it('button is disabled and shows loading text while awaiting the API', async () => {
    let resolveSubmit!: (v: { kind: 'failure' }) => void
    ;(contactClientModule.submitContact as Mock).mockImplementation(
      () =>
        new Promise<{ kind: 'failure' }>(resolve => {
          resolveSubmit = resolve
        }),
    )

    const user = userEvent.setup()
    render(<ContactUsPage />)

    await fillForm(user)
    await user.click(screen.getByRole('button', { name: /send message/i }))

    // While in flight the button should be disabled
    await waitFor(() =>
      expect(screen.getByRole('button', { name: /sending/i })).toBeDisabled(),
    )

    // Resolve the promise and let the UI settle
    resolveSubmit({ kind: 'failure' })

    await waitFor(() =>
      expect(
        screen.getByRole('button', { name: /send message/i }),
      ).not.toBeDisabled(),
    )
  })
})

// ---------------------------------------------------------------------------
// Structural check: ContactUsPage renders without any network request on mount
// ---------------------------------------------------------------------------
describe('ContactUsPage mount behaviour', () => {
  it('renders the page heading without calling the API client', () => {
    render(<ContactUsPage />)
    expect(screen.getByRole('heading', { name: /contact us/i })).toBeInTheDocument()
    expect(contactClientModule.submitContact).not.toHaveBeenCalled()
  })

  it('does not contain any login or sign-up reference', () => {
    render(<ContactUsPage />)
    const text = document.body.textContent ?? ''
    expect(text).not.toMatch(/\blog.?in\b/i)
    expect(text).not.toMatch(/\bsign.?up\b/i)
    expect(text).not.toMatch(/\bregister\b/i)
  })

  it('renders inside a <main> landmark', () => {
    render(<ContactUsPage />)
    const main = screen.getByRole('main')
    expect(within(main).getByLabelText(/full name/i)).toBeInTheDocument()
  })
})
