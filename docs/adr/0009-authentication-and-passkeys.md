---
status: Accepted
date: 2026-07-24
deciders: [Adrian]
tags: [architecture, security, identity, passkeys, auth]
---

# ADR-0009 — Authentication & passkeys

## Context and problem statement

Accounts are **optional** (the app works offline with no identity). When a user does create an account,
they authenticate with **email + password** and optionally **passkeys**, and clients call the API with a
token. How do we implement identity and passkeys across Web (WASM) and native (MAUI)?

## Decision drivers

- Offline mode uses **no** identity; auth is purely additive for sync.
- Passkeys/WebAuthn are a first-class requirement.
- Prefer platform-native, well-supported building blocks over bespoke crypto.
- Single suite account shared across all apps.

## Considered options

1. **ASP.NET Core Identity + built-in .NET 10 passkeys** (`AddPasskeys()`) + JWT bearer for APIs
2. ASP.NET Core Identity + a third-party FIDO2 library (e.g. Fido2NetLib)
3. External IdP / OIDC provider (e.g. hosted auth)

## Decision outcome

**Chosen: ASP.NET Core Identity with the built-in .NET 10 passkey support** (`AddPasskeys()`,
`IdentityPasskeyOptions`) for email+password and passkey registration/sign-in, issuing **bearer/JWT**
tokens for API calls. Tokens are stored in MAUI `SecureStorage` on device and in the WASM client's
secure browser storage. **Offline mode uses no identity at all.**

.NET 10 ships passkey support directly in Identity (and the Blazor Web App template), covering the
common WebAuthn scenarios without a third-party library.

## Consequences

- **Positive:** Minimal moving parts; first-party, maintained, documented; passkeys without extra deps;
  one account model for the whole suite.
- **Negative:** Built-in passkey support is scoped to common auth scenarios (not a full WebAuthn toolkit) —
  if we later need advanced ceremonies, add a FIDO2 library then.
- **Follow-ups:** Confirm the **web token model** (bearer-in-WASM vs a BFF/cookie) when the client shell is
  built; wire passkey Relying-Party settings to the suite domain; define token lifetime/refresh.

## Pros and cons of the options

### 1. Identity + built-in passkeys + JWT
- 👍 First-party, low-dependency, passkeys included, well-documented in .NET 10.
- 👎 Passkey feature scoped to common scenarios.

### 2. Identity + third-party FIDO2
- 👍 Full WebAuthn control.
- 👎 Extra dependency/maintenance; unnecessary for our needs now.

### 3. External IdP / OIDC
- 👍 Offloads auth entirely.
- 👎 External dependency/cost; heavier for a local-first hobby-scale product; data-residency questions.

## Links

- arc42: [08 Cross-cutting concepts](../arc42/08-crosscutting-concepts.md)
- Guideline: [security-privacy-guidelines.md](../guidelines/security-privacy-guidelines.md)
- Related open item: web auth token model (deferred — see [ADR register](index.md))
