---
id: UC-MAIN-017
title: Stay signed in when moving between suite apps
app: Main
actors: [User]
status: Approved
priority: Should
related: [FR-031, FR-008, NFR-001, ADR-0039, ADR-0021]
---

# UC-MAIN-017 — Stay signed in when moving between suite apps

## Summary / goal

A [User](../../glossary/user.md) who is signed in on one CircleTools app opens another one and is
**already recognised** — they continue with their account instead of signing in a second time. Two apps
that share one account should feel like one product, not two log-ins.

## Primary actor & stakeholders

- **Primary actor:** a signed-in user opening a second suite app on the same device/browser.
- **Stakeholders & interests:** the user, who wants one sign-in, not one per subdomain; the operator,
  who needs the hand-off to leak nothing and to bind the credential to the app that asked for it.

## Preconditions

- The user has a suite [account](../../glossary/user.md) and a **live session on the authority origin**
  (the Main shell), or is able to sign in there.
- The second app is a suite origin on the **allow-list** ([ADR-0013](../../adr/0013-wasm-api-dev-wiring.md)).

## Postconditions

- **Success:** the second app holds its **own** session — an in-memory access token and a persisted
  refresh token per [ADR-0026](../../adr/0026-refresh-token-persistence.md) — started without the user
  re-entering credentials, and its URL no longer carries the authorization code.
- **Failure:** the second app is signed out and shows its **own** sign-in form; nothing is stranded, and
  the account-free/offline experience is unchanged.

## Trigger

The user opens a second suite app that finds **no local session** and chooses to continue with their
CircleTools account.

## Main success scenario

1. The second app starts, restores no local session, and offers to **continue with the CircleTools
   account** (rather than immediately demanding a sign-in).
2. The user chooses it. The app generates a one-time **PKCE** secret it keeps in memory and performs a
   **top-level redirect** to the authority, carrying its own origin as the return target and the PKCE
   challenge.
3. The authority recognises the user's live session and asks the API to mint a **short-lived, single-use
   authorization code** bound to that user, the requesting app and the challenge.
4. The authority **redirects back** to the second app with the code.
5. The second app **exchanges** the code at the API, proving possession of the PKCE secret, and receives
   a normal session. It stores it exactly as a password sign-in would and **removes the code from the
   URL**.
6. The user is signed in on the second app, on their account, having typed nothing.

## Extensions / alternate flows

- **3a. The authority has no live session** → it presents **its own** sign-in first
  ([UC-MAIN-002](UC-MAIN-002-sign-in-with-password.md)); on success it continues from step 3. The user
  signs in **once**, on the authority.
- **1a. The user declines to continue with an account** → the second app shows its own sign-in form (or
  stays in the account-free experience where that app has one); no hand-off happens.
- **5a. The user has "stay signed in" tastes that differ per device** → the second app applies its own
  [ADR-0026](../../adr/0026-refresh-token-persistence.md) session/durable choice; the hand-off does not
  carry the authority's choice across.

## Exceptions

- **The authority or the network is unreachable** → the hand-off fails and the second app falls back to
  its **own** sign-in form; a normal password/passkey sign-in still works
  ([NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md)). SSO is never in the
  critical path.
- **The returned code is expired, already used, or the PKCE proof is wrong** → the exchange is refused
  and no session starts; the app shows its own sign-in. A code skimmed from the URL by another party is
  useless without the in-memory PKCE secret.
- **The requesting origin is not on the allow-list** → the authority mints/returns **no** code. An
  attacker origin cannot use the hand-off to harvest a session.

## Non-functional notes

- **Offline/local-first:** the hand-off only runs online and only when chosen; with no connection the
  app behaves exactly as the account-free/offline product does. It never blocks startup.
- **Localization:** the continue affordance, the brief "signing you in…" state, and the
  authority-unreachable fallback message are DE/EN/FR
  ([NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)), phrased as neutral
  facts, not errors.
- **Privacy:** an authorization code transits a URL briefly; it is single-use, short-lived, PKCE-bound
  and stripped after exchange ([ADR-0039](../../adr/0039-cross-app-single-sign-on.md)). The authority
  never returns a code — and so reveals nothing — to an origin off the allow-list
  ([NFR-005](../../requirements/non-functional/NFR-005-account-enumeration-resistance.md)).
- **Performance:** the hand-off is one redirect round-trip and one token exchange; it should complete in
  well under a second on a normal connection, and its failure path is immediate.

## Open questions

- **Silent vs interactive.** The first cut is **user-initiated** ("Continue with your account"), not a
  silent redirect on every cold load — a surprise navigation and a broken offline start are worse than
  one click. A silent (`prompt=none`) variant is a later option on the same mechanism
  ([ADR-0039](../../adr/0039-cross-app-single-sign-on.md) follow-up).
- **Single sign-out.** Ending the session on one app does **not** end it on the others in this use case.
  A cross-app "sign out everywhere" (the server already tracks the chains) is a separate use case.

## Traceability

- Requirements: [FR-031](../../requirements/functional/FR-031-single-sign-on-across-apps.md); reuses the
  session shape of [FR-008](../../requirements/functional/FR-008-sign-in-with-password.md); bounded by
  [NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md) and
  [NFR-005](../../requirements/non-functional/NFR-005-account-enumeration-resistance.md)
- ADRs: [ADR-0039](../../adr/0039-cross-app-single-sign-on.md) (the decision),
  [ADR-0021](../../adr/0021-web-auth-token-model.md) / [ADR-0026](../../adr/0026-refresh-token-persistence.md)
  (token model), [ADR-0013](../../adr/0013-wasm-api-dev-wiring.md) (allow-list),
  [ADR-0034](../../adr/0034-shared-account-session-ui-block.md) (shared flow)
- Feature spec: [FEAT-006](../../features/FEAT-006-m5-cross-app-sso.md)
- Related use cases: [UC-MAIN-002](UC-MAIN-002-sign-in-with-password.md)
- Glossary: [User](../../glossary/user.md)
</content>
