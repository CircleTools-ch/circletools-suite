---
status: Accepted
date: 2026-09-17
deciders: [Adrian]
tags: [ci-cd, build, deployment, mobile, ios, maui, github, cost]
---

# ADR-0065 — iOS builds on GitHub-hosted macOS runners

**Supersedes [ADR-0031](0031-ios-build-host.md)** (own Apple-silicon Mac as the iOS build host).
Amends [ADR-0038](0038-ci-cd-pipeline.md) (the iOS leg no longer waits on a self-hosted Mac) and
[ADR-0053](0053-backend-free-first-mobile-release.md) (iOS is no longer deferred to v2+).

## Context and problem statement

[ADR-0031](0031-ios-build-host.md) chose to **buy an Apple-silicon Mac mini** (~CHF 650–800) as the
durable iOS build host, keeping a cloud/hosted Mac only as a documented fallback. That decision
optimised for **durability** against Apple's yearly SDK treadmill and for smooth **interactive** work
(simulator, signing debugging). It was Accepted on 2026-07-29, before any store enrolment existed.

Two things have changed since:

- **The launch shape moved.** The maintainer now wants **iOS available at the family launch**, not in a
  later v2 — because most of the family uses iPhones, and Google Play's new-personal-account rule
  requires ~20 testers over 14 continuous days before production. Onboarding iPhone-owning testers via
  **TestFlight** is the fastest route to that tester count, so iOS is on the launch critical path.
  [ADR-0053](0053-backend-free-first-mobile-release.md) had deferred iOS to v2+.
- **The available hardware still cannot build iOS.** There is no Mac the project owns; the only Mac is a
  shared **Intel** MacBook Pro that most likely cannot even run the required macOS 26 / Xcode 26 (the
  narrow model list in ADR-0031). Buying a Mac mini now is a real cash outlay for a free hobby app, and
  it front-loads the single largest cost in the whole shipping story purely to unblock a family test.

**What produces a store-shippable, TestFlight-uploadable iOS artifact for the launch, without owning a
Mac — and at a cost proportional to a free seasonal app?**

## Decision drivers

- **iOS at launch.** iOS is now on the critical path (TestFlight is how iPhone family members test), so
  its build path must exist now, not after a hardware purchase.
- **No Mac owned; no cash outlay forced by the launch.** Avoid front-loading the Mac-mini cost just to
  unblock a family test.
- **Cost proportional to a hobby project.** Releases are infrequent (a `v*` tag cuts a deliberate mobile
  release, [ADR-0047](0047-versioning-and-release-strategy.md)); per-minute macOS cost only bites with
  frequent builds.
- **One system, native to the repo.** Stay on GitHub Actions ([ADR-0038](0038-ci-cd-pipeline.md)) — no
  second CI provider, no self-hosted machine to maintain.
- **Reversible.** If cost or Apple's SDK cadence later makes owning a Mac the cheaper/safer answer, the
  move back must be a runner swap, not a rewrite.

## Considered options

1. **Buy an Apple-silicon Mac mini now** — ADR-0031's original choice.
2. **GitHub-hosted macOS runners** — build/sign/upload iOS from `macos-*` runners in the same Actions
   workflows the repo already uses.
3. **Third-party mobile CI** (Codemagic, Bitrise) — turnkey macOS + signing on another provider.
4. **Keep iOS deferred** — ship Android-only, revisit iOS after a Mac is acquired (status quo of
   ADR-0031 + ADR-0053).

## Decision outcome

**Chosen: option 2 — build, sign and upload iOS on GitHub-hosted macOS runners.**

