---
id: FR-008
title: Sign in with email and password
type: functional
status: Draft
priority: Must
source: UC-MAIN-002
verification: test
related: [UC-MAIN-002, NFR-001, NFR-005, ADR-0009, ADR-0021]
---

# FR-008 — Sign in with email and password

## Description

The Main shell must let a [User](../../glossary/user.md) sign in with their email address and
password, establishing a session on that device:

- **access token: 15 minutes, held in memory** (never `localStorage`);
- **refresh token: 30 days, persisted, rotated on every use** as a sliding window;
- **reuse of a consumed refresh token is treated as theft** — the whole token chain is revoked.

Sessions survive a browser restart by default; there is no separate "remember me" control, and
signing out is the way to end a session on a shared device. On page reload the client silently
refreshes; if the refresh fails the user is signed out cleanly rather than left half-authenticated.

Failed sign-in must not distinguish an unknown address from a wrong password
([NFR-005](../non-functional/NFR-005-account-enumeration-resistance.md)), and repeated failures are
rate-limited with progressive backoff.

## Rationale

Sign-in is what turns the optional account into working sync, so it must be unremarkable and durable
on a personal device — a PWA that forgets you every visit does not feel installed. The token shape
implements [ADR-0021](../../adr/0021-web-auth-token-model.md)'s mitigations, which that ADR treats as
part of the decision rather than as later hardening: a WASM client cannot hold a secret, so short
lifetimes and rotation are what shrink and expose the compromise window.

## Acceptance criteria

```gherkin
Given a verified account
When the user signs in with the correct email and password
Then an access token valid for 15 minutes is issued and held in memory only
And a refresh token valid for 30 days is issued and persisted
And sync starts for the account's data

Given a valid session
When the access token expires and the client refreshes
Then a new access token and a new refresh token are issued
And the previous refresh token is invalidated

Given a refresh token that has already been consumed
When it is presented again
Then the request is denied
And every token in that chain is revoked

Given a signed-in web session
When the page is reloaded
Then the client obtains a new access token via the stored refresh token without user interaction

Given an unknown email address, or a known address with a wrong password
When sign-in is attempted
Then the failure response is identical in content, status code and timing for both cases

Given repeated failed sign-in attempts for one address or from one source
When further attempts are made
Then they are rate-limited with progressive backoff
And the response shape does not reveal whether the account exists

Given a user who was signed in and is now offline
When they open the app
Then locally cached data remains readable
And sync resumes when connectivity returns
```

## Dependencies & related

- Derived from: [UC-MAIN-002](../../use-cases/main/UC-MAIN-002-sign-in-with-password.md)
- Related: [FR-007](FR-007-create-suite-account.md), [FR-009](FR-009-reset-password.md),
  [FR-011](FR-011-sign-in-with-passkey.md),
  [NFR-001](../non-functional/NFR-001-local-first-offline.md),
  [NFR-002](../non-functional/NFR-002-assignment-privacy.md),
  [NFR-005](../non-functional/NFR-005-account-enumeration-resistance.md)
- Governing ADRs: [ADR-0021](../../adr/0021-web-auth-token-model.md) (token model — lifetimes pinned
  here), [ADR-0009](../../adr/0009-authentication-and-passkeys.md),
  [ADR-0016](../../adr/0016-sync-and-conflict-resolution.md)
- Glossary: [User](../../glossary/user.md), [Sync](../../glossary/sync.md)
