---
id: FR-031
title: Start a session from a signed-in sibling app (cross-app SSO)
type: functional
status: Implemented
priority: Should
source: UC-MAIN-017
verification: test
related: [UC-MAIN-017, FR-008, NFR-001, NFR-005, ADR-0039, ADR-0021, ADR-0026, ADR-0013, FEAT-006]
---

# FR-031 — Start a session from a signed-in sibling app (cross-app SSO)

## Description

A [User](../../glossary/user.md) with a live session on one suite origin (e.g. the Main shell at
`circletools.ch`) must be able to start a session on another suite origin (e.g.
`secretsanta.circletools.ch`) **without re-entering their credentials**, via the redirect-based
authorization-code hand-off decided in [ADR-0039](../../adr/0039-cross-app-single-sign-on.md).

- **One origin is the SSO authority** — the Main shell. It holds the session the hand-off draws from.
- A requesting app with **no local session** may offer to continue with the suite account; on the
  user's action it performs a **top-level redirect** to the authority, which — if it has a session —
  mints a **short-lived, single-use authorization code** and redirects back. The requesting app
  exchanges the code for its **own** session.
- The started session is **identical in shape** to one from [FR-008](FR-008-sign-in-with-password.md):
  access token in memory, refresh token persisted per
  [ADR-0026](../../adr/0026-refresh-token-persistence.md), independent per origin. SSO only *bootstraps*
  a session; it does not share one.
- The hand-off is a **convenience layered on top of** per-app sign-in, **never a replacement**. When
  the authority or the network is unreachable, or the exchange fails, the app **falls back to its own
  sign-in form** — it must never dead-end ([NFR-001](../non-functional/NFR-001-local-first-offline.md)).

The mechanism's security envelope is mandatory, not optional hardening (ADR-0039):

- the authorization code is **single-use** and **short-lived** (seconds);
- the requesting app is bound to the code by **PKCE** (`S256`) — possession of the code alone is
  insufficient to redeem it;
- the `return_uri` is validated against an **allow-list of suite origins sourced from the same
  configuration as CORS** ([ADR-0013](../../adr/0013-wasm-api-dev-wiring.md)); a code is never returned
  to an off-list origin;
- the code is removed from the URL immediately after the exchange.

## Rationale

Two suite apps share one account, one API contract and one session implementation
([ADR-0034](../../adr/0034-shared-account-session-ui-block.md)), yet a user still has to sign in again
crossing between them, because web storage is origin-scoped and
[ADR-0021](../../adr/0021-web-auth-token-model.md) deliberately keeps no shared cookie or BFF. SSO
closes that seam **without** reopening the token or hosting model: it adds a way to *start* a session
from a sibling's, keeping every per-origin guarantee. The redirect design (not an iframe) is what keeps
it working as browsers partition third-party storage (ADR-0039).

## Acceptance criteria

```gherkin
Given a user with a live session on the authority origin
When they open a sibling app with no local session and choose to continue with their account
Then a session is started on the sibling with no credentials re-entered
And that session has the same token shape as a password sign-in (FR-008)
And the authorization code is removed from the sibling's URL

Given a user with no session on the authority origin
When a sibling app requests the hand-off
Then the authority presents its own sign-in first
And once signed in, the hand-off completes and returns the user to the sibling, signed in

Given the authority origin or the network is unreachable
When a sibling app attempts the hand-off
Then the sibling falls back to its own sign-in form
And the user can complete a normal password or passkey sign-in
And the account-free and offline behaviour is unchanged

Given a return_uri whose origin is not on the suite allow-list
When an authorization code is requested
Then no code is issued

Given an authorization code that has already been redeemed
When it is presented again
Then the request is denied and no session is started

Given an authorization code that has expired
When it is presented
Then the request is denied and no session is started

Given a valid, unexpired authorization code
When it is presented without the matching PKCE code_verifier
Then the request is denied and no session is started
```

## Dependencies & related

- Derived from: [UC-MAIN-017](../../use-cases/main/UC-MAIN-017-single-sign-on-across-apps.md)
- Related: [FR-008](FR-008-sign-in-with-password.md) (the session shape this reuses),
  [NFR-001](../non-functional/NFR-001-local-first-offline.md) (the never-dead-end fallback),
  [NFR-005](../non-functional/NFR-005-account-enumeration-resistance.md) (the hand-off must reveal
  nothing about account existence to an off-list origin)
- Governing ADRs: [ADR-0039](../../adr/0039-cross-app-single-sign-on.md) (the decision),
  [ADR-0021](../../adr/0021-web-auth-token-model.md) / [ADR-0026](../../adr/0026-refresh-token-persistence.md)
  (unchanged token model this bootstraps into),
  [ADR-0013](../../adr/0013-wasm-api-dev-wiring.md) (the origins allow-list),
  [ADR-0034](../../adr/0034-shared-account-session-ui-block.md) (where the shared flow lives)
- Feature: [FEAT-006](../../features/FEAT-006-m5-cross-app-sso.md)
- Glossary: [User](../../glossary/user.md)
</content>
