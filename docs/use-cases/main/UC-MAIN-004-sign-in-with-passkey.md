---
id: UC-MAIN-004
title: Sign in with a passkey
app: Main
actors: [User]
status: Draft
priority: Should
related: [NFR-001, NFR-003, ADR-0009, ADR-0021]
---

# UC-MAIN-004 — Sign in with a passkey

## Summary / goal

A [User](../../glossary/user.md) signs in without typing a password, by confirming with a passkey
they registered earlier ([UC-MAIN-003](UC-MAIN-003-register-passkey.md)).

## Primary actor & stakeholders

- **Primary actor:** User with at least one registered passkey.
- **Stakeholders:** the operator (phishing-resistant sign-in, no password to steal or reuse).

## Preconditions

- The account has a passkey registered for an authenticator reachable from this device.
- The device is online.

## Postconditions

- **Success:** a session exists exactly as after password sign-in — short-lived access token in
  memory, persisted refresh token ([ADR-0021](../../adr/0021-web-auth-token-model.md)).
- **Failure:** no session; password sign-in is still available.

## Trigger

The user opens the sign-in screen and chooses **Use a passkey** — or the browser offers the passkey
directly in the sign-in field (conditional UI / autofill).

## Main success scenario

1. User opens the sign-in screen.
2. User selects **Use a passkey** (or picks the passkey the browser offers).
3. System starts the WebAuthn authentication ceremony.
4. Browser/OS prompts the user to confirm with the authenticator.
5. User confirms.
6. System verifies the assertion and issues tokens, identically to
   [UC-MAIN-002](UC-MAIN-002-sign-in-with-password.md) step 4.
7. System starts sync and shows the signed-in shell.

## Extensions / alternate flows

- **2a. No passkey is available on this device** → the option is not offered; the user signs in with
  their password. A passkey registered on another device does not help here unless cross-device
  (hybrid) sign-in is available.
- **2b. Cross-device sign-in** → the platform may offer a QR/hybrid flow using a phone's passkey. This
  is entirely platform-driven and depends on the RP-ID settings flagged in
  [UC-MAIN-003](UC-MAIN-003-register-passkey.md).
- **5a. User cancels** → returns to the sign-in screen with password sign-in intact; not an error.
- **6a. The credential was removed server-side** → the assertion is rejected with a message that the
  passkey is no longer registered, and the user falls back to password.
- **7a. First sign-in on a device holding anonymous groups** → the claim prompt applies here exactly
  as in [UC-MAIN-002](UC-MAIN-002-sign-in-with-password.md) (**M3**,
  [ADR-0017](../../adr/0017-anonymous-to-account-claim.md)). The claim is a property of *first
  sign-in*, not of *how* the user signed in.

## Exceptions

- **Ceremony fails or times out** → generic failure; fall back to password sign-in.
- **Backend unreachable** → cannot sign in; a previously signed-in device keeps working offline with
  cached data ([NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md)).

## Non-functional notes

- **Offline/local-first:** as with password sign-in — signing in needs a connection, staying signed in
  does not.
- **Localization:** our labels are DE/EN/FR; the OS prompt is not ours
  ([NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)).
- **Privacy:** no shared secret is transmitted; the assertion is bound to the relying-party origin,
  which is what makes it phishing-resistant.
- **Performance:** typically faster than password entry — one prompt, no typing.

## Decided (2026-07-27)

- **Once an account has a passkey, passkey sign-in is the default offered path** on that device, with
  password sign-in always visible as a fallback rather than hidden behind "other options". The
  fallback stays prominent because a passkey is device-bound and the user may be on a borrowed device.
- **RP-ID strategy** is settled in [UC-MAIN-003](UC-MAIN-003-register-passkey.md) (registrable suite
  domain), which is what makes cross-subdomain sign-in work.

## Open questions

- The **literal suite domain** remains unknown until the deployment ADRs land — see
  [UC-MAIN-003](UC-MAIN-003-register-passkey.md).

## Traceability

- Requirement: [FR-011](../../requirements/functional/FR-011-sign-in-with-passkey.md); constrained by
  [NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md),
  [NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)
- ADRs: [ADR-0009](../../adr/0009-authentication-and-passkeys.md),
  [ADR-0021](../../adr/0021-web-auth-token-model.md)
- Glossary: [User](../../glossary/user.md)
- Origin: [brainstorming — accounts, families & sync (v2)](../../brainstorming/2026-07-24-accounts-families-sync.md), theme A / slice M1
