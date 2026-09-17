---
status: Superseded by ADR-0065
date: 2026-07-29
deciders: [Adrian]
tags: [deployment, mobile, ios, maui, tooling, build]
---

# ADR-0031 — iOS build host

> **Superseded by [ADR-0065](0065-hosted-macos-ios-builds.md) (2026-09-17).** The project now builds
> iOS on **GitHub-hosted macOS runners** rather than buying an Apple-silicon Mac, because iOS moved onto
> the launch critical path and hosted builds cost ~CHF 0 at this release cadence with no hardware outlay.
> The cost/durability analysis below is **preserved as the documented upgrade trigger**: buy the Mac if
> build frequency, a hosted-image SDK lag, or interactive-debugging needs make it the cheaper/safer
> choice — see ADR-0065 → "Upgrade trigger".

## Context and problem statement

The suite promises native iOS alongside Android and Web
([arc42 ch. 1](../arc42/01-introduction-and-goals.md), quality goal Q2). The MAUI head
`CircleTools.SecretSanta.App` already multi-targets `net10.0-ios`, but only on macOS — the csproj
scopes the iOS and Mac Catalyst target frameworks behind `IsOSPlatform('osx')` precisely because
they cannot be built anywhere else.

That is not a MAUI limitation and there is no way around it: compiling, signing and packaging an iOS
app requires Apple's toolchain, which runs only on macOS. Visual Studio on Windows can *drive* an
iOS build, but only by delegating it to a Mac over the network (**Pair to Mac**). The one feature
that sidestepped a Mac for debug deployment, **Hot Restart**, was removed in Visual Studio 2026 and
survives only in Visual Studio 2022 17.14 — it never produced a store-shippable artifact anyway.

The available hardware is a Windows PC, an Android phone, an iPad, and a **Intel-based MacBook Pro**
belonging to Adrian's wife. The Intel machine is where the decision gets sharp, because the version
chain is unforgiving:

- .NET 10's iOS workload requires **Xcode 26.x**.
- Xcode 26.4 and later require **macOS 26.2 (Tahoe)**.
- macOS 26 Tahoe supports exactly **two** Intel MacBook Pro models — the **16-inch 2019** and the
  **13-inch 2020 with four Thunderbolt 3 ports**. Every other 2018–2020 MacBook Pro is already
  excluded, despite having run macOS 15 Sequoia fine.
- macOS 26 is the **last** macOS to support Intel at all. macOS 27 (autumn 2026) is Apple silicon
  only.
- Apple raises the mandatory build SDK every spring — the iOS 26 SDK has been required for all
  submissions since **28 April 2026**. The next bump will require Xcode 27, which will require
  macOS 27, which will not run on Intel.

**What produces iOS artifacts, this year and in three years?**

## Decision drivers

- Q2 cross-platform feature parity: iOS is not optional, so its build path cannot be a hope.
- The Mac is a **shared family machine**, not project infrastructure. Installing Xcode (tens of GB),
  upgrading macOS and leaving it awake for Pair to Mac sessions all impose on someone else.
- TestFlight is the only realistic way to get builds onto the iPad, and it needs a real signed build.
- Whatever is chosen should be able to become the CI runner when the CI/CD ADR lands, rather than
  being thrown away.
- Cost proportional to a hobby project.

## Considered options

1. **Buy an Apple-silicon Mac** (Mac mini) as a dedicated build host.
2. **Use the wife's Intel MacBook Pro.**
3. **Rent a cloud Mac** (hosted macOS CI or a leased Mac mini).

## Decision outcome

**Chosen: option 1 — an Apple-silicon Mac mini as the durable iOS build host**, with option 2 as a
**conditional short bridge** and option 3 as the documented fallback.

The Intel MacBook Pro is usable *only if* it is one of the two models above. The check takes ten
seconds: **Apple menu → About This Mac** → confirm the model year, and for the 13-inch, count the
Thunderbolt ports. If it is not a 16-inch 2019 or a four-port 13-inch 2020, the iOS story on that
machine is over today, not next year.

