---
id: NFR-007
title: Store distributability & platform compliance
type: non-functional
status: Approved
priority: Must
source: ADR-0030, ADR-0031, deployment milestone
verification: review
related: [ADR-0030, ADR-0031, ADR-0022, ADR-0023, FR-015, NFR-003, NFR-004]
---

# NFR-007 — Store distributability & platform compliance

## Description

The native apps must remain **publishable and updatable** on the Apple App Store and Google Play.
Unlike most requirements in this register, this one is not satisfied once: Apple and Google each
raise their floor on an annual schedule, and an app that stops meeting it cannot ship an update —
including a security fix. This requirement states the obligations so they are checkable rather than
remembered.

**Build floors (moving targets).** Every release must be built against the currently mandated
toolchain. As of this writing:

| Platform | Obligation | Since / deadline |
|---|---|---|
| Apple | Submissions must be built with **Xcode 26 / iOS 26 SDK** or later | mandatory since **28 Apr 2026** |
| Google Play | New apps and updates must target **API 36** (Android 16) | mandatory from **31 Aug 2026** |
| Google Play | Existing apps must target **API 35** to stay available to new users | current |

Apple bumps the required SDK each spring; Google raises the target API level each August. Both dates
must be re-checked at every release and at every .NET/MAUI upgrade, and the build floor drives the
[iOS build host](../../adr/0031-ios-build-host.md) — a required Xcode implies a required macOS,
which implies required hardware.

**Publisher obligations.** The **DSA trader status** must be declared in App Store Connect. This
applies to every developer, trader or not, and apps without a verified declaration are removed from
the EU App Store — which is the majority of the intended audience.

**In-product obligations.** Both stores require that an app offering account creation also offers
**in-app account deletion** *and* a publicly reachable **account-deletion URL** that a user can find
without installing the app. [ADR-0022](../../adr/0022-account-deletion-and-data-export.md) and
[FR-015](../functional/FR-015-delete-account.md) design the behaviour; this requirement adds that the
web-facing URL must exist and be reachable before first submission.

Both stores also require a reachable **privacy policy URL**, and a **data-safety declaration** (Play)
/ **privacy labels** (App Store) that match what the app actually collects. Under
[NFR-003](NFR-003-multilingual-de-en-fr.md) the privacy policy, the imprint and the store listings
are provided in **DE / EN / FR**.

**Identity permanence.** The **`ApplicationId` / bundle identifier cannot be changed after the first
store release.** It is chosen once, for the lifetime of the app. It is now **set** to
`ch.circletools.secretsanta`, derived from the chosen suite domain `circletools.ch`
([naming](../../process/naming.md)). The same gate applies to the passkey relying-party ID: RP IDs do not
migrate ([ADR-0023](../../adr/0023-local-dev-infrastructure-in-apphost.md)); the RP-ID is `circletools.ch`,
applied at deployment before the first real passkey registration.

## Rationale

Everything else in this register describes what the product should do for its users. This one
describes what the *distribution channels* demand in return for reaching them, and it is written down
because the failure mode is silent and delayed: nothing breaks when a deadline passes, the app simply
becomes unshippable at the moment an update is next needed. The costs are also asymmetric — a missed
target-API deadline is a day of work, a bundle identifier chosen carelessly is permanent, and a
missing account-deletion URL is a rejected submission after everything else was ready.

## Acceptance criteria

```gherkin
Given a release candidate for iOS
When the artifact is built
Then it is built with the Xcode and SDK version currently mandated by Apple

Given a release candidate for Android
When the artifact is built
Then its target API level meets Google Play's current requirement for new uploads

Given the App Store Connect account
When a submission is prepared for EU distribution
Then a DSA trader status has been declared and verified

Given a user with a suite account
When they look for a way to delete it
Then deletion is available in the app
And a publicly reachable account-deletion URL exists outside the app

Given a store listing
When it is reviewed before submission
Then a reachable privacy policy URL is present
And the listing, privacy policy and imprint are available in DE, EN and FR
And the privacy declarations match the data the app actually collects

Given the first store release of an app
When its bundle identifier is set
Then it is derived from the final suite domain and is never changed afterwards
```

## Quality attribute *(NFR only)*

- **Attribute:** deployability / compliance (external platform constraints).
- **Metric & target:**
  - **zero** releases built below the mandated Apple SDK or Play target-API floor;
  - **zero** store-listing fields required by either store left unfilled at submission;
  - account-deletion URL and privacy-policy URL: **reachable** (HTTP 200) at submission time;
  - store metadata present in **3/3** languages;
  - bundle identifier changes after first release: **0**.
- **Measurement approach:** review at each release, driven by the submission checklist in the
  [mobile-apps runbook](../../deployment/mobile-apps.md). The build floors are the parts worth
  automating once CI exists — the target API level is a build property and can be asserted, whereas
  the store-listing completeness is only observable in App Store Connect and the Play Console. Until
  then this is a manual gate, and the runbook is the checklist.

## Dependencies & related

- Governing ADRs: [ADR-0030](../../adr/0030-mobile-store-publisher-identity.md),
  [ADR-0031](../../adr/0031-ios-build-host.md),
  [ADR-0022](../../adr/0022-account-deletion-and-data-export.md),
  [ADR-0023](../../adr/0023-local-dev-infrastructure-in-apphost.md)
- Related: [FR-015](../functional/FR-015-delete-account.md),
  [NFR-003](NFR-003-multilingual-de-en-fr.md),
  [NFR-004](NFR-004-data-protection-and-retention.md)
- Runbooks: [mobile apps](../../deployment/mobile-apps.md),
  [device testing](../../deployment/device-testing.md)
- Process: [naming](../../process/naming.md) — gates the bundle identifier
- arc42: [07 Deployment view](../../arc42/07-deployment-view.md)
- **Open:** the account-deletion and privacy-policy URLs depend on the cloud-hosting decision in the
  [deployment ADR backlog](../../adr/index.md#open-decisions-backlog).
