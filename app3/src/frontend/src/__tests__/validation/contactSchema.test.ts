/**
 * Vitest unit tests for validateContactForm (STORY-009 / STORY-014)
 *
 * Acceptance criteria covered:
 *   STORY-014 AC3  — all-valid input passes with no errors
 *   STORY-014 AC4  — each missing/empty field yields an error keyed under that field
 *   STORY-014 AC5  — message of 1000 chars is valid; 1001 chars is invalid
 *   STORY-014 AC6  — malformed emails rejected; well-formed email accepted
 *
 * Additional edge-cases:
 *   - whitespace-only fields treated as empty (trimming behaviour)
 *   - max-length boundaries for fullName (200), phone (50), subject (200)
 */

import { describe, it, expect } from 'vitest'
import {
  validateContactForm,
  type ContactFormInput,
} from '../../validation/contactSchema'

// ── helpers ────────────────────────────────────────────────────────────────────

/** A fully-valid baseline payload so individual tests can override one field. */
const valid: ContactFormInput = {
  fullName: 'Jane Doe',
  email: 'jane@example.com',
  phone: '+1-800-555-0100',
  subject: 'Hello there',
  message: 'This is a perfectly valid message.',
}

/** Returns a copy of `valid` with the specified field overridden. */
function with_(field: keyof ContactFormInput, value: string): ContactFormInput {
  return { ...valid, [field]: value }
}

// ── STORY-014 AC3 — all-valid input passes ─────────────────────────────────────

describe('validateContactForm — all-valid input', () => {
  it('returns an empty errors object when every field is valid (happy path)', () => {
    const errors = validateContactForm(valid)
    expect(errors).toEqual({})
  })

  it('returns no errors when fields are at their maximum allowed lengths', () => {
    const errors = validateContactForm({
      fullName: 'A'.repeat(200),
      email: 'a@example.com',
      phone: '1'.repeat(50),
      subject: 'S'.repeat(200),
      message: 'M'.repeat(1000),
    })
    expect(errors).toEqual({})
  })
})

// ── STORY-014 AC4 — each missing/empty field yields an error ──────────────────

describe('validateContactForm — required field errors', () => {
  const requiredFields: (keyof ContactFormInput)[] = [
    'fullName',
    'email',
    'phone',
    'subject',
    'message',
  ]

  requiredFields.forEach((field) => {
    it(`returns an error keyed under "${field}" when it is an empty string (happy path for error case)`, () => {
      const errors = validateContactForm(with_(field, ''))
      expect(errors).toHaveProperty(field)
      expect(typeof errors[field]).toBe('string')
      expect((errors[field] as string).length).toBeGreaterThan(0)
    })

    it(`does NOT return an error for "${field}" when it has valid content (negative: no false positive)`, () => {
      const errors = validateContactForm(valid)
      expect(errors).not.toHaveProperty(field)
    })
  })

  it('accumulates errors for ALL empty fields simultaneously', () => {
    const errors = validateContactForm({
      fullName: '',
      email: '',
      phone: '',
      subject: '',
      message: '',
    })
    expect(errors).toHaveProperty('fullName')
    expect(errors).toHaveProperty('email')
    expect(errors).toHaveProperty('phone')
    expect(errors).toHaveProperty('subject')
    expect(errors).toHaveProperty('message')
  })

  it('returns only the error for the single empty field, not others', () => {
    const errors = validateContactForm(with_('subject', ''))
    expect(errors).toHaveProperty('subject')
    expect(errors).not.toHaveProperty('fullName')
    expect(errors).not.toHaveProperty('email')
    expect(errors).not.toHaveProperty('phone')
    expect(errors).not.toHaveProperty('message')
  })
})

// ── Trimming behaviour — whitespace-only treated as empty ─────────────────────

describe('validateContactForm — whitespace-only fields treated as empty', () => {
  const fields: (keyof ContactFormInput)[] = [
    'fullName',
    'phone',
    'subject',
    'message',
  ]

  fields.forEach((field) => {
    it(`"${field}" with only spaces is treated as empty and returns an error`, () => {
      const errors = validateContactForm(with_(field, '   '))
      expect(errors).toHaveProperty(field)
    })
  })

  it('email with only spaces is treated as empty and returns an error', () => {
    const errors = validateContactForm(with_('email', '   '))
    expect(errors).toHaveProperty('email')
  })

  it('leading/trailing whitespace does not cause a max-length false positive for a valid value', () => {
    // 198 real chars padded with one leading and one trailing space = 200 raw chars.
    // After trim it is 198 chars, which is within the 200-char limit.
    const errors = validateContactForm(with_('fullName', ' ' + 'A'.repeat(198) + ' '))
    expect(errors).not.toHaveProperty('fullName')
  })
})

