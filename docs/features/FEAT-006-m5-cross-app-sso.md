---
id: FEAT-006
title: M5 — Cross-app single sign-on
status: In progress
owner: Adrian
target-milestone: M5 (v2 — cross-app SSO)
bounded-context: Platform
related: [UC-MAIN-017, FR-031, ADR-0039, ADR-0021, ADR-0026, ADR-0013, ADR-0034, ADR-0009, FR-008, NFR-001, TP-006]
---

# FEAT-006 — M5 — Cross-app single sign-on

## Problem & goals

- **Problem:** the suite now has two standalone WASM PWAs on **separate origins** — the Main shell
  (`circletools.ch`) and Secret-Santa (`secretsanta.circletools.ch`) — sharing one account, one API
  contract and one session implementation ([ADR-0034](../adr/0034-shared-account-session-ui-block.md)).
  But web storage is **origin-scoped**, so a user signed in on one arrives at the other **signed out**
  and has to sign in again. [ADR-0021](../adr/0021-web-auth-token-model.md) keeps the access token in
  memory and only the refresh token in per-origin storage; there is no shared cookie or BFF to bridge
  the two origins.
- **Goals:** a user who has a live session on one suite origin can start a session on another **without
  re-entering credentials**, using the redirect-based authorization-code hand-off decided in
  [ADR-0039](../adr/0039-cross-app-single-sign-on.md). Each origin keeps its **own independent bearer
  session** ([ADR-0021](../adr/0021-web-auth-token-model.md)/[ADR-0026](../adr/0026-refresh-token-persistence.md));
  SSO only *bootstraps* one from a sibling's. The hand-off is a **convenience layered on top of** per-app
  sign-in, never a replacement — when it can't run, the app falls back to its own login form.
