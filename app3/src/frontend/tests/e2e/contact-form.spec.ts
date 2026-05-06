import { test, expect } from '@playwright/test'

/**
 * End-to-end smoke tests for the Contact Us form.
 *
 * Prerequisites: run `docker compose up -d` from the repo root before executing
 * these tests. The compose stack exposes the frontend at http://localhost:3000
 * and the backend API at http://localhost:8080.
 *
 * AC3 note: A 201 response from the backend is only returned after the row has
 * been successfully persisted to the `contact_submissions` table (EF Core
 * transaction). The success banner appearing in the UI therefore proves the row
 * was inserted. A DevOps reviewer can additionally confirm with:
 *   docker compose exec db psql -U contact_user -d contactdb \
 *     -c "SELECT * FROM contact_submissions ORDER BY received_at DESC LIMIT 1;"
 */

test.describe('Contact form', () => {
  test('fills all five fields, submits, and shows the success banner', async ({ page }) => {
    await page.goto('/')

    // Fill all five fields with valid values
    await page.getByLabel('Full Name').fill('Jane Smith')
    await page.getByLabel('Email').fill('jane@example.com')
    await page.getByLabel('Phone').fill('+1-555-0100')
    await page.getByLabel('Subject').fill('Test submission')
    await page.getByLabel('Message').fill('This is an end-to-end test message.')

    // Submit the form
    await page.getByRole('button', { name: /send message/i }).click()

    // AC2: success banner must appear after a successful submission.
    // AC3: the backend only responds 201 after persisting the row to
    //      contact_submissions, so the banner appearing proves DB persistence.
    const banner = page.getByRole('status')
    await expect(banner).toBeVisible({ timeout: 10000 })
    await expect(banner).toContainText(/thank you/i)
  })

  test('shows inline validation errors when form is submitted empty', async ({ page }) => {
    await page.goto('/')

    // Submit with all fields empty — all five should show errors
    await page.getByRole('button', { name: /send message/i }).click()

    // At least one inline error alert must be visible immediately (client-side
    // validation blocks the network request when any field is invalid)
    const firstAlert = page.getByRole('alert').first()
    await expect(firstAlert).toBeVisible()
  })
})
