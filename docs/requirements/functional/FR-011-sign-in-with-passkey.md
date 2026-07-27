---
id: FR-011
title: Sign in with a passkey
type: functional
status: Draft
priority: Should
source: UC-MAIN-004
verification: test
related: [UC-MAIN-004, NFR-001, ADR-0009, ADR-0021]
---

# FR-011 — Sign in with a passkey

## Description

A [User](../../glossary/user.md) with a registered [Passkey](../../glossary/passkey.md) must be able
to sign in with it instead of a password, obtaining a session **identical** to the one issued by
[FR-008](FR-008-sign-in-with-password.md) — same token shape, same lifetimes, same rotation.

- Once an account has a passkey on the current device, **passkey sign-in is the default offered path**.
- **Password sign-in stays visible as a fallback**, not hidden behind "other options" — a passkey is
  device-bound and the user may be on a borrowed device.
- Where the platform offers it, the passkey may be presented directly in the sign-in field
  (conditional UI / autofill).
- A cancelled ceremony returns to sign-in without being presented as an error.
- A credential removed server-side is rejected, and the user falls back to password.

## Rationale

Passkey sign-in is the payoff for [FR-010](FR-010-manage-passkeys.md): faster than typing, and
phishing-resistant because the assertion is bound to the relying-party origin. Keeping the password
path prominent is deliberate — defaulting to passkeys while hiding the fallback strands anyone on a
device that has no credential, which is exactly the person who most needs to sign in.

## Acceptance criteria

```gherkin
Given a user with a passkey available on this device
When they open the sign-in screen
Then passkey sign-in is offered as the default path
And password sign-in remains visible as a fallback

Given a user with an available passkey
When they complete the authentication ceremony
Then a session is issued identical in token shape and lifetimes to password sign-in
And sync starts for the account's data

Given a device with no passkey for this account
When the user opens the sign-in screen
Then passkey sign-in is not offered
And password sign-in works normally

Given a user starting a passkey ceremony
When they cancel it
Then they return to the sign-in screen with password sign-in intact
And no error styling is shown

Given a passkey that has been removed server-side
When the user attempts to sign in with it
Then the assertion is rejected with a message that the passkey is no longer registered
And password sign-in is offered

Given a user signing in with a passkey for the first time on a device holding anonymous groups
When sign-in completes
Then the group-claim prompt applies exactly as it does after password sign-in
```

## Dependencies & related

- Derived from: [UC-MAIN-004](../../use-cases/main/UC-MAIN-004-sign-in-with-passkey.md)
- Related: [FR-010](FR-010-manage-passkeys.md), [FR-008](FR-008-sign-in-with-password.md),
  [NFR-001](../non-functional/NFR-001-local-first-offline.md)
- Governing ADRs: [ADR-0009](../../adr/0009-authentication-and-passkeys.md),
  [ADR-0021](../../adr/0021-web-auth-token-model.md),
  [ADR-0017](../../adr/0017-anonymous-to-account-claim.md) (the claim prompt is a property of first
  sign-in, not of how the user signed in)
- **External dependency:** the suite domain, as in [FR-010](FR-010-manage-passkeys.md).
- Glossary: [Passkey](../../glossary/passkey.md), [User](../../glossary/user.md)
