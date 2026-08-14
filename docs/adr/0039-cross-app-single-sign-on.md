---
status: Accepted
date: 2026-08-13
deciders: [Adrian]
tags: [architecture, security, identity, auth, web, sso]
---

# ADR-0039 — Cross-app single sign-on between the WASM PWAs

## Context and problem statement

The suite now has two standalone Blazor WASM PWAs on **separate origins** — the Main shell
(`circletools.ch`) and Secret-Santa (`secretsanta.circletools.ch`) — both authenticating against the
**same Main identity backend** through the shared [`Ui.Account`](0034-shared-account-session-ui-block.md)
block. They share one account, one API contract and one session implementation, but a user who signs
in on one still arrives at the other **signed out**, and has to sign in again.

The reason is structural, not a missing feature. [ADR-0021](0021-web-auth-token-model.md) keeps the
access token in memory and persists only the refresh token, and [ADR-0026](0026-refresh-token-persistence.md)
keeps that refresh token in the browser's `sessionStorage`/`localStorage`. **Web storage is
origin-scoped**: `secretsanta.circletools.ch` cannot read a token `circletools.ch` wrote, and vice
versa. Each origin runs its own independent session by construction. [ADR-0021](0021-web-auth-token-model.md)
also deliberately rules out a BFF/cookie host, so there is no server-set, domain-scoped cookie the
two subdomains could share either.

**How does a session established on one suite origin become a session on another, without reopening
ADR-0021's token model or ADR-0005's serverless-PWA hosting?**

## Decision drivers

- **Don't reopen [ADR-0021](0021-web-auth-token-model.md) or [ADR-0005](0005-web-hosting-render-model.md).**
  The bearer-in-WASM model and the standalone static PWA are settled; SSO must layer on top of them,
  not replace them with cookies or an always-on BFF.
- **Survive modern browser storage rules.** Safari ITP and Chrome's storage/third-party-cookie
  partitioning make the classic hidden-iframe "silent SSO" between origins unreliable. Whatever is
  chosen must not depend on one origin reading another's cookies or storage from an embedded frame.
- **One implementation, both apps.** Per [ADR-0034](0034-shared-account-session-ui-block.md) the
  session plumbing lives once in `Ui.Account`; the SSO flow must live there too so both frontends —
  and a future MAUI head — get it from one source.
- **Reuse the session machinery that exists.** `SessionService.StartAsync` already mints a token pair
  for a user; the flow should bootstrap a sibling session through it, not invent a parallel path.
- **Degrade, never block.** The authority origin or the network may be unreachable. A failed hand-off
  must fall back to the app's own local sign-in, exactly as [ADR-0021](0021-web-auth-token-model.md)
  requires "unreachable" to degrade rather than destroy.
- **No new open-redirect or token-leak surface.** A cross-origin hand-off that carries a credential in
  a URL is an attack surface unless the credential is single-use, short-lived and the return target is
  allow-listed.

## Considered options

1. **Redirect-based authorization-code hand-off ("connect").** One origin is the SSO authority; a
   requesting app top-level-redirects to it, the authority (if it has a session) mints a short-lived
   single-use code and redirects back, and the requesting app exchanges the code at the API for its
   own fresh session.
2. **Hidden-iframe + `postMessage` silent SSO.** A shared broker origin embedded by both apps holds
   the session and answers token requests over `postMessage`.
3. **Shared parent-domain `HttpOnly` cookie (BFF-style).** The API sets one `Domain=.circletools.ch`
   refresh cookie both subdomains send automatically — true SSO for free, but cookie auth.

## Decision outcome

**Chosen: option 1 — a redirect-based authorization-code hand-off.**

**One origin is the SSO authority: the Main app (`circletools.ch`).** It is the suite hub, it already
owns every account screen through `Ui.Account`, and it is the origin a user is most likely already
signed in to. All apps trust it as the place a session is established. (A dedicated `id.circletools.ch`
authority is a possible later evolution; it is deliberately **not** introduced now, because it would
be one more thing to host for no capability the Main origin does not already provide.)

The flow, when an app (e.g. Secret-Santa) needs an account and `AccountSession.RestoreAsync` finds no
local session:

1. **The app does not immediately show a login form.** It offers to continue with the suite account
   and, on the user's action, performs a **top-level redirect** to the authority's connect endpoint,
   carrying its own origin as `return_uri`, a `client` identifier, and a **PKCE `code_challenge`** it
   derives from a freshly generated verifier it keeps in memory.
