# Requirement Specification

| Field       | Value                    |
|-------------|--------------------------|
| Run ID      | run-2026-05-03-001       |
| Version     | 1.0                      |
| Status      | draft                    |
| Author      | BA Agent                 |
| Date        | 2026-05-03               |

---

## Overview

The system consists of a frontend application and a backend service. The frontend provides a user-facing "Contact Us" page where visitors can submit contact enquiries. The backend receives and persists those submissions. No user authentication or authorisation is required for any part of the system.

> Note: The raw input is brief and high-level — some acceptance criteria may need refinement once stakeholders clarify business rules (e.g., notification behaviour, field validation rules, data retention).

---

## Requirements

### REQ-001 — Contact Us Page (Frontend)

**Description**
The frontend application must include a dedicated "Contact Us" page that is publicly accessible without any login or authentication. The page must present a form allowing any visitor to submit a contact enquiry.

**Acceptance Criteria**
1. Given a visitor navigates to the Contact Us page, the page loads and displays a contact form without requiring any login or credentials.
2. The contact form contains, at minimum, the following fields: full name, email address, and message body.
3. Each required field is visually indicated as mandatory, and the form prevents submission when any required field is empty.
4. After successful submission the visitor receives an on-screen confirmation that their enquiry has been received.
5. If the submission fails (e.g., the backend is unreachable), the page displays a user-friendly error message and does not lose the visitor's entered data.

---

### REQ-002 — Contact Form Validation (Frontend)

**Description**
The frontend must validate the contact form inputs on the client side before sending data to the backend. Validation must provide clear, field-level feedback to the visitor.

**Acceptance Criteria**
1. Given a visitor submits the form with an improperly formatted email address, a field-level error message is displayed and the form is not submitted.
2. Given a visitor submits the form with the message body left blank, a field-level error message is displayed and the form is not submitted.
3. All validation error messages are visible and descriptive enough for the visitor to correct the issue without further guidance.

---

### REQ-003 — Contact Submission Backend Endpoint

**Description**
The backend service must expose an endpoint that accepts contact form submissions from the frontend. No authentication or authorisation mechanism is required to call this endpoint.

**Acceptance Criteria**
1. Given the frontend posts a valid contact submission payload, the backend returns a success response and records the submission.
2. Given the frontend posts an incomplete or malformed payload (e.g., missing required fields), the backend returns a descriptive error response and does not record the submission.
3. The endpoint is accessible without any token, session, or credential — anonymous requests are accepted.

---

### REQ-004 — Storage of Contact Submissions (Backend)

**Description**
The backend must persist each valid contact form submission so that it can be reviewed at a later time by the appropriate team.

**Acceptance Criteria**
1. Given a valid submission is received by the backend, all submitted fields (name, email, message) along with a server-side timestamp are stored durably.
2. Stored submissions are not deleted or overwritten by subsequent submissions — each submission is retained as a distinct record.

---

### REQ-005 — No Authentication or Authorisation

**Description**
The system must not enforce any authentication or authorisation for any user-facing feature. Visitors must be able to access the Contact Us page and submit the form without creating an account, logging in, or providing any credentials.

**Acceptance Criteria**
1. Given a visitor accesses the application for the first time, they can reach the Contact Us page and submit the form without being prompted to register or log in.
2. No protected routes, authentication tokens, or session management mechanisms are required or enforced for the contact form workflow.

---

## Traceability

| Paragraph (raw-input.md)                                         | REQ(s) Covered                    |
|------------------------------------------------------------------|-----------------------------------|
| "Create a react app and .net backend with contact us page."      | REQ-001, REQ-002, REQ-003, REQ-004 |
| "no authorizations needs."                                       | REQ-005                           |
