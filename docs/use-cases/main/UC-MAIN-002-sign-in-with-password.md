---
id: UC-MAIN-002
title: Sign in with email and password
app: Main
actors: [User]
status: Approved
priority: Must
related: [NFR-001, NFR-003, ADR-0009, ADR-0016, ADR-0017, ADR-0021]
---

# UC-MAIN-002 — Sign in with email and password

## Summary / goal

A [User](../../glossary/user.md) signs in on a device so the suite can act as them —
[syncing](../../glossary/sync.md) their claimed data and showing what is shared with them. Signing in
is **additive**: it never becomes a precondition for using an app.

## Primary actor & stakeholders

- **Primary actor:** User with an existing account.
- **Stakeholders:** the operator (must not leak which addresses are registered, and must resist
  credential stuffing); other users whose shared data becomes reachable once this session exists.

## Preconditions

- An account exists and its email is verified ([UC-MAIN-001](UC-MAIN-001-create-account.md)).
- The device is online.

## Postconditions

- **Success:** a session exists on this device — a short-lived access token in memory and a persisted
  refresh token ([ADR-0021](../../adr/0021-web-auth-token-model.md)); sync is active for account data.
- **Failure:** no session; the app continues to work account-free with whatever is stored locally.

## Trigger

The user chooses **Sign in** in the Main shell, or the app finds no valid session where one is needed.

## Main success scenario

1. User selects **Sign in**.
2. User enters **email** and **password**.
3. System verifies the credentials.
4. System issues a short-lived **access token (held in memory)** and a **refresh token**, per
   [ADR-0021](../../adr/0021-web-auth-token-model.md).
5. System starts [sync](../../adr/0016-sync-and-conflict-resolution.md) for the account's data.
6. System shows the signed-in shell.

## Extensions / alternate flows

- **2a. User prefers a passkey** → [UC-MAIN-004](UC-MAIN-004-sign-in-with-passkey.md).
- **3a. Unknown email or wrong password** → one **generic** failure message that does not distinguish
  the two cases. Distinguishing them would turn the sign-in form into the account-enumeration oracle
  that [ADR-0018](../../adr/0018-circle-and-sharing-model.md) rules out elsewhere.
- **3b. Forgotten password** → [UC-MAIN-007](UC-MAIN-007-reset-password.md).
- **3c. Email not yet verified** → sign-in **succeeds**, but nothing account-scoped works — no sync,
  no claiming, no circle — and the shell shows a persistent prompt to verify
  ([UC-MAIN-001](UC-MAIN-001-create-account.md), decided 2026-07-27).
- **6a. First sign-in on a device holding anonymous groups** → the user is asked which
  [Groups](../../glossary/group.md) to take into the account, per
  [ADR-0017](../../adr/0017-anonymous-to-account-claim.md) — opt-in, default none, and unclaimed
  groups stay local and are not nagged about. The screen is specified in **M3**.
- **Page reload (web)** → the in-memory access token is gone by design; the client silently refreshes
  using the stored refresh token. If that fails, the user is signed out cleanly rather than left in a
  half-authenticated state.

## Exceptions

- **Repeated failed attempts** → progressive rate-limiting / lockout, with an identical response
  shape so the lockout itself does not reveal that the account exists.
- **Refresh-token reuse detected** → treated as theft: the whole token chain is revoked and the user
  must sign in again ([ADR-0021](../../adr/0021-web-auth-token-model.md)).
- **Backend unreachable** → the user cannot sign in; if they were signed in before, the locally cached
  data stays readable offline and sync resumes when connectivity returns.

## Non-functional notes

- **Offline/local-first:** signing in requires a connection; **staying** signed in does not. An
  offline device with a cached copy keeps working, which is the account-free behaviour the app
  already has ([NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md)).
- **Localization:** form, errors and reset emails in DE/EN/FR
  ([NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)).
- **Privacy:** a session grants access to data protected by authorization rather than by absence —
  including assignments ([ADR-0020](../../adr/0020-server-side-assignment-privacy.md)), so session
  handling is load-bearing for [NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md).
- **Performance:** the silent refresh on reload adds a round-trip before the first authenticated
  render; it must degrade sanely when offline rather than blocking startup.

## Decided (2026-07-27)

- **Session lifetimes:** access token **15 minutes**, held in memory; refresh token **30 days**,
  persisted and **rotated on every use**, as a sliding window. Sessions survive a browser restart by
  default — which is what makes sync feel automatic on a personal device, and what an installed PWA
  has to do to feel installed. This satisfies ADR-0021's "minutes, not hours" and its
  rotation/theft-detection requirement; it is pinned here and in the M1 feature spec, as ADR-0021
  asked.
- **No separate "remember me" tick.** Persistence is the default; a user on a shared device signs
  out, which is a clearer and more discoverable control than a checkbox at sign-in. The residual risk
  on a shared family tablet is accepted and noted in
  [ADR-0021](../../adr/0021-web-auth-token-model.md)'s XSS/token trade-off.
- **Password reset gets its own use case** — [UC-MAIN-007](UC-MAIN-007-reset-password.md). It has its
  own actor state, its own email, and its own enumeration-resistance rule.

## Open questions

- The **CSP directive set** is still to be written out — ADR-0021 makes it the primary XSS control and
  defers the concrete directives to the M1 feature spec. It cannot be finalised before the hosting
  origin is known.

## Traceability

- Requirement: [FR-008](../../requirements/functional/FR-008-sign-in-with-password.md); constrained by
  [NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md),
  [NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md),
  [NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md),
  [NFR-005](../../requirements/non-functional/NFR-005-account-enumeration-resistance.md)
- ADRs: [ADR-0009](../../adr/0009-authentication-and-passkeys.md),
  [ADR-0016](../../adr/0016-sync-and-conflict-resolution.md),
  [ADR-0017](../../adr/0017-anonymous-to-account-claim.md),
  [ADR-0021](../../adr/0021-web-auth-token-model.md)
- Glossary: [User](../../glossary/user.md), [Sync](../../glossary/sync.md), [Group](../../glossary/group.md)
- Origin: [brainstorming — accounts, families & sync (v2)](../../brainstorming/2026-07-24-accounts-families-sync.md), theme A / slice M1
