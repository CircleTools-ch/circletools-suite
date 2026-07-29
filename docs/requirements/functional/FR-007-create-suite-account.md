---
id: FR-007
title: Create a suite account with email verification
type: functional
status: Implemented
priority: Must
source: UC-MAIN-001
verification: test
related: [UC-MAIN-001, NFR-001, NFR-003, NFR-004, NFR-005, ADR-0009]
---

# FR-007 — Create a suite account with email verification

## Description

The Main shell must let a visitor create one [User](../../glossary/user.md) account for the whole
suite from a display name, an email address and a password. The account is created in a
**pending-verification** state; a verification link is sent to the address, and opening it marks the
email verified.

- **Password policy:** minimum **12 characters**, no composition rules, no forced rotation, checked
  against a breached-password list.
- **Pending accounts may sign in**, but nothing account-scoped works until the email is verified — no
  [Sync](../../glossary/sync.md), no claiming, no [Circle](../../glossary/circle.md) join or invite.
- **Pending accounts are purged after 30 days.**
- The response to sign-up must be **identical whether or not the address already has an account**
  ([NFR-005](../non-functional/NFR-005-account-enumeration-resistance.md)); an already-registered
  address instead receives a *"someone tried to sign up with your address"* mail.

Creating an account must not alter or remove any local, account-free data
([NFR-001](../non-functional/NFR-001-local-first-offline.md)).

## Rationale

An account is the entry point to every v2 capability — circles, sync, seeing your own assignment on
your own device — while staying strictly optional. Verification exists because an unproven address
must never be able to touch other people: it is the precondition for invitations and for anything
shared. The password policy follows NIST SP 800-63B, where length beats character-class rules that
push people toward predictable substitutions.

## Acceptance criteria

```gherkin
Given a visitor with no account and a working connection
When they submit a display name, a valid email and a password of at least 12 characters
Then a user is created in pending-verification state
And a verification email is sent in the visitor's current language

Given a pending-verification account
When the user opens the verification link
Then the email is marked verified
And the user is signed in

Given a pending-verification account
When the user signs in
Then sign-in succeeds
And sync, group claiming and circle join/invite remain unavailable
And the shell shows a prompt to verify the address

Given an email address that already has an account
When someone submits the sign-up form with that address
Then the response is identical in content, status code and timing to a successful sign-up
And no account is created
And the address receives a "someone tried to sign up" notification instead

Given a submitted password shorter than 12 characters or present on the breached-password list
When the visitor attempts to submit
Then submission is blocked and the rule is explained

Given a pending-verification account that is 30 days old
When the retention job runs
Then the account and its personal data are purged

Given the backend is unreachable
When a visitor attempts to create an account
Then the failure is reported
And all local, account-free functionality continues to work unchanged
```

## Dependencies & related

- Derived from: [UC-MAIN-001](../../use-cases/main/UC-MAIN-001-create-account.md)
- Related: [FR-008](FR-008-sign-in-with-password.md), [FR-009](FR-009-reset-password.md),
  [NFR-001](../non-functional/NFR-001-local-first-offline.md),
  [NFR-003](../non-functional/NFR-003-multilingual-de-en-fr.md),
  [NFR-004](../non-functional/NFR-004-data-protection-and-retention.md),
  [NFR-005](../non-functional/NFR-005-account-enumeration-resistance.md)
- Governing ADR: [ADR-0009](../../adr/0009-authentication-and-passkeys.md)
- **External dependency:** a transactional email provider — an open
  [deployment ADR](../../adr/index.md#open-decisions-backlog). This requirement cannot be completed
  without it.
- Glossary: [User](../../glossary/user.md), [Sync](../../glossary/sync.md)