The iOS release leg runs on a `macos-*` runner inside the existing `release-mobile.yml`, on the same
`v*` tag that already cuts the Android release. The runner selects an Xcode that satisfies the .NET 10
iOS floor (Xcode 26.x, per ADR-0031's version chain), restores the MAUI workload, and — when signing
secrets exist — imports a distribution certificate and provisioning profile, publishes a signed `.ipa`
for `net10.0-ios`, and uploads it to **TestFlight** via an App Store Connect API key. No csproj change
is needed: the MAUI head already exposes `net10.0-ios` on macOS
(`Condition="$([MSBuild]::IsOSPlatform('osx'))"`).

**Why hosted, not the owned Mac, for now.** ADR-0031's cost case against hosted macOS assumed a habit of
building on **every push**. This project does not: iOS is built only on a `v*` tag and on manual
`workflow_dispatch` — a handful of builds a month. At GitHub's 10× macOS multiplier a ~20–30 min iOS
build costs well under an hour of included minutes each, so realistic release cadence stays at or near
**CHF 0/month**, versus a CHF 650–800 up-front purchase. For a free app whose iOS need right now is "get
TestFlight builds to the family", hosted wins on arithmetic and on time-to-launch. **ADR-0031's own
reasoning is preserved, not discarded** — it becomes the *upgrade trigger* below.

**Cost is bounded deliberately:** iOS stays **out of the per-push CI gate** (`ci.yml` keeps building
Android + Windows on Windows, iOS is not compiled on every push). It is built only on release tags and
manual dispatch. Adding an iOS compile check to the everyday gate is an explicit, cost-incurring
follow-up, not a default.

The **Intel MacBook Pro bridge and the eventual Mac-mini purchase are both retired as the plan** and
kept only as the documented upgrade path (see follow-ups). The Windows PC stays the primary development
machine; iOS is never built locally.

## Consequences

- **Positive:** iOS ships at launch with **no Mac owned and ~CHF 0 recurring** at this cadence. TestFlight
  distribution to the family's iPhones/iPad becomes possible immediately after Apple enrolment
  ([ADR-0030](0030-mobile-store-publisher-identity.md)) and the first signed build.
- **Positive:** one system (GitHub Actions) spans the core gate, the VPS deploy, and now the iOS release
  leg — no self-hosted machine to keep patched, no second CI provider.
- **Positive:** the CHF 650–800 Mac-mini outlay is avoided for a free app with no revenue.
- **Negative / accepted:** **no local iOS interactive loop.** There is no simulator on the developer's
  desk and signing problems must be debugged through CI runs, which is slower than on a real Mac. This is
  the friction ADR-0031 warned about, accepted as proportionate for an infrequent-release hobby app.
- **Negative / accepted:** **signing material lives in GitHub secrets** — the distribution certificate
  `.p12`, the provisioning profile, and an App Store Connect API key. Stored as encrypted Actions
  secrets under [ADR-0050](0050-secrets-management.md), with the authoritative copy in the password
  manager. This is the "keys on someone else's machine" cost ADR-0031 named.
- **Negative / accepted:** **exposure to Apple's SDK treadmill via the hosted image.** If GitHub's macOS
  image lags a freshly-mandated Xcode/iOS SDK around an Apple spring deadline, a release can be blocked
  until the image updates (or a specific Xcode is selected). Tracked as a new risk; the owned-Mac upgrade
  path is the mitigation if it ever bites.
- **Follow-ups:**
  1. Add the **`ios` job** to `release-mobile.yml` (this ADR's implementation): Xcode select → workload
     restore → signed `.ipa` when secrets exist, compile-check otherwise → TestFlight upload. *(Built
     alongside this ADR.)*
  2. Define the iOS **signing secrets** under [ADR-0050](0050-secrets-management.md):
     `IOS_DIST_CERT_P12_BASE64`, `IOS_DIST_CERT_PASSWORD`, `IOS_PROVISIONING_PROFILE_BASE64`,
     `APPSTORE_ISSUER_ID`, `APPSTORE_KEY_ID`, `APPSTORE_API_PRIVATE_KEY`. Produce them once from the
     Apple Developer portal + App Store Connect (no local Mac required).
  3. **iOS Universal Links** for reveal links — [ADR-0053](0053-backend-free-first-mobile-release.md)
     gave Android App Links (`assetlinks.json`) but iOS needs an **Associated Domains** entitlement plus
     an `apple-app-site-association` file served from `secretsanta.circletools.ch/.well-known/`. Until
     then iOS reveal links open in the browser (the same offline web fallback), which is functional but
     not the in-app experience. Separate build step.
  4. Record the Xcode/macOS floor and the hosted-image dependency in the
     [mobile runbook](../deployment/mobile-apps.md); re-check at every .NET and Xcode bump.
- **New risks:** replaces ADR-0031's risk **R6** (Intel bridge expiry) with a new risk in
  [arc42 ch. 11](../arc42/11-risks-and-technical-debt.md): a mandated-SDK / hosted-image lag can block an
  iOS release around an Apple deadline. Mitigation is the owned-Mac upgrade path.

### Upgrade trigger — when to revisit and buy the Mac (ADR-0031 preserved)

Move back to an **owned Apple-silicon Mac** (self-hosted runner, ADR-0031's case) if any of these hold:

- iOS build frequency rises enough that the 10× macOS minutes approach the price of a Mac mini;
- a hosted-image Xcode lag actually blocks a required submission around an Apple SDK deadline;
- interactive iOS work (real simulator debugging, signing forensics) becomes a recurring need.

At that point the change is a **runner swap** (`runs-on: macos-*` → the self-hosted label) plus moving
the signing material onto that machine — no pipeline rewrite. ADR-0031's cost/durability analysis stands
as the justification for that future move.

## Pros and cons of the options

### 1. Own Apple-silicon Mac mini (ADR-0031, now superseded)
- 👍 Durable; fast local builds; no per-minute meter; can be a self-hosted CI runner.
- 👍 Real local simulator and signing debugging.
- 👎 CHF 650–800 up front for a free app — the largest single cost, spent only to unblock a family test.
- 👎 Another machine to keep on Apple's macOS/Xcode update treadmill.

### 2. GitHub-hosted macOS runners (chosen)
- 👍 iOS at launch with no Mac owned and ~CHF 0 at this release cadence.
- 👍 Same CI system, same `v*` tag as Android; nothing new to sign up for.
- 👍 Always a current macOS/Xcode image (usually); no hardware to maintain.
- 👎 No local interactive loop; signing debugged through CI runs.
- 👎 Signing keys live in GitHub secrets; exposure to hosted-image SDK lag at Apple deadlines.

### 3. Third-party mobile CI (Codemagic / Bitrise)
- 👍 Turnkey macOS + code-signing management; generous free tiers.
- 👎 A second provider/identity beside GitHub for no benefit the hosted runners don't already give here.

### 4. Keep iOS deferred (status quo)
- 👎 iPhone family members cannot test; blocks the fastest path to Play's 20-tester threshold; fails the
  "iOS at launch" driver.

## Links

- Supersedes: [ADR-0031](0031-ios-build-host.md) (iOS build host — own Mac)
- Amends: [ADR-0038](0038-ci-cd-pipeline.md) (CI/CD — iOS leg was a self-hosted Mac runner),
  [ADR-0053](0053-backend-free-first-mobile-release.md) (iOS was deferred to v2+)
- Related ADRs: [ADR-0030](0030-mobile-store-publisher-identity.md) (Apple enrolment / signing identity),
  [ADR-0047](0047-versioning-and-release-strategy.md) (`v*` tag cuts a mobile release; version numbers),
  [ADR-0050](0050-secrets-management.md) (where the signing secrets live),
  [ADR-0060](0060-local-notification-scheduling-and-permission.md) (iOS was deferred there too)
- Requirements: [NFR-007](../requirements/non-functional/NFR-007-store-distributability.md)
- arc42: [07 Deployment view](../arc42/07-deployment-view.md),
  [11 Risks and technical debt](../arc42/11-risks-and-technical-debt.md)
- Runbook: [Deployment — mobile apps](../deployment/mobile-apps.md)
