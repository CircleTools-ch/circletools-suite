---
id: UC-MAIN-003
title: Register a passkey
app: Main
actors: [User]
status: Draft
priority: Should
related: [NFR-003, ADR-0009, ADR-0021]
---

# UC-MAIN-003 — Register a passkey

## Summary / goal

A signed-in [User](../../glossary/user.md) adds a **passkey** to their account so they can sign in on
this device without typing a password, using the platform authenticator (Face/Touch/Windows Hello) or
a security key.

## Primary actor & stakeholders

- **Primary actor:** User, already signed in.
- **Stakeholders:** the operator (fewer passwords typed and phished); the user's household (each
  device can carry its own passkey).

## Preconditions

- The user is **signed in** ([UC-MAIN-002](UC-MAIN-002-sign-in-with-password.md)) and their email is
  verified.
- The browser or OS supports WebAuthn and exposes an authenticator.
- The device is online.

## Postconditions

- **Success:** a passkey credential (public key + metadata + a user-chosen label) is stored against
  the `User` and listed in their security settings.
- **Failure:** no credential is stored; the password credential is unchanged and still works.

## Trigger

The user chooses **Add a passkey** in profile → security.

## Main success scenario

1. User opens **Security** in their profile and selects **Add a passkey**.
2. System starts the WebAuthn registration ceremony via ASP.NET Core Identity's built-in passkey
   support ([ADR-0009](../../adr/0009-authentication-and-passkeys.md)).
3. Browser/OS prompts the user to confirm with the platform authenticator.
4. User confirms (biometric, device PIN, or security key).
5. System stores the credential against the user's account.
6. User gives the passkey a **label** (e.g. "Phone", "Work laptop") so it can be recognised later.
7. System lists the passkey with its label and creation date.

## Extensions / alternate flows

- **1a. The browser or device has no WebAuthn support** → the option is shown as unavailable with a
  plain explanation, not hidden silently; password sign-in remains.
- **4a. User cancels the ceremony** → nothing is stored; the user returns to security settings with no
  error styling — cancelling is a normal choice, not a failure.
- **4b. The authenticator already holds a credential for this account** → the platform reports it; the
  system explains that this device already has a passkey rather than creating a duplicate.
- **6a. User skips the label** → a sensible default is derived (e.g. browser/platform name) and can be
  edited later.
- **Multiple passkeys** → a user may register one per device; the list supports several.
- **Removing a passkey** → the user may delete any passkey. The password credential remains the
  always-available fallback, so removing the last passkey never locks anyone out.

## Exceptions

- **Ceremony fails or times out** → system reports it and leaves the account untouched; the user can
  retry.
- **Backend unreachable** → registration is unavailable; sign-in by password is unaffected.

## Non-functional notes

- **Offline/local-first:** passkey registration is online-only and is irrelevant to the account-free
  flow, which never authenticates at all.
- **Localization:** the app's own labels and explanations are DE/EN/FR
  ([NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)); the OS-level prompt
  in step 3 is rendered by the platform and is **not** ours to translate.
- **Privacy:** only a **public** key and metadata are stored — no biometric data ever leaves the
  device.
- **Performance:** the ceremony is a couple of round-trips plus a local prompt; it must not block the
  rest of the profile screen.

## Decided (2026-07-27)

- **RP-ID strategy: the registrable suite domain (eTLD+1), not a subdomain.** Setting the
  Relying-Party ID to `<suite-domain>` rather than `secretsanta.<suite-domain>` means one passkey
  works across the Main shell and every app subdomain — which is the whole point of "one suite
  account". Choosing this now de-blocks the design; only the literal domain string waits on hosting.
- **MAUI reuses the same RP ID**, via associated domains (iOS) / Digital Asset Links (Android), so a
  passkey registered on the web works in the native app and vice versa. To be confirmed against the
  platform behaviour during the M1 build.
- **Registration is offered proactively once**, right after the first successful sign-in, as a
  dismissible prompt — never blocking, and always also reachable from settings. A passkey nobody is
  told about is a passkey nobody registers.

## Open questions

- **The literal suite domain is still unknown** — it waits on the hosting/domain decision in the
  deployment ADRs, and on [naming](../../process/naming.md). This is the one hard external dependency
  in M1: the strategy above is settled, but the RP ID cannot be *configured* until the domain exists.

## Traceability

- Requirements: to be derived; constrained by
  [NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)
- ADRs: [ADR-0009](../../adr/0009-authentication-and-passkeys.md),
  [ADR-0021](../../adr/0021-web-auth-token-model.md)
- Glossary: [User](../../glossary/user.md)
- Origin: [brainstorming — accounts, families & sync (v2)](../../brainstorming/2026-07-24-accounts-families-sync.md), theme A / slice M1
