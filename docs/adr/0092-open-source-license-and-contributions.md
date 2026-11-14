---
status: Superseded by ADR-0099
date: 2026-11-13
deciders: [Adrian]
tags: [licensing, open-source, legal, mobile, self-hosting]
---

# ADR-0092 — Open-source license (AGPL-3.0) and contributor terms

> **Superseded by [ADR-0099](0099-apache-2-license.md) (2026-11-13):** the suite is licensed under
> **Apache-2.0** instead. The CLA this ADR required proved too heavy for a one-person project. Kept for
> the record; nothing below is in force.

## Context and problem statement

The repository is private and its `LICENSE` is a placeholder ("all rights reserved"),
which says the final model is decided by ADR before any public distribution. The owner wants the whole
suite to be **open source and self-hostable** (see [ADR-0095](0095-self-host-deployment-topology.md)),
while CircleTools keeps running a **hosted service** and keeps shipping the official apps on Google Play
and the App Store.

**Which license do we publish under, and what do contributors agree to, so that the code stays open,
the hosted service stays viable, and the official apps can still go through the stores?**

## Decision drivers

- Self-hosters and contributors must get real freedoms (use, study, modify, run, share).
- Someone running a **modified** copy as a hosted service should not be able to keep the changes closed
  — the hosted CircleTools service competes on comfort, not on secret code.
- The official apps must stay shippable through **Google Play and the App Store**. The App Store terms add
  usage restrictions that conflict with the GPL family; this is only a non-issue when the publisher holds
  (or is granted) the rights to every line it ships.
- The project is maintained by one person: the setup must be simple and must not need a lawyer per PR.
- Dependencies (MIT / Apache-2.0: .NET, MudBlazor, …) must be compatible with the chosen license.

## Considered options

1. **AGPL-3.0-or-later for the whole repository + a contributor license agreement (CLA)**
2. **AGPL-3.0 for the server, MPL-2.0 or Apache-2.0 for the clients**
3. **Apache-2.0 (or MIT) for everything**, with a DCO sign-off
4. **Source-available license** (BUSL, Elastic-style)

## Decision outcome

**Chosen: Option 1 — AGPL-3.0-or-later for the whole repository, with a lightweight CLA.**

- **One license** for server, web and mobile code keeps the repo simple and stops a fork from
  closing any part of it, including a hosted variant (AGPL §13).
- The **CLA** has each contributor keep their copyright while granting CircleTools a broad license to
  their contribution, including the right to distribute it under other terms. That grant is what lets
  the official apps ship through the App Store, and it leaves the door open to relicensing later.
  It is enforced on pull requests by **CLA Assistant** (GitHub app installed on the organization
  2026-11-13; the agreement text is served from a GitHub Gist kept in step with
  the CLA page (removed with ADR-0099)), and documented in `CONTRIBUTING.md`.
- Every source file gets an `SPDX-License-Identifier: AGPL-3.0-or-later` header, checked by the build
  or CI so new files cannot be missed.
- The brand is **not** licensed by the AGPL — see [ADR-0094](0094-trademark-and-brand-policy.md).

Option 3 is the fallback if the CLA turns out to be a real barrier for contributors: it removes the
App Store question entirely, at the cost of allowing closed hosted forks.

## Consequences

- **Positive:** the code is genuinely open; a modified hosted copy must publish its source; the
  official store apps stay possible; self-hosters can run it with no conditions beyond the license.
- **Negative:** some companies avoid AGPL code, and a CLA puts off some contributors. Accepted: the
  audience is families, friends and self-hosters, not companies embedding the code.
- **Follow-ups:**
  - Replace `LICENSE` with the AGPL-3.0 text; add a `NOTICE` listing third-party licenses.
  - Add SPDX headers to every source file and a CI check for them.
  - Set up the CLA bot and describe it in `CONTRIBUTING.md`.
  - The web app and the apps show the license and a link to the source on "About & legal"
    (AGPL §13 asks for a way for network users to get the source).
- **New risks:** a mistake in the CLA wording weakens the App Store path — use a well-known template
  (e.g. Apache ICLA-style or the Contributor Agreements generator) rather than a home-made text.

## Pros and cons of the options

### AGPL-3.0 everywhere + CLA
- 👍 Strongest protection of openness, including hosted forks; one license to understand.
- 👍 CLA keeps the store path and relicensing open.
- 👎 CLA friction; AGPL is avoided by some organisations.

### AGPL server, permissive clients
- 👍 Clients are easy to reuse and ship through stores without a CLA.
- 👎 Two licenses in one repo; shared UI building blocks sit in both halves, so the line is blurry.

### Apache-2.0 / MIT everywhere
- 👍 Simplest; no store conflict; no CLA (a DCO sign-off is enough); patent grant with Apache-2.0.
- 👎 Anyone may run a closed, modified hosted copy.

### Source-available (BUSL etc.)
- 👍 Most control over commercial use.
- 👎 Not open source by the OSI definition — contradicts the goal.

## Links

- Related ADRs: [ADR-0093](0093-publish-the-repository.md) (making the repository public),
  [ADR-0094](0094-trademark-and-brand-policy.md) (brand), [ADR-0030](0030-mobile-store-publisher-identity.md)
  (store publisher identity), [ADR-0095](0095-self-host-deployment-topology.md) (self-hosting)
- Backlog: [Open source & self-hosting](../roadmap/backlog.md#open-source--self-hosting-2026-11-13) (OSS-1)