// ── STORY-014 AC5 — message length boundary (1000 / 1001) ────────────────────

describe('validateContactForm — message length boundary', () => {
  it('accepts a message of exactly 1000 characters (happy path)', () => {
    const errors = validateContactForm(with_('message', 'M'.repeat(1000)))
    expect(errors).not.toHaveProperty('message')
  })

  it('rejects a message of 1001 characters (over limit)', () => {
    const errors = validateContactForm(with_('message', 'M'.repeat(1001)))
    expect(errors).toHaveProperty('message')
    expect(errors.message).toMatch(/1000/i)
  })

  it('accepts a message of 999 characters (one below boundary)', () => {
    const errors = validateContactForm(with_('message', 'M'.repeat(999)))
    expect(errors).not.toHaveProperty('message')
  })
})

// ── STORY-014 AC6 — email format validation ───────────────────────────────────

describe('validateContactForm — email format', () => {
  const malformedEmails = ['foo', 'foo@', 'foo@bar']

  malformedEmails.forEach((email) => {
    it(`rejects malformed email "${email}"`, () => {
      const errors = validateContactForm(with_('email', email))
      expect(errors).toHaveProperty('email')
      expect(errors.email).toMatch(/not a valid format/i)
    })
  })

  it('accepts a well-formed email "jane@example.com" (happy path)', () => {
    const errors = validateContactForm(with_('email', 'jane@example.com'))
    expect(errors).not.toHaveProperty('email')
  })

  it('accepts an email with sub-domain "user@mail.example.co.uk"', () => {
    const errors = validateContactForm(with_('email', 'user@mail.example.co.uk'))
    expect(errors).not.toHaveProperty('email')
  })

  it('accepts an email with a plus sign "user+tag@example.com"', () => {
    const errors = validateContactForm(with_('email', 'user+tag@example.com'))
    expect(errors).not.toHaveProperty('email')
  })

  it('rejects an empty-string email (treated as missing)', () => {
    const errors = validateContactForm(with_('email', ''))
    expect(errors).toHaveProperty('email')
    // Should be a "required" message, not a format message
    expect(errors.email).toMatch(/required/i)
  })
})

// ── Max-length boundaries for fullName (200), phone (50), subject (200) ───────

describe('validateContactForm — max-length boundaries', () => {
  // fullName
  it('accepts fullName of exactly 200 characters', () => {
    const errors = validateContactForm(with_('fullName', 'A'.repeat(200)))
    expect(errors).not.toHaveProperty('fullName')
  })

  it('rejects fullName of 201 characters', () => {
    const errors = validateContactForm(with_('fullName', 'A'.repeat(201)))
    expect(errors).toHaveProperty('fullName')
    expect(errors.fullName).toMatch(/200/i)
  })

  it('accepts fullName of 199 characters (one below boundary)', () => {
    const errors = validateContactForm(with_('fullName', 'A'.repeat(199)))
    expect(errors).not.toHaveProperty('fullName')
  })

  // phone
  it('accepts phone of exactly 50 characters', () => {
    const errors = validateContactForm(with_('phone', '1'.repeat(50)))
    expect(errors).not.toHaveProperty('phone')
  })

  it('rejects phone of 51 characters', () => {
    const errors = validateContactForm(with_('phone', '1'.repeat(51)))
    expect(errors).toHaveProperty('phone')
    expect(errors.phone).toMatch(/50/i)
  })

  it('accepts phone of 49 characters (one below boundary)', () => {
    const errors = validateContactForm(with_('phone', '1'.repeat(49)))
    expect(errors).not.toHaveProperty('phone')
  })

  // subject
  it('accepts subject of exactly 200 characters', () => {
    const errors = validateContactForm(with_('subject', 'S'.repeat(200)))
    expect(errors).not.toHaveProperty('subject')
  })

  it('rejects subject of 201 characters', () => {
    const errors = validateContactForm(with_('subject', 'S'.repeat(201)))
    expect(errors).toHaveProperty('subject')
    expect(errors.subject).toMatch(/200/i)
  })

  it('accepts subject of 199 characters (one below boundary)', () => {
    const errors = validateContactForm(with_('subject', 'S'.repeat(199)))
    expect(errors).not.toHaveProperty('subject')
  })
})
