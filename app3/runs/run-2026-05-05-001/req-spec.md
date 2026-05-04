# Requirement Specification

| Field       | Value                  |
|-------------|------------------------|
| Run ID      | run-2026-05-05-001     |
| Version     | 1.1                    |
| Status      | draft                  |
| Author      | BA Agent               |
| Date        | 2026-05-05             |

---

## 1. Overview

The system shall consist of a frontend web application and a backend web service. The primary user-facing feature is a "Contact Us" page that allows any visitor to submit a contact enquiry without requiring authentication. The application requires no user login or access-control mechanisms of any kind.

---

## 2. Scope

- A publicly accessible web front end.
- A backend service that receives and persists contact form submissions.
- No authentication, authorisation, or user account management.

---

## 3. Requirements

### REQ-001 — Contact Us Page (Frontend)

**Description:**
The web application shall provide a dedicated "Contact Us" page that is accessible to all visitors without any login or authentication step. The page shall present a form for users to submit their contact details and a message.

**Notes:** Required form fields are confirmed by the product owner as: full name, email address, phone number, subject, and message. The message field has a maximum length of 1000 characters.

**Acceptance Criteria:**
1. Given a visitor navigates to the Contact Us page, the page renders a form containing the following required fields: full-name, email address, phone number, subject, and message/body.
2. Given a visitor who has not authenticated, the Contact Us page is still fully accessible and the form is fully usable — no login prompt or redirect occurs.
3. Given a visitor submits the form with all required fields populated correctly, a success confirmation message is displayed to the visitor on screen.
4. Given a visitor attempts to submit the form with one or more required fields left empty, inline validation messages identify each missing field before the form is submitted to the backend.
5. Given a visitor provides an email address in an invalid format, an inline validation message informs the visitor before the form is submitted to the backend.
6. Given a visitor enters a message that exceeds 1000 characters, the form prevents submission and displays an inline validation message indicating the character limit has been exceeded.
7. Given a visitor is composing a message, a character counter or equivalent feedback indicates the remaining characters allowed up to the 1000-character limit.

---

### REQ-002 — Contact Form Submission (Backend)

**Description:**
The backend service shall expose an endpoint that accepts contact form submissions from the frontend, validates the submitted data, and persists each valid submission for future retrieval.

**Notes:** Notification behaviour (e.g., sending an email to an administrator on new submission) is confirmed out of scope. Admin viewing of stored submissions is confirmed out of scope. Required fields are confirmed as: full name, email address, phone number, subject, and message (maximum 1000 characters).

**Acceptance Criteria:**
1. Given a well-formed submission containing a full name, a valid email address, a phone number, a subject, and a non-empty message of 1000 characters or fewer, the backend accepts the request, persists the data, and returns a success response.
2. Given a submission that is missing any required field (name, email, phone, subject, or message), the backend returns a validation-error response identifying which fields are invalid, and the submission is not persisted.
3. Given a submission containing an email address that does not conform to a standard email format, the backend returns a validation-error response and the submission is not persisted.
4. Given a submission in which the message field exceeds 1000 characters, the backend returns a validation-error response and the submission is not persisted.
5. Given two or more contact submissions are received in sequence, each is stored independently and no previously stored submission is overwritten or lost.

---

### REQ-003 — No Authentication or Authorisation

**Description:**
The system shall not implement any form of authentication (e.g., login, session tokens, identity verification) or authorisation (e.g., role-based access, permission checks) anywhere in the application. All pages and all backend endpoints relevant to the Contact Us feature shall be open to any user without credentials.

**Acceptance Criteria:**
1. Given any visitor accesses the web application, no login page, sign-up page, or authentication challenge is presented at any point in the user journey for the Contact Us feature.
2. Given a request is sent directly to the backend contact-submission endpoint without any authentication header or token, the backend processes the request normally and does not return an authentication or authorisation error.

---

### REQ-004 — Frontend and Backend Integration

**Description:**
The frontend web application and the backend service shall be able to communicate with each other so that contact form submissions entered in the browser are transmitted to and processed by the backend.

**Acceptance Criteria:**
1. Given a visitor completes and submits the contact form in the browser, the form data is transmitted to the backend service and the backend's response determines the feedback shown to the visitor (success or error).
2. Given the backend service is unavailable when the visitor submits the form, the frontend displays an appropriate error message informing the visitor that the submission could not be completed, rather than showing an unhandled error or blank screen.

---

## 4. Out of Scope

- User authentication and authorisation of any kind.
- Administration interface for viewing stored contact submissions — confirmed out of scope by product owner.
- Email or other notification delivery triggered by a new submission — confirmed out of scope by product owner.
- Any features beyond the Contact Us page (no other pages are specified).

---

## 5. Open Questions

All open questions from version 1.0 have been resolved by the product owner. No open questions remain.

| # | Question | Resolution |
|---|----------|------------|
| 1 | Which specific fields are required on the contact form? | Resolved — required fields are: full name, email, phone, subject, message. |
| 2 | Should submitted contact enquiries be viewable anywhere (e.g., an admin panel)? | Resolved — no admin panel is needed. Out of scope. |
| 3 | Should a notification be sent to the site owner when a new submission arrives? | Resolved — no notification email is needed. Out of scope. |
| 4 | Are there any content or length constraints on the message field? | Resolved — message field has a maximum of 1000 characters. |
