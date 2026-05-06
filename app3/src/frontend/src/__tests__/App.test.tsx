/**
 * STORY-008 smoke tests — Frontend project scaffold (Vite + React 18 + TypeScript strict)
 *
 * Acceptance criteria covered:
 *   AC4 — main.tsx mounts <App /> into #root  (verified by App rendering cleanly
 *          into a container element; the real entry-point is thin wiring only)
 *   AC5 — App renders ContactUsPage; no router-protected area, no login route
 *   REQ-003 — the page contains no login or sign-up links
 */

import { render, screen } from '@testing-library/react'
import App from '../App'

describe('App scaffold (STORY-008)', () => {

  // ── AC5 / happy path ────────────────────────────────────────────────────────
  it('renders ContactUsPage without crashing', () => {
    render(<App />)

    // ContactUsPage scaffold renders an <h1>Contact Us</h1> heading.
    expect(screen.getByRole('heading', { name: /contact us/i })).toBeInTheDocument()
  })

  // ── AC4 / happy path ────────────────────────────────────────────────────────
  // main.tsx does: createRoot(document.getElementById('root')!).render(<StrictMode><App /></StrictMode>)
  // We verify the same wiring by rendering App into a #root-like container — if it
  // mounts without throwing the entry-point contract is satisfied.
  it('mounts into a #root container without throwing (main.tsx wiring)', () => {
    const container = document.createElement('div')
    container.id = 'root'
    document.body.appendChild(container)

    expect(() => {
      render(<App />, { container })
    }).not.toThrow()

    document.body.removeChild(container)
  })

  // ── AC4 / edge-case: container without id ───────────────────────────────────
  it('also mounts correctly when rendered outside of a #root container', () => {
    // App must not hard-code any assumption about the container id.
    expect(() => {
      render(<App />)
    }).not.toThrow()
  })

  // ── REQ-003 / AC5 — no login or sign-up links — happy path ──────────────────
  it('contains no login or sign-up links', () => {
    render(<App />)

    expect(screen.queryByRole('link', { name: /log.?in|sign.?in/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /sign.?up|register|create.?account/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /log.?in|sign.?in/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /sign.?up|register|create.?account/i })).not.toBeInTheDocument()
  })

  // ── REQ-003 / AC5 — negative: no authentication-related text anywhere ────────
  it('does not render any authentication-related text', () => {
    render(<App />)

    const bodyText = document.body.textContent ?? ''
    expect(bodyText).not.toMatch(/\blog.?in\b/i)
    expect(bodyText).not.toMatch(/\bsign.?up\b/i)
    expect(bodyText).not.toMatch(/\bregister\b/i)
    expect(bodyText).not.toMatch(/\bcreate\s+account\b/i)
  })

  // ── AC5 — edge-case: single route, single page landmark ─────────────────────
  it('renders exactly one <main> landmark — no extra pages or route outlets', () => {
    render(<App />)

    const mains = screen.getAllByRole('main')
    expect(mains).toHaveLength(1)
  })

})
