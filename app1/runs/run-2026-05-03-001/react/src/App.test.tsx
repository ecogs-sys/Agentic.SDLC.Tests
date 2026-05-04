import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { vi } from 'vitest'
import * as contactApi from './api/contactApi'

// Mock the API so that ContactPage/ContactForm never makes real network calls.
vi.mock('./api/contactApi')

// ---------------------------------------------------------------------------
// App inner routes — imported after mocks are in place.
//
// App.tsx wraps its Routes in BrowserRouter, which would conflict with the
// MemoryRouter we use here to control the initial URL. To avoid nested-router
// issues we mock BrowserRouter to be a transparent wrapper, so MemoryRouter
// (provided by the test) is the sole router in the tree.
// ---------------------------------------------------------------------------
vi.mock('react-router-dom', async (importOriginal) => {
  const actual = await importOriginal<typeof import('react-router-dom')>()
  return {
    ...actual,
    // Replace BrowserRouter with a passthrough so MemoryRouter controls routing.
    BrowserRouter: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  }
})

// Dynamic import AFTER vi.mock so the mock is active when the module loads.
const { default: App } = await import('./App')

// ---------------------------------------------------------------------------
// AC1 — navigating directly to /contact renders ContactPage; no redirect/guard
// ---------------------------------------------------------------------------
describe('AC1: /contact route renders ContactPage', () => {
  it('happy path — ContactPage heading and form are present when starting at /contact', () => {
    render(
      <MemoryRouter initialEntries={['/contact']}>
        <App />
      </MemoryRouter>,
    )

    expect(screen.getByRole('heading', { name: /contact us/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /send message/i })).toBeInTheDocument()
  })

  it('edge case — the home-page heading is NOT rendered when starting at /contact', () => {
    render(
      <MemoryRouter initialEntries={['/contact']}>
        <App />
      </MemoryRouter>,
    )

    expect(screen.queryByRole('heading', { name: /welcome/i })).not.toBeInTheDocument()
  })
})

// ---------------------------------------------------------------------------
// AC2 — the landing page at / includes a visible link whose href is /contact
// ---------------------------------------------------------------------------
describe('AC2: home page has a discoverable link to /contact', () => {
  it('happy path — a link pointing to /contact is present on the home page', () => {
    render(
      <MemoryRouter initialEntries={['/']}>
        <App />
      </MemoryRouter>,
    )

    const link = screen.getByRole('link', { name: /contact us/i })
    expect(link).toBeInTheDocument()
    expect(link).toHaveAttribute('href', '/contact')
  })

  it('edge case — the /contact route content is NOT shown on the home page', () => {
    render(
      <MemoryRouter initialEntries={['/']}>
        <App />
      </MemoryRouter>,
    )

    expect(screen.queryByRole('heading', { name: /contact us/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /send message/i })).not.toBeInTheDocument()
  })
})

// ---------------------------------------------------------------------------
// AC5 (routing layer) — /contact route renders <ContactForm> with no auth guard
// ---------------------------------------------------------------------------
describe('AC5 (routing): /contact route mounts ContactForm fields directly', () => {
  it('happy path — all three form fields are present at /contact via full App routing', () => {
    render(
      <MemoryRouter initialEntries={['/contact']}>
        <App />
      </MemoryRouter>,
    )

    expect(screen.getByLabelText(/full name/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/email address/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/message/i)).toBeInTheDocument()
  })

  it('edge case — submitContact is never called during a plain render of /contact', () => {
    render(
      <MemoryRouter initialEntries={['/contact']}>
        <App />
      </MemoryRouter>,
    )

    expect(contactApi.submitContact).not.toHaveBeenCalled()
  })
})
