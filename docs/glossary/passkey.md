---
term: Passkey
contexts: [Identity]
status: Draft
---

# Passkey

## Definition

A WebAuthn/FIDO2 credential registered by a [User](user.md) that lets them sign in on a device by
confirming with the platform authenticator (Face/Touch/Windows Hello) or a security key, instead of
typing a password. The suite stores only the **public** key plus metadata — a user-chosen label and
the creation date. The private key never leaves the user's device, and no biometric data is ever
transmitted or stored.

A user may register **several** passkeys (typically one per device) and may remove any of them. A
passkey never replaces the password credential: the password remains the always-available fallback, so
removing the last passkey cannot lock anyone out.

Passkeys are **additive to an account**, and an account is itself optional — the account-free flows
never authenticate at all, so passkeys are irrelevant to them
([NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md)).

## Bounded context(s)

- **Identity:** part of the `User` aggregate's credential set. Backed by ASP.NET Core Identity's
  built-in .NET 10 passkey support per [ADR-0009](../adr/0009-authentication-and-passkeys.md).

The **Relying-Party ID** is the registrable suite domain (eTLD+1), not an app subdomain, so one
passkey works across the Main shell and every app subdomain — see
[UC-MAIN-003](../use-cases/main/UC-MAIN-003-register-passkey.md).

## Synonyms / do-not-use

- **Synonyms:** WebAuthn credential, FIDO2 credential (both are the technical spelling of the same
  thing; `Passkey` is the canonical name in code and UI).
- **Do NOT use:** "security key" for a passkey in general (a hardware security key is one kind of
  authenticator, not the concept); "biometric login" (the biometric unlocks the authenticator locally —
  it is not the credential and never reaches us); "token" (that is the
  [session](../adr/0021-web-auth-token-model.md) bearer token, a different thing entirely).

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | Passkey |
| EN | Passkey |
| FR | Clé d'accès |

## Related terms

- [User](user.md)

## First appearance

- [ADR-0009 — Authentication & passkeys](../adr/0009-authentication-and-passkeys.md)