2. **On the authority origin**, if `RestoreAsync` recovers a live session, the app calls a new
   **authenticated** API endpoint that mints a **short-lived, single-use authorization code** bound to
   that user, the requesting `client`/`return_uri`, and the `code_challenge`. It then top-level
   redirects back to `return_uri` with `?code=…`. If the authority has **no** session, it shows its
   own sign-in screen first and then continues the same mint-and-redirect — so the user signs in once,
   on the authority, and lands back where they started.
3. **Back on the requesting origin**, the app exchanges the code at a new **anonymous** API endpoint,
   presenting the code and the PKCE **`code_verifier`**. The API validates them, calls
   `SessionService.StartAsync` for the bound user, and returns an ordinary `SessionResponse`. The app
   stores it exactly as a normal sign-in would (access token in memory, refresh token in its own
   storage scope per [ADR-0026](0026-refresh-token-persistence.md)) and immediately **strips the code
   from the URL**.

The result is a **fully independent session per origin, bootstrapped from the authority's** — same
bearer contract, its own refresh chain, its own storage scope. Nothing about [ADR-0021](0021-web-auth-token-model.md)'s
per-origin token model changes; the only thing added is a way to *start* a session from a sibling
origin instead of from a password.

### Why redirects, not the iframe

Option 2 is the historical SPA answer and it is now the fragile one: an embedded cross-origin frame
reading the authority's session depends on third-party cookies / unpartitioned storage, which Safari
already blocks and Chrome is partitioning away. **Top-level navigation is first-party at the authority
for the duration of the redirect**, so the authority can read its own session normally and the hand-off
keeps working as browsers tighten. That robustness is the whole reason to prefer the redirect.

### Security envelope (part of the decision, not follow-up)

- **`return_uri` is allow-listed.** The authority only ever redirects a code to an origin on a fixed
  allow-list of suite origins — the same list the API already maintains as `Cors:AllowedOrigins`
  ([ADR-0013](0013-wasm-api-dev-wiring.md)). This is the control against open-redirect and
  code-exfiltration to an attacker origin.
- **The code is single-use, short-lived (seconds, not minutes), and server-tracked**, bound to one
  user, one `client`/`return_uri`, and one `code_challenge`. Redeeming it consumes it.
- **PKCE binds the code to the requester.** The code travels in a URL (browser history, `Referer`), so
  possession of the code alone must not be enough to redeem it. The requesting app proves possession of
  the `code_verifier` for the `code_challenge` the code was minted against; a code skimmed from a URL is
  useless without it. A strict `Referrer-Policy` and the immediate URL strip reduce the exposure
  further.
- **This does not weaken [ADR-0021](0021-web-auth-token-model.md)'s threat model.** Each origin still
  holds bearer tokens the same way; XSS on one origin is no worse than before. The one new property is
  that a *compromised authority origin* can mint sibling sessions — but a compromised authority origin
  can already act as the user, so the boundary is unchanged in kind.
- **The hand-off never becomes load-bearing.** If the authority or the network is unreachable, the
  redirect/exchange simply fails and the app falls back to its **own local sign-in** (the `Ui.Account`
  login screen both apps already have). Cross-app SSO is a convenience on top of, not a replacement
  for, per-app authentication — the offline/local-first guarantees ([NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md))
  are untouched.

### Where the code lives

- **Client:** the connect/exchange flow joins the session plumbing in
  **`CircleTools.Ui.Account`**, so both frontends (and a later MAUI head, which can host the same flow
  over its own redirect scheme) get it from one place, matching
  [ADR-0034](0034-shared-account-session-ui-block.md). It uses the existing keyed
  `AccountApiClients.Anonymous` client for the token exchange.
- **Server:** two endpoints join `AccountEndpoints` under the existing `/api/v1/account` group — an
  **authenticated** `connect/authorize` (mint code) and an **anonymous** `connect/token` (redeem code
  → `SessionService.StartAsync`). Both reuse the `SessionService`/`AccessTokenIssuer` machinery; only
  the short-lived authorization-code store is new.

Option 2 is rejected for the storage-partitioning fragility above. Option 3 (a shared parent-domain
`HttpOnly` cookie) is **not chosen but explicitly kept on the table**: it would be simpler *and*
improve XSS posture, but it reopens [ADR-0021](0021-web-auth-token-model.md)'s deliberate "no cookies /
no BFF" stance and is a larger decision than adding SSO warrants today. If the product later needs the
stronger XSS resistance an `HttpOnly` cookie gives, that ADR — not this one — is where it gets revisited.

## Consequences

- **Positive:** signing in on one suite app carries into the others, with no change to the token model,
  the hosting model, or the offline story; the session stays independent and bearer-based per origin.
