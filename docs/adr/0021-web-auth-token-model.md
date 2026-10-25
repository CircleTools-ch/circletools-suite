---
status: Accepted
date: 2026-07-27
deciders: [Adrian]
tags: [architecture, security, identity, auth, web]
---

# ADR-0021 — Web auth token model

## Context and problem statement

[ADR-0009](0009-authentication-and-passkeys.md) settled the identity stack — ASP.NET Core Identity
with built-in .NET 10 passkeys, issuing bearer tokens for API calls — but explicitly **deferred the
web token model** ("bearer-in-WASM vs a BFF/cookie") until the client shell was built. v2 builds it.

The constraint that makes this awkward: [ADR-0005](0005-web-hosting-render-model.md) chose a
**standalone Blazor WASM PWA**. There is no server-side host rendering the app, so there is nowhere
to run a backend-for-frontend without **introducing a server the architecture deliberately avoided**.

**How does the web client hold and present credentials?**

## Decision drivers

- ADR-0005's standalone WASM PWA is a settled decision serving offline/PWA goals; reopening it needs a
  reason stronger than "cookies are safer".
- The native MAUI client already uses bearer tokens in `SecureStorage` (ADR-0009) — a shared model
  keeps one API contract for both clients.
- **A WASM client cannot keep a secret.** Anything the app can read, injected script can read; there
  is no `HttpOnly` equivalent in the browser for a client-side app.
- Account mode now carries genuinely sensitive data — assignments protected only by authorization
  ([ADR-0020](0020-server-side-assignment-privacy.md)) — so a stolen token is a real loss, not a
  theoretical one.
- Hobby scale: no budget for infrastructure that exists only to hold a cookie.

## Considered options

1. **Bearer token in the WASM client**, with short lifetimes, refresh-token rotation and a strict CSP.
2. **Introduce a BFF / cookie host** — a server-side component that holds the token and issues an
   `HttpOnly`, `Secure`, `SameSite` cookie to the browser.

## Decision outcome

**Chosen: option 1 — bearer token in the WASM client.**

The mitigations are part of the decision, not aspirations to revisit later:

- **Short-lived access tokens.** Minutes, not hours. A stolen access token expires quickly on its own.
- **Refresh-token rotation.** Each refresh issues a new refresh token and invalidates the previous
  one; reuse of a consumed refresh token is treated as theft and revokes the whole chain. This turns
  silent long-term compromise into a detectable event.
- **Strict Content-Security-Policy.** No inline script, no wildcard sources. The CSP is the primary
  control here, because XSS is the entire threat model — without it the other two mitigations only
  shorten the window.
- **Access token held in memory**, not in `localStorage`. It does not survive a tab reload (a refresh
  gets a new one), which removes the most trivially harvestable copy. Only the refresh token is
  persisted, and only where a session genuinely needs to survive a reload.
- **MAUI is unchanged:** tokens in `SecureStorage` per ADR-0009. The API sees the same bearer
  contract from both clients.

**ADR-0005 is not reopened.** The BFF would deliver meaningfully better XSS resistance, and we are
choosing not to take it, because it would add the one thing the hosting architecture exists to avoid —
an always-on server in front of a static PWA — along with its hosting cost, deployment surface and an
offline story that no longer works the same way.

**This closes the follow-up left open in ADR-0009.**

## Consequences

- **Positive:** no new infrastructure; ADR-0005 and ADR-0009 stay intact; one token model across web
  and native, so one API contract; the PWA keeps working as a static, cacheable, offline-capable
  artifact.
- **Negative / accepted — XSS yields a usable token.** With no `HttpOnly` protection available to a
  client-side app, any successful script injection can act as the user for at least the access
  token's lifetime. This is the central cost, and it is **accepted, not solved**: short lifetimes,
  rotation and CSP shrink and expose the window, they do not close it.
- **Negative / accepted:** the CSP becomes load-bearing. A careless relaxation (an inline script, a
  third-party widget) silently removes the main mitigation. It needs to be verified as part of the
  deployment checks, not just set once.
- **Negative / accepted:** refresh-token rotation requires server-side tracking of the chain and
  correct handling of the racing-refresh case (two tabs refreshing at once must not look like theft).
- **Negative / accepted:** in-memory access tokens mean a page reload triggers a refresh round-trip —
  a small startup cost, and a path that must degrade sanely when offline.
- **Follow-ups:** pin concrete token lifetimes and the CSP directive set in the M1 feature spec; wire
  passkey Relying-Party settings to the suite domain (also outstanding from ADR-0009); add a
  deployment check that asserts the CSP is present and strict.
- **New risks:** if the product later handles materially more sensitive data than gift assignments,
  this decision should be revisited — the BFF option does not become wrong, only expensive.

## Amendment (2026-10-25, Proposed) — offline is not signed out

From the [2026-10-25 Galaxy S24 device test](../brainstorming/2026-10-25-secret-santa-s24-local-device-test.md)
(F5): the access token lives in memory only, so an app started **without a reachable server** has a refresh
token but nothing to read a principal from. It looked signed out, and because the account's groups are shown
only to their account (ADR-0086), every group owned or taken part in through the account disappeared from the
phone until the next start with a network. Nothing re-tried the session when the network came back.

- **The account a session belongs to is remembered next to the refresh token**, in the same scope, and cleared
  with it (`IRefreshTokenStore.RememberAccountAsync` / `ReadAccountAsync`). It is written when a session
  starts and on every successful refresh. It is **display state, not a credential**: it opens no API call — every
  request still needs a renewed access token — and it only lets the device keep showing what it already holds
  for that account (`SessionAccount`).
- **A waiting session is renewed on every sync trigger** (start, change, reconnect, resume, the foreground poll)
  through `AccountSession.ResumeAsync`, not only once before the first render. A refused refresh still ends the
  session and forgets the account with the token (unchanged: "rejected is the end of the session").
- The header stays signed out while the session waits; the groups show. Telling the person that the session
  is waiting for the network is left open.

## Pros and cons of the options

### 1. Bearer token in WASM (chosen)
- 👍 No new infrastructure; consistent with ADR-0005 and ADR-0009; one model for web and native; PWA
  stays static and offline-capable.
- 👎 No `HttpOnly` protection; XSS yields a usable token; security depends on a CSP staying strict.

### 2. BFF / cookie host
- 👍 `HttpOnly` cookies are not readable by injected script — genuinely stronger against the main
  threat; tokens never touch the browser.
- 👎 Reopens [ADR-0005](0005-web-hosting-render-model.md) and adds an always-on server the
  architecture deliberately avoids; hosting cost and deployment surface; complicates the offline PWA
  story.

## Links

- Related ADRs: closes the deferred follow-up in [ADR-0009](0009-authentication-and-passkeys.md);
  [ADR-0005](0005-web-hosting-render-model.md), [ADR-0013](0013-wasm-api-dev-wiring.md),
  [ADR-0010](0010-api-style-and-versioning.md), [ADR-0020](0020-server-side-assignment-privacy.md)
- arc42: [08 Cross-cutting concepts](../arc42/08-crosscutting-concepts.md) §8.4
- Guideline: [security & privacy guidelines](../guidelines/security-privacy-guidelines.md)
- Source: [brainstorming — accounts, families & sync (v2)](../brainstorming/2026-07-24-accounts-families-sync.md), decision D8
- Glossary: [User](../glossary/user.md)