Even if it qualifies, it buys roughly one release cycle. Apple's spring 2027 SDK requirement will
demand a macOS the machine cannot run, and at that point every iOS update stops — including security
fixes and forced platform-compliance updates
([NFR-007](../requirements/non-functional/NFR-007-store-distributability.md)). Treating it as the
plan would mean discovering the wall at the worst possible moment: when an update is already due.

A base Mac mini (M-series, ~CHF 650–800) removes the whole problem for years, is a machine the
project owns rather than borrows, and can later sit on a shelf as a self-hosted CI runner. Against
the recurring alternatives it also wins on arithmetic: hosted macOS CI runs about **USD 0.062 per
minute**, and on GitHub Actions macOS minutes drain any included allowance at a **10× multiplier**,
so a habit of building on every push reaches the price of the hardware quickly.

The Windows PC stays the primary development machine. Pair to Mac connects it to whichever Mac is
acting as host, so day-to-day work does not move.

## Consequences

- **Positive:** iOS stops being a blocked dependency. Simulator testing, device deployment,
  TestFlight and store submission all become possible on demand, without negotiating for someone
  else's laptop.
- **Positive:** the iPad becomes testable — via the simulator for layout and via TestFlight for the
  real thing.
- **Positive:** the eventual CI/CD ADR gains a real option (self-hosted macOS runner) instead of
  being forced onto per-minute hosted macOS.
- **Negative / accepted:** ~CHF 650–800 up front, for a project with no revenue. This is the single
  largest cost in the whole shipping story — larger than both store fees combined for several years.
- **Negative / accepted:** one more machine to keep updated. Xcode and macOS updates become a
  recurring chore with hard external deadlines attached.
- **Follow-ups:**
  - Verify the MacBook Pro model before assuming any bridge exists at all.
  - Record the Xcode/macOS floor in the [mobile runbook](../deployment/mobile-apps.md) and re-check
    it at every .NET and Xcode bump.
  - Revisit as input to the open **CI/CD** decision in the
    [ADR backlog](index.md#open-decisions-backlog).
- **New risks:** risk **R6** in [arc42 ch. 11](../arc42/11-risks-and-technical-debt.md) — if the
  purchase is deferred and the Intel bridge is used instead, iOS updates acquire a hard expiry date
  set by Apple, not by us.

## Pros and cons of the options

### 1. Own Apple-silicon Mac (chosen)
- 👍 Durable — no expiry date imposed by Apple's hardware transition.
- 👍 Project-owned; always available; can become the CI runner.
- 👍 Fast builds; no per-minute meter running.
- 👎 The largest single up-front cost of the whole shipping effort.
- 👎 Another machine to maintain.

### 2. Wife's Intel MacBook Pro
- 👍 Free, and available today — *if* it is one of the two supported models.
- 👎 Dies at Apple's next SDK deadline; macOS 27 drops Intel entirely.
- 👎 Most 2018–2020 MacBook Pros cannot even run macOS 26, so this may already be impossible.
- 👎 Imposes Xcode, an OS upgrade and build sessions on a shared family machine.

### 3. Cloud / hosted Mac
- 👍 No hardware to buy or maintain; always a current macOS and Xcode.
- 👍 The natural answer once CI exists.
- 👎 Per-minute cost that never stops (≈ USD 0.062/min hosted, and a 10× multiplier against included
  CI minutes); overtakes the hardware cost with regular use.
- 👎 Awkward for interactive work — simulator testing and signing debugging over a remote session are
  markedly worse than local.
- 👎 Provisioning certificates and keys have to live on someone else's machine.

## Links

- Related ADRs: [ADR-0030](0030-mobile-store-publisher-identity.md),
  [ADR-0023](0023-local-dev-infrastructure-in-apphost.md)
- Requirements: [NFR-007](../requirements/non-functional/NFR-007-store-distributability.md)
- arc42: [07 Deployment view](../arc42/07-deployment-view.md),
  [11 Risks and technical debt](../arc42/11-risks-and-technical-debt.md),
  [02 Constraints](../arc42/02-constraints.md)
- Runbooks: [device testing](../deployment/device-testing.md),
  [mobile apps](../deployment/mobile-apps.md)
- Open decision: CI/CD pipeline — [ADR backlog](index.md#open-decisions-backlog)
