---
id: UC-MAIN-001
title: Create a suite account
app: Main
actors: [Visitor]
status: Approved
priority: Must
related: [NFR-001, NFR-003, ADR-0009, ADR-0017, ADR-0018, ADR-0021]
---

# UC-MAIN-001 — Create a suite account

## Summary / goal

A person who has been using the apps without an account creates one [User](../../glossary/user.md)
account for the whole suite, so they can later join a [Circle](../../glossary/circle.md) and
[sync](../../glossary/sync.md) their data across devices. The account is **optional and additive** —
everything they could do before, they can still do without signing in.

## Primary actor & stakeholders

- **Primary actor:** Visitor — a person with no account, using the app anonymously or arriving from
  an [Invitation](../../glossary/invitation.md) email.
- **Stakeholders:** the operator (must store no more personal data than needed, and must not let the
  sign-up form become an account-enumeration oracle); existing users (their address must not be
  confirmable by a stranger attempting to sign up with it).

## Preconditions

- The app is open (web or native) and the device is **online** — this is one of the few flows that
  genuinely requires connectivity.
- No account exists for the actor, or the actor believes none exists.

## Postconditions

- **Success:** a `User` exists with a display name, email and password credential; the email is
  verified; the actor is signed in on this device.
- **Failure:** no account is created, no session is issued, and any local anonymous data is untouched.

## Trigger

The visitor chooses **Create account** in the Main shell, or opens an invitation email addressed to
an address that has no account.

## Main success scenario

1. Visitor selects **Create account**.
2. Visitor enters a **display name**, an **email address**, and a **password**.
3. System validates the input against the password policy and the email format.
4. System creates the `User` in a **pending-verification** state and sends a verification email in the
   actor's current language.
5. System tells the visitor to check their inbox — without stating whether the address was new.
6. Visitor opens the verification link.
7. System marks the email as **verified** and signs the visitor in, issuing tokens per
   [ADR-0021](../../adr/0021-web-auth-token-model.md).
8. System shows the signed-in shell.

## Extensions / alternate flows

- **2a.** Display name is empty → system blocks submission and prompts for one.
- **3a.** Password fails the policy → system explains the rule and blocks submission.
- **4a. The address already has an account** → the system's response to the visitor is **identical**
  to step 5. Instead of a verification mail, the address receives a *"someone tried to create an
  account with your address — sign in instead"* mail. The sign-up form must never confirm that an
  address is registered — the same enumeration-resistance rule invitations follow
  ([ADR-0018](../../adr/0018-circle-and-sharing-model.md)).
- **6a.** The verification link has expired → system offers to resend it; the pending account is not
  discarded.
- **6b.** Visitor never verifies → the account stays pending: the visitor **may sign in**, but nothing
  account-scoped works (no sync, no claiming, no circle) and the shell shows a persistent prompt to
  verify. The pending account is **purged after 30 days**.
- **1a. Arriving from an invitation** → after step 7 the visitor lands in the inviting circle. That
  landing step belongs to **M2** and is specified with the circle use cases, not here.
- **7a. This device holds anonymous groups** → on this first sign-in the visitor is asked which
  [Groups](../../glossary/group.md) to take into the account, per
  [ADR-0017](../../adr/0017-anonymous-to-account-claim.md). The claim screen itself is **M3** scope;
  M1 only has to leave the hook.

## Exceptions

- **Email delivery fails** → the account remains pending; the visitor can trigger a resend. The
  system does not claim the mail was delivered.
- **Backend unreachable** → system reports that account creation needs a connection and returns the
  visitor to the app, which keeps working account-free ([NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md)).
- **Repeated sign-up attempts from one source** → rate-limited, so the identical-response rule in
  **4a** cannot be defeated by volume or timing.

## Non-functional notes

- **Offline/local-first:** sign-up is online-only. Failing to sign up must leave the account-free
  experience completely intact — no half-created state, no nagging.
- **Localization:** the form and the **transactional emails** are DE/EN/FR, sent in the language the
  actor is using at sign-up (`de-CH` default) — see [NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md).
- **Privacy:** this is the **first personal data the suite stores server-side** — display name and
  email. Password is stored only as a hash (ASP.NET Core Identity,
  [ADR-0009](../../adr/0009-authentication-and-passkeys.md)). See the
  [security & privacy guidelines](../../guidelines/security-privacy-guidelines.md).
- **Performance:** form submission is a single round-trip; verification mail should arrive within
  seconds, and the UI must not block on it.

## Decided (2026-07-27)

- **Unverified accounts may sign in, but nothing account-scoped works** until the email is verified —
  no [sync](../../glossary/sync.md), no claiming, no circle join or invite. Nothing that touches other
  people happens on an unproven address, and a typo'd address never accumulates server-side data.
- **Unverified pending accounts are purged after 30 days.**
- **Password policy:** minimum **12 characters**, no composition rules and no forced rotation
  (NIST SP 800-63B), checked against a breached-password list. Length beats character-class theatre,
  and ASP.NET Core Identity supports this directly.
- **Rate limits:** per-address and per-source, with progressive backoff — on sign-up, sign-in and
  reset requests alike. This is what stops the identical-response rule in **4a** from being defeated
  by volume or timing.

## Open questions

- **Avatar** in the profile — captured as an idea, not in the selected v2 scope. Deferred to **M2**;
  see [UC-MAIN-005](UC-MAIN-005-manage-profile.md).

## Traceability

- Requirement: [FR-007](../../requirements/functional/FR-007-create-suite-account.md); constrained by
  [NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md),
  [NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md),
  [NFR-004](../../requirements/non-functional/NFR-004-data-protection-and-retention.md),
  [NFR-005](../../requirements/non-functional/NFR-005-account-enumeration-resistance.md)
- ADRs: [ADR-0009](../../adr/0009-authentication-and-passkeys.md),
  [ADR-0017](../../adr/0017-anonymous-to-account-claim.md),
  [ADR-0018](../../adr/0018-circle-and-sharing-model.md),
  [ADR-0021](../../adr/0021-web-auth-token-model.md)
- Glossary: [User](../../glossary/user.md), [Circle](../../glossary/circle.md),
  [Sync](../../glossary/sync.md), [Invitation](../../glossary/invitation.md)
- Origin: [brainstorming — accounts, families & sync (v2)](../../brainstorming/2026-07-24-accounts-families-sync.md), theme A / slice M1
