---
id: FR-010
title: Register and remove passkeys
type: functional
status: Approved
priority: Should
source: UC-MAIN-003
verification: test
related: [UC-MAIN-003, NFR-003, ADR-0009, ADR-0021]
---

# FR-010 — Register and remove passkeys

## Description

A signed-in [User](../../glossary/user.md) with a verified email must be able to register one or more
[Passkeys](../../glossary/passkey.md) against their account, label each one, and remove any of them.

- The suite stores the **public key**, a user-chosen label and the creation date — never a private key
  and never biometric data.
- The **Relying-Party ID is the registrable suite domain (eTLD+1)**, not an app subdomain, so one
  passkey works across the Main shell and every app subdomain.
- The **MAUI client reuses the same RP ID** via associated domains (iOS) / Digital Asset Links
  (Android), so a passkey registered on the web works natively and vice versa.
- Registration is **offered once, proactively and dismissibly, after the first successful sign-in**,
  and is always reachable from security settings.
- The password credential always remains, so **removing the last passkey never locks a user out**.
- Where WebAuthn is unavailable, the option is shown as unavailable with an explanation rather than
  silently hidden.

## Rationale

Passkeys are a first-class requirement in [ADR-0009](../../adr/0009-authentication-and-passkeys.md):
they remove the password from the common path, which is the single biggest reduction in both phishing
risk and sign-in friction. The eTLD+1 relying-party choice is what makes "one suite account" true in
practice rather than only on paper — a passkey scoped to one subdomain would have to be registered
again per app.

## Acceptance criteria

```gherkin
Given a signed-in user with a verified email on a WebAuthn-capable device
When they complete the passkey registration ceremony
Then a credential is stored against their account with a label and creation date
And only the public key and metadata are persisted

Given a user with one or more registered passkeys
When they view security settings
Then each passkey is listed with its label and creation date

Given a user registering a passkey
When they do not supply a label
Then a default label is derived from the platform and remains editable

Given a user registering a passkey
When they cancel the platform ceremony
Then nothing is stored
And the cancellation is not presented as an error

Given an authenticator that already holds a credential for this account
When registration is attempted again on it
Then the user is told this device already has a passkey
And no duplicate credential is created

Given a user with registered passkeys
When they remove every passkey
Then removal succeeds
And they can still sign in with their password

Given a browser or device without WebAuthn support
When the user opens security settings
Then the passkey option is shown as unavailable with an explanation

Given a passkey registered through the web client
When the same user signs in on the MAUI client
Then that passkey is offered, because both clients use the same relying-party ID
```

## Dependencies & related

- Derived from: [UC-MAIN-003](../../use-cases/main/UC-MAIN-003-register-passkey.md)
- Related: [FR-011](FR-011-sign-in-with-passkey.md), [FR-008](FR-008-sign-in-with-password.md),
  [NFR-003](../non-functional/NFR-003-multilingual-de-en-fr.md)
- Governing ADRs: [ADR-0009](../../adr/0009-authentication-and-passkeys.md),
  [ADR-0021](../../adr/0021-web-auth-token-model.md)
- **External dependency:** the **literal suite domain** must exist before the RP ID can be configured
  — open [deployment ADR](../../adr/index.md#open-decisions-backlog) plus
  [naming](../../process/naming.md). The *strategy* is settled; only the string is missing.
- Glossary: [Passkey](../../glossary/passkey.md), [User](../../glossary/user.md)
