# Requirement Specification

| Field       | Value                        |
|-------------|------------------------------|
| Run ID      | run-2026-05-04-001           |
| Version     | 1.0                          |
| Status      | draft                        |
| Date        | 2026-05-04                   |
| Author      | BA Agent (claude-sonnet-4-6) |

---

## 1. Overview

The system consists of a web frontend and a backend service. The frontend provides a publicly accessible "Contact Us" page through which visitors can submit enquiries. The backend receives and persists those submissions. No user authentication or authorisation is required anywhere in the system.

---

## 2. Scope

- A single-page web application serving at minimum a "Contact Us" page.
- A backend service that accepts, validates, and stores contact form submissions.
- No login, session management, or role-based access control of any kind.

---

## 3. Requirements

### REQ-001 — Web Frontend Application

**Description:**
A web frontend application must be delivered that is accessible to any visitor without requiring login or account creation. It must be navigable via a standard web browser and serve at minimum the Contact Us page described in REQ-003.

**Acceptance Criteria:**
- AC-001-1: A visitor can open the application in a web browser without being prompted to log in or create an account.
- AC-001-2: The application loads and renders without errors on the latest stable versions of Chrome, Firefox, and Edge.
- AC-001-3: The application contains at minimum a Contact Us page reachable from the default/root route or a clearly visible navigation element.

---

### REQ-002 — Backend Service

**Description:**
A backend service must be delivered that exposes endpoints consumed by the frontend. The service must operate without enforcing any authentication or authorisation on the endpoints used by the Contact Us feature. It must accept incoming contact submissions, validate required fields, and persist or forward the data.

**Acceptance Criteria:**
- AC-002-1: The backend service starts and responds to health or readiness checks without errors.
- AC-002-2: Requests to the contact submission endpoint are accepted without any authentication token, API key, or session cookie being required.
- AC-002-3: The backend rejects a submission that is missing any required field (see REQ-003) and returns a descriptive error response indicating which field is invalid.
- AC-002-4: A valid submission received by the backend is persisted or forwarded such that it can be retrieved or reviewed by an operator.

---

### REQ-003 — Contact Us Page and Submission Flow

**Description:**
The frontend must include a "Contact Us" page containing a form that allows a visitor to submit an enquiry. The form must collect at minimum the visitor's name, email address, and message. On submission the data must be sent to the backend service. The visitor must receive clear feedback indicating whether the submission succeeded or failed.

**Acceptance Criteria:**
- AC-003-1: The Contact Us page displays a form containing fields for at minimum: full name, email address, and message body.
- AC-003-2: Submitting the form with all required fields populated sends the data to the backend service and displays a success confirmation to the visitor.
- AC-003-3: Attempting to submit the form with one or more required fields empty prevents submission and displays inline validation feedback identifying the missing or invalid fields.
- AC-003-4: If the backend returns an error, the visitor sees a user-friendly error message and the form data they entered is preserved so they can retry without re-entering information.
- AC-003-5: The email field validates that the entered value conforms to a standard email address format before the form is submitted.

---

### REQ-004 — No Authentication or Authorisation

**Description:**
The system must not implement any form of user authentication, authorisation, or access control. All frontend pages and all backend endpoints supporting the Contact Us feature must be publicly accessible.

> Note: input is ambiguous regarding whether any future admin-facing views (e.g., viewing stored submissions) are in scope. This requirement captures the explicit statement that no authorisation is needed; if an admin view is added later, authorisation requirements should be re-evaluated at that time.

**Acceptance Criteria:**
- AC-004-1: No login screen, sign-up screen, or authentication prompt exists anywhere in the application for the features described in this specification.
- AC-004-2: The backend does not return HTTP 401 or HTTP 403 responses to any request made as part of the Contact Us submission flow.

---

## 4. Out of Scope

- User account management, login, or session handling.
- Admin or operator interface for viewing stored submissions (not mentioned in raw input).
- Email notification delivery to the visitor or the site owner (not mentioned in raw input).
- Automated testing infrastructure.

---

## 5. Open Questions

| # | Question | Impact |
|---|----------|--------|
| 1 | What fields beyond name, email, and message (if any) should the Contact Us form collect? | REQ-003 AC-003-1 |
| 2 | Where should submissions be persisted — a database, a file, forwarded by email, or a third-party service? | REQ-002 AC-002-4 |
| 3 | Is a confirmation email to the visitor or the site owner required? | New REQ may be needed |
| 4 | Are there any specific browser or device (mobile) support targets beyond desktop browsers? | REQ-001 AC-001-2 |
