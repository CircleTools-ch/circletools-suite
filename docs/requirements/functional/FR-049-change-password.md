---
id: FR-049
title: Change password while signed in
type: functional
status: Approved
priority: Must
source: 2026-10-15 sync device test (D10), UC-MAIN-002
verification: test
related: [UC-MAIN-002, UC-MAIN-007, FR-008, FR-009, FR-013, NFR-005, ADR-0079, ADR-0021, FEAT-023]
---

# FR-049 — Change password while signed in

## Description

A signed-in [User](../../glossary/user.md) must be able to change their password from **Security**,
without going through the email reset flow:

- they enter their **current password** and a **new password** (entered twice);
- the new password follows the same rules as at sign-up: the password policy and the breached-password
  check;
- on success, **every other session** of the account ends, and this device stays signed in
  ([ADR-0079](../../adr/0079-sign-out-this-device-and-change-password.md));
- a **notice email** goes to the account's address ("your password was changed"). It contains no link and
  is written in the account's language;
- a wrong current password gets the same answer as a failed sign-in and is rate-limited the same way.

The Security page also offers **"Sign out on all devices"**, which ends every session including this one.

## Rationale

The only way to change a password was "forgot password". People expect a change-password form in their
account settings. The email-change notice already advises "change your password immediately" if the change
wasn't theirs, so that advice must be possible to follow. Ending the other sessions is what makes a
password change useful when someone else knows the old password.

## Acceptance criteria

```gherkin
Given a signed-in user on the Security page
When they enter their current password and a valid new password twice and confirm
Then the new password is required at the next sign-in
And the old password no longer works
And this device stays signed in
And every other session of the account is ended
And a notice email without a link is sent to the account's address in the account's language

Given a wrong current password
When the user tries to change the password
Then the change is refused with the same message as a failed sign-in
And repeated attempts are rate-limited

Given a new password that fails the password policy or appears in a known breach
When the user tries to change the password
Then the change is refused and the reason is shown

Given two different entries for the new password
When the user tries to confirm
Then the form says they don't match and nothing is sent

Given a signed-in user
When they choose "Sign out on all devices" and confirm
Then every session of the account ends, including this one
```

## Dependencies & related

- Derived from: [UC-MAIN-002](../../use-cases/main/UC-MAIN-002-sign-in-with-password.md) (password
  credential), [2026-10-15 sync device test](../../brainstorming/2026-10-15-secret-santa-sync-device-test.md)
- Related: [FR-008](FR-008-sign-in-with-password.md), [FR-009](FR-009-reset-password.md),
  [FR-013](FR-013-change-email-address.md),
  [NFR-005](../non-functional/NFR-005-account-enumeration-resistance.md)
- Governing ADRs: [ADR-0079](../../adr/0079-sign-out-this-device-and-change-password.md),
  [ADR-0021](../../adr/0021-web-auth-token-model.md)
- Implemented by: [FEAT-023](../../features/FEAT-023-session-scope-and-change-password.md)
- Glossary: [User](../../glossary/user.md)