- **Non-goals:**
  - **A shared cookie / BFF.** Option 3 in [ADR-0039](../adr/0039-cross-app-single-sign-on.md) (a
    parent-domain `HttpOnly` cookie) is explicitly **not** built here; it would reopen ADR-0021 and is a
    separate, larger decision.
  - **A dedicated `id.circletools.ch` authority origin.** The Main origin is the authority; a standalone
    identity origin is a possible later evolution, out of scope now (ADR-0039).
  - **A fully silent (`prompt=none`) redirect on every cold load.** The first cut is **interactive**
    ("Continue as …" on the user's action). The endpoints are shaped so a silent variant can be added
    later without a contract change, but it is not built now (ADR-0039 follow-up).
  - **Single sign-out across apps.** Signing out of one origin ends only that origin's session. A
    cross-app "sign out everywhere" reuses the server's existing `RevokeAll` but is its own slice; not here.
  - **Changing how any app holds tokens.** No change to `AccountSession`, the in-memory access-token
    store, or the `sessionStorage`/`localStorage` split — only a new *entry* path into a session.

## Requirements & use cases covered

| Item | Covered? | Notes |
|------|----------|-------|
| [UC-MAIN-017](../use-cases/main/UC-MAIN-017-single-sign-on-across-apps.md) → [FR-031](../requirements/functional/FR-031-single-sign-on-across-apps.md) | ✅ | the whole feature: start a session on a sibling origin from a signed-in authority, without re-entering credentials; independent per-origin session; never-dead-end fallback |
| [FR-008](../requirements/functional/FR-008-sign-in-with-password.md) | ✅ (extended) | adds a second way to *start* a session — an authorization-code exchange — alongside password/passkey; the resulting session is identical |
| [NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md) | ✅ (preserved) | the hand-off is never in the write path and never load-bearing; an unreachable authority falls back to local sign-in, offline/account-free is untouched |
| [NFR-005](../requirements/non-functional/NFR-005-account-enumeration-resistance.md) | ✅ | the authority returns no code to an off-allow-list origin, so the hand-off reveals nothing to an attacker origin |

## Design overview

The Main origin (`circletools.ch`) is the **SSO authority**: it owns every account screen through
[`Ui.Account`](../adr/0034-shared-account-session-ui-block.md) and is the origin a user is most likely
already signed in to. A requesting app with **no local session** offers to continue with the suite
account and, on the user's action, does a **top-level redirect** to the authority, which — if it has a
session — mints a **short-lived, single-use authorization code** and redirects back; the requesting app
exchanges the code at the API for its own fresh session. Top-level navigation (not an embedded iframe)
is what makes this survive third-party-cookie / storage partitioning (ADR-0039).

The flow lives once in **`CircleTools.Ui.Account`** so both frontends — and a future MAUI head — get it
from one place; the two new server endpoints join `AccountEndpoints` under the existing
`/api/v1/account` group and reuse `SessionService`.

```mermaid
sequenceDiagram
    autonumber
    participant SS as secretsanta.circletools.ch<br/>(requesting app)
    participant U as User (browser)
    participant M as circletools.ch<br/>(authority origin)
    participant API as Main API /api/v1/account/connect

    Note over SS: RestoreAsync → no local session
    SS->>SS: generate PKCE verifier (in memory)<br/>+ code_challenge
    SS->>U: "Continue with your CircleTools account"
    U->>M: top-level redirect ?return_uri&client&code_challenge
    Note over M: RestoreAsync recovers a live session<br/>(else show authority login first)
    M->>API: POST connect/authorize (bearer)<br/>{ client, return_uri, code_challenge }
    API-->>M: { code } (single-use, ~30s TTL)
    M->>U: top-level redirect return_uri?code=…
    U->>SS: back with ?code=…
    SS->>API: POST connect/token (anonymous)<br/>{ code, code_verifier }
    API->>API: validate code + PKCE + return_uri allow-list<br/>→ SessionService.StartAsync(user)
    API-->>SS: SessionResponse (access, refresh, expiresIn)
    SS->>SS: store as normal sign-in; strip ?code from URL
    Note over SS,M: fresh independent session per origin;<br/>authority unreachable → fall back to local login
```

## Domain model impact

- **Aggregates / entities / value objects:** **none added or changed.** This is a Platform/identity
  mechanism, not app domain. No `SecretSanta.Core` or `Circles` change.
- **New Identity concept (server):** a **connect authorization code** — a short-lived, single-use grant
  bound to `(userId, client, return_uri, code_challenge, expiresAt)`. It is a credential-issuance
  mechanism, not ubiquitous language; **no glossary term** is coined (the user-facing concept remains
  *signing in*, already covered by [User](../glossary/user.md)/[FR-008](../requirements/functional/FR-008-sign-in-with-password.md)).
- **Glossary:** no additions.

## API / contract changes

All under `/api/v1/account` ([ADR-0010](../adr/0010-api-style-and-versioning.md)); contracts in
`CircleTools.Main.Contracts`; client flow in `CircleTools.Ui.Account`.

- **`POST /api/v1/account/connect/authorize`** — **bearer-authorized** (`RequireAuthorization`). The
  signed-in authority app calls it. Request `{ client, returnUri, codeChallenge, codeChallengeMethod }`.
  The endpoint validates `returnUri` against the **allow-list** (see below), mints a code bound to the
  caller's user id + the request fields, stores it with a short TTL, and returns `{ code }`. It does
  **not** redirect — the WASM client owns navigation.
- **`POST /api/v1/account/connect/token`** — **anonymous** (`AllowAnonymous`), used by the
  `AccountApiClients.Anonymous` client (same reason `refresh` is anonymous:
  [SessionRefresher](../adr/0021-web-auth-token-model.md) must never loop through the authorizing
  handler). Request `{ code, codeVerifier }`. The endpoint atomically **consumes** the code (single-use),
  verifies PKCE (`SHA-256(codeVerifier) == codeChallenge`) and expiry, then calls
  `SessionService.StartAsync(user)` and returns the existing **`SessionResponse`** — the same DTO
  `login`/`refresh` return, so the client stores it through the unchanged `AccountSession` path.
- **New request DTOs:** `ConnectAuthorizeRequest`, `ConnectAuthorizeResponse` (`{ Code }`),
  `ConnectTokenRequest`. **No response DTO is new** — `connect/token` reuses `SessionResponse`.
- **New client keys/routes** in `AccountApiClients`: a `ConnectAuthorizeRoute` / `ConnectTokenRoute`
  constant pair alongside `AccountRoute`.
- **Versioning / compatibility:** purely **additive** — two new routes, new request DTOs, reused
  `SessionResponse`. No change to `login`, `refresh`, `logout`, passkeys, sync, circles, or heartbeat.

### `return_uri` allow-list

The authority only ever mints/returns a code for a `returnUri` whose origin is on a **fixed allow-list
of suite origins** — the same set the API already maintains as `Cors:AllowedOrigins`
([ADR-0013](../adr/0013-wasm-api-dev-wiring.md), `__0` = main-web, `__1` = secretsanta-web). This is the
control against open-redirect and code exfiltration to an attacker origin, and it MUST be sourced from
the same configuration the CORS policy reads, not a second hand-maintained list (a divergence is a hole).

## Data & migration considerations

- **Postgres (server):** the authorization code is **short-lived and single-use**; it does **not** need
  a durable relational table. Preferred backing is the same in-process/short-TTL store the app already
  uses for transient server state; if a table is used it lives in the Identity/`auth` schema as
  `(code_hash, user_id, client, return_uri, code_challenge, expires_at, consumed_at)` keyed by
  `code_hash`, with a sweep of expired rows. **The code is stored hashed**, never in plaintext, exactly
  as refresh secrets are ([ADR-0026](../adr/0026-refresh-token-persistence.md) / `RefreshTokenSecret`).
  No foreign key beyond the user id already used by the refresh chains.
- **SQLite / `localStorage` (client):** **no persistent schema change.** The PKCE `code_verifier` lives
  in **memory** (or `sessionStorage` only for the duration of the redirect round-trip, cleared on
  exchange) — it must not outlive the hand-off. The resulting refresh token is stored through the
  **unchanged** [ADR-0026](../adr/0026-refresh-token-persistence.md) path (`sessionStorage` by default,
  `localStorage` behind "stay signed in").
- **Sync impact:** none. SSO starts a session; it does not touch synced aggregates.

## Localization impact

New user-facing strings, all in **DE / EN / FR** (tone per [ADR-0032](../adr/0032-ui-tone-of-voice-and-gender-neutral-terms.md)),
in the `Ui.Account` block's `AccountStrings` so both apps share them:

- **The continue affordance:** "Continue with your CircleTools account" (the interactive trigger).
- **The authority hand-off state:** a brief "signing you in…" while the redirect round-trips.
- **The fallback message:** when the authority is unreachable or the exchange fails — phrased as a
  neutral *fact* that leads into the normal login form ("Couldn't reach your account — sign in here"),
  **not** an error dialog.
- No count-bearing strings here, so the M2/M3 singular-plural trap does not apply — but the DE/EN/FR
  walkthrough still verifies each string reads correctly on screen (`CLAUDE.md` UI-verification rule).

## Testing strategy

Paired plan: **TP-006** (to be written with this spec). Weighted toward the two silent-failure surfaces:

- **The security envelope**, because each control fails invisibly: a code must be **single-use** (a
  second `connect/token` with the same code fails), **short-lived** (expired code fails), **PKCE-bound**
  (right code + wrong/absent `code_verifier` fails), and **origin-bound** (a `return_uri` off the
  allow-list is refused at `authorize`). These are unit/integration tests against the two endpoints, plus
  an assertion that the allow-list is **the same source** as CORS.
- **The degrade path**, because a broken hand-off must never strand the user: authority unreachable →
  the requesting app shows its **own** login form and a successful password sign-in still works; offline
  → account-free/local-first behaviour unchanged (NFR-001).
- **The happy path across two origins**, as the `CLAUDE.md`-mandated **two-origin, DE/EN/FR browser
  walkthrough**: sign in on Main → open Secret-Santa → "Continue with your account" → land signed in
  with the URL's `?code` stripped and a clean console; then the reverse direction; then with the
  authority *signed out* (authority login appears, then hand-off completes).

## Rollout / deployment & feature flags

- **Inert for account-free users** by construction — the flow only ever runs when an app has no local
  session *and* the user chooses to continue with an account; the offline product is unchanged.
- **Config, not a flag:** the `return_uri` allow-list and the authority origin are configuration keyed
  to the deployed suite origins. A **deployment check** asserts the allow-list matches
  `Cors:AllowedOrigins` (ADR-0039 follow-up), sitting beside the CSP check ADR-0021 already requires.
- **Brand/domain gate:** the authority origin and allow-list are only final once the suite origins are
  fixed — the same prerequisite [ADR-0036](../adr/0036-cloud-provider-and-hosting.md)/[ADR-0037](../adr/0037-transactional-email-provider.md)
  carry, and tied to the outstanding passkey RP-ID work ([ADR-0021](../adr/0021-web-auth-token-model.md)/[ADR-0009](../adr/0009-authentication-and-passkeys.md)).
  Buildable now against the fixed dev ports/origins; *shippable* only after the domain gate.

## Numbers & product questions this spec fixes

| Question | Decision |
|----------|----------|
| **Authority origin** | The **Main app** (`circletools.ch`). No new identity origin (ADR-0039). |
| **Code TTL** | Short — target **~30 s**, enough for one redirect round-trip; pin the exact value with the token lifetimes in `TokenLifetimeOptions`. |
| **Single-use** | The code is **consumed atomically** on first `connect/token`; a second attempt fails. |
| **Requester binding** | **PKCE** (`S256`): `authorize` stores `code_challenge`; `token` requires the matching `code_verifier`. Possession of the code alone is insufficient. |
| **Origin binding** | `return_uri` validated against the **CORS allow-list** at `authorize`; codes are never returned to an off-list origin. |
| **Code at rest** | Stored **hashed** (like refresh secrets), short-lived; no durable relational need. |
| **Trigger** | **Interactive** ("Continue with your account") for the first cut; silent (`prompt=none`) deferred, same endpoints. |
| **Session result** | `connect/token` returns the existing **`SessionResponse`** via `SessionService.StartAsync`; the client stores it through the unchanged `AccountSession` path — a full, independent per-origin session. |
| **Failure** | Any failure (unreachable authority, expired/rejected code) → **fall back to the app's own login form**; never a dead-end. |

## Risks & open questions

- **Traceability chain closed.** [FR-031](../requirements/functional/FR-031-single-sign-on-across-apps.md)
  (functional) and [UC-MAIN-017](../use-cases/main/UC-MAIN-017-single-sign-on-across-apps.md) are
  authored and registered, traced backward to [ADR-0039](../adr/0039-cross-app-single-sign-on.md) and
  forward to this spec and [TP-006](TP-006-m5-cross-app-sso.md). FR-031 is `Draft` and moves to
  `Implemented` when this feature is built and TP-006 is green — the same status flow M1–M4 followed.
- **The authority origin becomes a higher-value target** — compromising it yields sessions on every
  sibling ([ADR-0039](../adr/0039-cross-app-single-sign-on.md)). Bounded (it already holds the user's own
  session); the mitigation is the strict CSP [ADR-0021](../adr/0021-web-auth-token-model.md) already
  makes load-bearing, doubly so on the authority.
- **The `return_uri` allow-list and the CORS allow-list must not drift.** Wire them to one source and
  assert it in a test/deployment check, or a future added origin silently becomes a code-exfiltration
  hole.
- **Redirect UX must not surprise or trap.** First cut is user-initiated, and every failure lands on the
  local login form. A silent variant, if added later, must keep the same fallback.
- **The `code_verifier` must not leak or persist.** Keep it in memory (or `sessionStorage` cleared on
  exchange); a verifier surviving in durable storage defeats PKCE's point.

## Definition of Done

- [x] **FR-031 + UC-MAIN-017 authored and traced** (backward to ADR-0039, forward to here and TP-006) —
      the process gate to start building; FR-031 flips to `Implemented` when TP-006 is green
- [x] Meets the acceptance criteria of the new FR at the API level (session shape identical to FR-008 —
      the SSO session refreshes through the normal `/refresh` path; NFR-001 preserved by the callback's
      degrade-to-local path)
- [x] Builds clean under analyzers (warnings = errors) — server, `Ui.Account`, and both web apps
- [x] Automated tests per TP-006 pass: **single-use, expiry, PKCE-bound, origin-bound** (`ConnectEndpointsTests`,
      `ConnectAuthorizationCodeServiceTests`, `PkceTests`), plus the mint→redeem→refresh parity path.
      **Still owed:** the bUnit degrade-to-local test and the two-origin browser E2E (below)
- [x] Architecture boundaries respected — client flow in `Ui.Account`, no app-UI coupling; server
      endpoints reuse `SessionService` (`UiAccountLayeringTests` + arch suite green)
- [x] Localized strings provided (DE/EN/FR) in `AccountStrings` (`LocalizationParityTests` green)
- [ ] Docs, API comments updated; **arc42 ch. 8 §8.4 / ch. 5 SSO diagram still to add**
- [ ] **Two-origin DE/EN/FR browser walkthrough** (the `CLAUDE.md` UI-verification gate, run from the
      Aspire AppHost): Main→Secret-Santa and reverse, authority-signed-out case, `?code` stripped,
      console clean, and the authority-unreachable fallback to local login. **Not yet run** — this is
      what flips FEAT-006 to `Done` and FR-031 to `Implemented`
- [x] Traceability links closed
</content>
