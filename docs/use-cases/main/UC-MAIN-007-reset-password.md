---
id: UC-MAIN-007
title: Reset a forgotten password
app: Main
actors: [User]
status: Approved
priority: Must
related: [NFR-003, ADR-0009, ADR-0018, ADR-0021]
---

# UC-MAIN-007 — Reset a forgotten password

## Summary / goal

A [User](../../glossary/user.md) who cannot remember their password regains access to their account
through a link sent to their verified email address — without the system ever revealing to the
requester whether that address has an account.

## Primary actor & stakeholders

- **Primary actor:** User who cannot sign in.
- **Stakeholders:** the operator (a password-reset form is the classic account-enumeration oracle and
  the classic account-takeover vector); the account owner, whose address may be entered by someone
  else.

## Preconditions

- The device is online.
- The actor believes an account exists for the address they enter.

## Postconditions

- **Success:** the password is replaced, **every existing session and refresh token is revoked**, and
  the user is signed in fresh on this device.
- **Failure:** no credential changes; existing sessions are untouched.

## Trigger

The user selects **Forgot password** on the sign-in screen.

## Main success scenario

1. User selects **Forgot password** and enters their email address.
2. System responds that *if* the address has an account, a reset link has been sent — **without
   confirming either way**.
3. System sends a single-use, short-lived reset link to the address, in the account's preferred
   language.
4. User opens the link.
5. System prompts for a new password and validates it against the password policy
   ([UC-MAIN-001](UC-MAIN-001-create-account.md)).
6. System replaces the credential, **revokes all sessions and refresh tokens**, and consumes the link.
7. System signs the user in on this device.

## Extensions / alternate flows

- **2a. The address has no account** → the response in step 2 is byte-for-byte identical and no mail
  is sent. Same enumeration-resistance rule as sign-up and invitations
  ([ADR-0018](../../adr/0018-circle-and-sharing-model.md)).
- **2b. The address has an *unverified* account** → treated the same from the requester's point of
  view. The reset mail doubles as proof of address control, so completing it also **verifies** the
  email.
- **4a. The link has expired or was already used** → the user is told plainly and offered a new one.
  Reset links are single-use so that a forwarded or archived mail cannot be replayed.
- **4b. The user remembers their password meanwhile** → they simply sign in; the outstanding link
  expires on its own. Requesting a reset does **not** lock the existing password.
- **6a. The user has passkeys** → they are **kept**. A password reset proves control of the email, not
  loss of the device; revoking passkeys would punish the user for forgetting a password they may
  rarely use. (Removing a passkey is an explicit action in
  [UC-MAIN-003](UC-MAIN-003-register-passkey.md).)

## Exceptions

- **Repeated reset requests for one address** → rate-limited per address and per source, so the
  identical response in step 2 cannot be defeated by volume or timing, and so the address cannot be
  mail-bombed.
- **Email delivery fails** → the system cannot and does not report this to the requester; doing so
  would leak that the address exists.
- **Backend unreachable** → reset is unavailable; local, account-free use is unaffected
  ([NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md)).

## Non-functional notes

- **Offline/local-first:** online-only, and irrelevant to the account-free flow.
- **Localization:** the reset mail is sent in the **account's** preferred language
  ([UC-MAIN-005](UC-MAIN-005-manage-profile.md)), not the requesting browser's — the requester may not
  be the account owner ([NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)).
- **Privacy:** the response must be identical in **content, status code and timing** for known and
  unknown addresses. Timing is the part that is easy to get wrong and easy to measure.
- **Performance:** the mail is queued, not awaited; the UI returns immediately.

## Open questions

- Should completing a reset **notify** the account by a second mail ("your password was changed")?
  Standard practice and cheap; recommendation is yes, to be confirmed in the M1 feature spec.

## Traceability

- Requirement: [FR-009](../../requirements/functional/FR-009-reset-password.md); constrained by
  [NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md),
  [NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md),
  [NFR-005](../../requirements/non-functional/NFR-005-account-enumeration-resistance.md)
- ADRs: [ADR-0009](../../adr/0009-authentication-and-passkeys.md),
  [ADR-0018](../../adr/0018-circle-and-sharing-model.md) (enumeration-resistance rule),
  [ADR-0021](../../adr/0021-web-auth-token-model.md) (session revocation)
- Glossary: [User](../../glossary/user.md)
- Origin: split out of [UC-MAIN-002](UC-MAIN-002-sign-in-with-password.md) on 2026-07-27
