---
id: FR-009
title: Reset a forgotten password
type: functional
status: Implemented
priority: Must
source: UC-MAIN-007
verification: test
related: [UC-MAIN-007, NFR-003, NFR-005, ADR-0009, ADR-0021]
---

# FR-009 — Reset a forgotten password

## Description

The Main shell must let a [User](../../glossary/user.md) who cannot sign in request a password reset
by email. The reset link is **single-use and short-lived**. Completing a reset:

- replaces the password credential;
- **revokes every existing session and refresh token**;
- **verifies the email address** if it was still pending (the mail proves address control);
- **keeps existing [Passkeys](../../glossary/passkey.md)** — forgetting a password is not evidence of
  a lost device.

Requesting a reset does **not** lock or invalidate the current password. The response must be
identical whether or not the address has an account, in content, status code **and timing**
([NFR-005](../non-functional/NFR-005-account-enumeration-resistance.md)), and requests are
rate-limited per address and per source.

## Rationale

Password reset is simultaneously the most common account-recovery path and the classic
account-enumeration oracle and takeover vector. Revoking sessions on reset is what makes it a genuine
recovery mechanism: a user who resets because they suspect compromise must actually eject the
intruder. Rate-limiting protects the address from being mail-bombed as well as protecting the oracle.

## Acceptance criteria

```gherkin
Given an account exists for an email address
When a reset is requested for that address
Then a single-use, short-lived reset link is sent to it
And the response states only that a link has been sent if the address has an account

Given no account exists for an email address
When a reset is requested for that address
Then no email is sent
And the response is identical in content, status code and timing to the case where one exists

Given a valid reset link
When the user sets a new password meeting the password policy
Then the password is replaced
And all existing sessions and refresh tokens for that user are revoked
And the user is signed in on the current device

Given a reset link that has already been used or has expired
When it is opened
Then it is rejected with a clear explanation and the offer of a new link

Given a pending-verification account
When its owner completes a password reset
Then the email address is also marked verified

Given a user with registered passkeys
When they complete a password reset
Then their passkeys remain registered and usable

Given a user requests a reset but then remembers their password
When they sign in with the existing password
Then sign-in succeeds and the outstanding reset link simply expires

Given repeated reset requests for one address
When further requests are made
Then they are rate-limited
```

## Dependencies & related

- Derived from: [UC-MAIN-007](../../use-cases/main/UC-MAIN-007-reset-password.md)
- Related: [FR-007](FR-007-create-suite-account.md), [FR-008](FR-008-sign-in-with-password.md),
  [FR-010](FR-010-manage-passkeys.md),
  [NFR-003](../non-functional/NFR-003-multilingual-de-en-fr.md),
  [NFR-005](../non-functional/NFR-005-account-enumeration-resistance.md)
- Governing ADRs: [ADR-0009](../../adr/0009-authentication-and-passkeys.md),
  [ADR-0021](../../adr/0021-web-auth-token-model.md) (session revocation)
- **External dependency:** a transactional email provider — an open
  [deployment ADR](../../adr/index.md#open-decisions-backlog).
- Glossary: [User](../../glossary/user.md), [Passkey](../../glossary/passkey.md)
