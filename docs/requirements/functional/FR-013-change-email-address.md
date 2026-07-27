---
id: FR-013
title: Change the account email address
type: functional
status: Draft
priority: Should
source: UC-MAIN-005
verification: test
related: [UC-MAIN-005, NFR-003, NFR-005, ADR-0009]
---

# FR-013 — Change the account email address

## Description

A signed-in [User](../../glossary/user.md) must be able to change the email address on their account
through a **verify-before-swap** flow:

1. the user enters the new address;
2. a verification link is sent to the **new** address;
3. the **old** address is notified that a change was requested;
4. only when the new address is verified does it replace the old one.

Until verification completes, the **old address remains the account's address** and continues to work
for sign-in and password reset. The response must not reveal whether the new address already belongs
to another account ([NFR-005](../non-functional/NFR-005-account-enumeration-resistance.md)), and
requests are rate-limited.

## Rationale

The email address is the account's recovery channel, so changing it is a security-relevant operation
rather than an ordinary profile field edit — that is why it is its own requirement instead of part of
[FR-012](FR-012-manage-profile.md). Verify-before-swap prevents a typo or a hijacked session from
silently moving the recovery channel out of the owner's reach, and notifying the old address gives the
real owner a chance to react while they still control the account.

## Acceptance criteria

```gherkin
Given a signed-in user
When they request a change to a new email address
Then a verification link is sent to the new address
And the old address receives a notification that a change was requested
And the account's address is still the old one

Given a pending email change
When the user opens the verification link sent to the new address
Then the new address replaces the old one
And it is marked verified

Given a pending email change that has not been verified
When the user signs in or requests a password reset
Then the old address still works for both

Given a new address that already belongs to another account
When the change is requested
Then the response is identical in content, status code and timing to the case where it does not
And no account data is disclosed

Given a pending email change
When the verification link expires
Then the account's address is unchanged
And the user may request the change again

Given repeated email-change requests
When further requests are made
Then they are rate-limited
```

## Dependencies & related

- Derived from: [UC-MAIN-005](../../use-cases/main/UC-MAIN-005-manage-profile.md), extension 3b
- Related: [FR-012](FR-012-manage-profile.md), [FR-007](FR-007-create-suite-account.md),
  [FR-009](FR-009-reset-password.md),
  [NFR-003](../non-functional/NFR-003-multilingual-de-en-fr.md),
  [NFR-005](../non-functional/NFR-005-account-enumeration-resistance.md)
- Governing ADR: [ADR-0009](../../adr/0009-authentication-and-passkeys.md)
- **External dependency:** a transactional email provider — an open
  [deployment ADR](../../adr/index.md#open-decisions-backlog).
- Glossary: [User](../../glossary/user.md)