- **Positive:** one flow in `Ui.Account` serves both apps and a future MAUI head; the authority is an
  existing origin, so no new always-on component is introduced.
- **Positive:** the redirect design is resilient to third-party-cookie and storage partitioning, so it
  does not rot as browsers tighten — unlike the iframe alternative.
- **Negative / accepted:** a credential briefly transits a URL. Single-use + short TTL + PKCE +
  `return_uri` allow-list + URL strip are the mitigations, and they are part of the decision, not
  aspirations.
- **Negative / accepted:** the `return_uri` allow-list is per-environment upkeep alongside CORS
  ([ADR-0013](0013-wasm-api-dev-wiring.md)); a careless addition of an untrusted origin would be a
  code-exfiltration hole, so it belongs in the deployment checks.
- **Negative / accepted:** cross-app SSO needs the authority origin reachable; when it is not, the user
  falls back to a per-app sign-in. This is a soft dependency by design.
- **Follow-ups:**
  - Pin the authorization-code TTL, the single-use/PKCE mechanics and the code-store backing in the
    feature spec that implements this (a new `FEAT-…`); add a deployment check that the `return_uri`
    allow-list matches the CORS allow-list.
  - **Brand/domain gate:** the authority origin and the allow-list are only final once the suite
    origins are fixed — the same prerequisite [ADR-0036](0036-cloud-provider-and-hosting.md)/[ADR-0037](0037-transactional-email-provider.md)
    carry, and tied to the outstanding passkey RP-ID work from [ADR-0021](0021-web-auth-token-model.md)/[ADR-0009](0009-authentication-and-passkeys.md).
  - Decide the first cut's trigger: **interactive** ("Continue as …" on the user's action) rather than
    a silent redirect on every cold load, to avoid surprising navigations and offline breakage; a
    silent (`prompt=none`) variant can be added later on the same endpoints.
  - Update [arc42 ch. 8 (cross-cutting)](../arc42/08-crosscutting-concepts.md) §8.4 with the SSO flow,
    and add it to the auth picture in [ch. 5](../arc42/05-building-block-view.md).
- **New risks:** the authority origin becomes a higher-value target — compromising it yields sessions
  on every sibling. Bounded, because it already holds the user's own session; the mitigation is the
  same strict CSP [ADR-0021](0021-web-auth-token-model.md) already makes load-bearing, now doubly so on
  the authority.

## Pros and cons of the options

### 1. Redirect-based authorization-code hand-off (chosen)
- 👍 No cookies, no BFF; reuses `SessionService` and the existing origins allow-list; one flow in
  `Ui.Account` for all apps.
- 👍 Top-level redirects survive third-party-cookie/storage partitioning; the model does not rot.
- 👎 A code transits a URL (needs single-use + short TTL + PKCE + allow-list); needs two new API
  endpoints and a short-lived code store; SSO needs the authority reachable.

### 2. Hidden-iframe + `postMessage` silent SSO
- 👍 Can be fully silent with no visible navigation; a familiar SPA pattern.
- 👎 Depends on third-party cookies / unpartitioned cross-origin storage that Safari blocks and Chrome
  is partitioning away — fragile exactly where it must be reliable.

### 3. Shared parent-domain `HttpOnly` cookie (BFF-style)
- 👍 Simplest possible SSO — one cookie both subdomains send; also removes the refresh token from
  script reach, improving XSS posture.
- 👎 Reopens [ADR-0021](0021-web-auth-token-model.md)'s "no cookies / no BFF"; introduces a server role
  the hosting model avoids; larger decision than adding SSO needs today.

## Links

- Related ADRs: builds on [ADR-0021](0021-web-auth-token-model.md) (token model, unchanged) and
  [ADR-0026](0026-refresh-token-persistence.md) (per-origin storage); reuses
  [ADR-0013](0013-wasm-api-dev-wiring.md) (origins allow-list) and
  [ADR-0034](0034-shared-account-session-ui-block.md) (where the shared client code lives);
  [ADR-0009](0009-authentication-and-passkeys.md), [ADR-0005](0005-web-hosting-render-model.md),
  [ADR-0010](0010-api-style-and-versioning.md)
- arc42 chapters: [08 Cross-cutting concepts](../arc42/08-crosscutting-concepts.md) §8.4,
  [05 Building-block view](../arc42/05-building-block-view.md)
- Requirements: [FR-008](../requirements/functional/FR-008-sign-in-with-password.md),
  [NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md)
- Guideline: [security & privacy guidelines](../guidelines/security-privacy-guidelines.md)
</content>
</invoke>
