---
status: Accepted
date: 2026-09-17
deciders: [Adrian]
tags: [mobile, ios, android, maui, auth, passkeys, webauthn, security]
---

# ADR-0066 — Native passkeys on the mobile heads

## Context and problem statement

The suite's account sign-in offers **password** and **passkey** (WebAuthn) authentication (FR-008,
FR-010, FR-011). On the browser heads the passkey ceremony runs through `navigator.credentials`
(`wwwroot/js/passkeys.js`, behind the shared `IPasskeyCeremony` seam). On the **MAUI heads** the
ceremony has been a **stub since FEAT-002** — `MauiPasskeyCeremony` throws "not yet available", and the
account pages catch it and show a graceful "passkeys aren't available here" message (FR-011). Password
sign-in works fully on device; only the passkey buttons are inert.

Passkeys were deferred on mobile for one concrete reason, recorded in the stub and in FEAT-002: the
WebAuthn **relying-party ID (RP-ID)** is the registrable suite domain, and it could not be settled while
the **brand and domain were deliberately deferred** ([naming](../process/naming.md)). A passkey is bound
to its RP-ID for life, so registering any real passkey against a placeholder RP-ID would have been
unrecoverable.

**That blocker is gone.** The brand and domain were chosen on **2026-08-13** — `circletools.ch`, with
the RP-ID fixed as `circletools.ch` ([NFR-007](../requirements/non-functional/NFR-007-store-distributability.md),
[ADR-0023](../adr/0023-local-dev-infrastructure-in-apphost.md)). The store-preparation pass
([ADR-0065](0065-hosted-macos-ios-builds.md)) also brought the iOS head onto the build path and wired iOS
deep links. So the question is now open again: **should the mobile heads implement native passkeys, and
how?**

## Decision drivers

- **Feature parity & product promise.** The app is marketed as offering passkey accounts; a visible
  affordance that only errors is weak UX and a minor App Store 2.1 ("completeness") risk.
- **The deferral reason is resolved.** RP-ID = `circletools.ch` is settled and permanent.
- **One seam already exists.** `IPasskeyCeremony` is platform-agnostic (WebAuthn JSON in, WebAuthn JSON
  out); the server issues options and verifies results unchanged. Only the ceremony is per-platform.
- **No Mac for the iOS loop** ([ADR-0065](0065-hosted-macos-ios-builds.md)): iOS passkey code cannot be
  built or debugged locally; the only test loop is TestFlight on a real device.
- **External dependencies gate the runtime, not the code.** Passkeys need the RP domain to serve
  association files (`assetlinks.json` for Android, `apple-app-site-association` for iOS) with real
  identifiers (Play App Signing SHA-256, Apple Team ID), which depend on the store accounts and live
  hosting — none of which are ready yet.

## Considered options

1. **Keep passkeys stubbed on mobile** — password-only on device, revisit later.
2. **Implement native passkeys on both heads now**, behind the existing seam, using each platform's
   system passkey API (Android Credential Manager, iOS AuthenticationServices).
3. **Implement Android now, defer iOS** — Android is testable on the maintainer's own phone without a
   Mac; iOS is not.

## Decision outcome

**Chosen: option 2 — implement native passkeys on both heads behind `IPasskeyCeremony`**, with the
realistic split that **Android is implemented and verifiable now**, while **iOS is implemented but
verified later** (first TestFlight build on a real iPad/iPhone), because there is no local iOS loop.

- **Android** uses **Credential Manager** (`androidx.credentials`), which consumes and produces the
  **standard WebAuthn JSON** the seam already speaks — a near 1:1 mapping onto `CreateAsync`/`AssertAsync`.
- **iOS** uses **AuthenticationServices** (`ASAuthorizationPlatformPublicKeyCredentialProvider`), which
  works in binary `NSData`, so the ceremony does the base64url↔binary translation to and from the WebAuthn
  JSON. Written now, reviewed against the API docs (no local compile), verified on device later.
- **No server or seam change.** The RP-ID stays `circletools.ch`; the Main service issues options and
  verifies results exactly as it does for the web heads.
- **Association files** carry the passkey relations: `assetlinks.json` gains
  `delegate_permission/common.get_login_creds`; a new `apple-app-site-association` carries the
  `webcredentials` section (and `applinks` for the Universal Links from ADR-0065). Both need real
  identifiers filled in at go-live (tracked as follow-ups, alongside the existing GL-9).

The passkey affordance on the mobile heads is **enabled only once the association files carry real
identifiers and the RP domain is live** — until then the graceful "not available here" message (FR-011)
remains the correct fallback, so the code can land ahead of the external prerequisites without shipping a
button that fails.

## Consequences

- **Positive:** passkey sign-in reaches the mobile heads with no server change; the "optional passkey
  account" promise becomes real on device; removes the App Store 2.1 concern once enabled.
- **Positive:** the platform seam proves its worth — the entire change is two `MauiPasskeyCeremony`
  implementations plus association-file entries.
- **Negative / accepted:** the **iOS ceremony ships unverified** until a device test (no Mac, per
  ADR-0065). Mitigated by writing it against the current API docs and an explicit device-test follow-up.
- **Negative / accepted:** **runtime is gated on external setup** — the Play App Signing SHA-256, the
  Apple Team ID + Associated Domains capability, and the live RP domain serving both association files.
  The code is inert (falls back to the FR-011 message) until these exist.
- **Negative / accepted:** a new binding dependency on `androidx.credentials` (Android) via CPM.
- **Follow-ups:**
  1. Fill `assetlinks.json` with the real Play App Signing SHA-256 (existing **GL-9**) and add the
     `get_login_creds` relation.
  2. Serve `apple-app-site-association` with the real Apple **Team ID** (`webcredentials` +
     `applinks`), and enable **Associated Domains** on the App ID + regenerate the provisioning profile.
  3. **Device-verify** both ceremonies (Android on the Pixel; iOS on the first TestFlight build).
  4. Enable the passkey affordance on the mobile heads once 1–2 are live (until then FR-011 fallback).
- **New risks:** an iOS API mismatch not caught without a local compile — reduced by the doc-based review
  and the device follow-up; a mismatch between the server's WebAuthn option shape and a platform API's
  expectations — reduced because the server JSON is standard and Android takes it verbatim.

## Pros and cons of the options

### 1. Keep stubbed
- 👍 Zero work; password sign-in already covers accounts on device.
- 👎 The advertised passkey feature stays broken on mobile; minor App Store 2.1 risk; the resolved
  deferral reason is left unclaimed.

### 2. Both heads now (chosen)
- 👍 Full parity behind the existing seam; no server change; strongest product outcome.
- 👎 iOS ships unverified until a device test; new Android binding dependency.

### 3. Android now, iOS later
- 👍 Only ships what can be verified locally.
- 👎 Leaves iOS — the tablet/iPhone-priority platform — without a written ceremony, so it is re-opened
  from scratch later instead of reviewed-and-ready; the code cost of writing iOS now is small.

## Links

- Supersedes the mobile-passkey deferral in [FEAT-002](../features/FEAT-002-m1-identity.md) /
  [FR-010](../requirements/functional/FR-010-manage-passkeys.md),
  [FR-011](../requirements/functional/FR-011-sign-in-with-passkey.md)
- Related ADRs: [ADR-0021](0021-web-auth-token-model.md) (token model),
  [ADR-0023](0023-local-dev-infrastructure-in-apphost.md) (RP-ID = `circletools.ch`),
  [ADR-0028](0028-browser-platform-adapters-block.md) (platform-seam pattern),
  [ADR-0034](0034-shared-account-session-ui-block.md) (shared account UI),
  [ADR-0065](0065-hosted-macos-ios-builds.md) (iOS build host; no local iOS loop)
- Requirements: [FR-008](../requirements/functional/FR-008-sign-in-with-password.md),
  [FR-010](../requirements/functional/FR-010-manage-passkeys.md),
  [FR-011](../requirements/functional/FR-011-sign-in-with-passkey.md),
  [NFR-007](../requirements/non-functional/NFR-007-store-distributability.md)
- Runbook: [Deployment — mobile apps](../deployment/mobile-apps.md)
