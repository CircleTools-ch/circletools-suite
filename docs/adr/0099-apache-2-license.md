---
status: Accepted
date: 2026-11-13
deciders: [Adrian]
tags: [licensing, open-source, legal, mobile, self-hosting]
---

# ADR-0099 — License the suite under Apache-2.0 (supersedes ADR-0092)

## Context and problem statement

[ADR-0092](0092-open-source-license-and-contributions.md) chose **AGPL-3.0-or-later with a Contributor
License Agreement (CLA)** and was accepted and implemented the same day (2026-11-13). Putting the CLA into
practice showed what that choice costs a one-person project:

- the App Store's terms conflict with the GPL family, so every outside contribution needs a CLA that grants
  the right to ship it on other terms;
- the CLA is a contract contributors sign, so it needs either a lawyer-written standard text or a legal
  review of a custom one;
- CLA Assistant then needs a GitHub Gist, an organization link, a bot allowlist, and re-signing whenever the
  text changes.

The owner found this too complicated for what it protects. Nothing had been committed or published yet,
and the repository has had no outside contributors, so changing the license costs only the rework.

**Is there a license that keeps the suite genuinely open source and self-hostable, without the CLA
machinery?**

## Decision drivers

- Open source by the OSI definition; self-hosting with no conditions beyond the license.
- No CLA, no contributor paperwork, no legal review needed to accept a pull request.
- The official apps must stay shippable through Google Play and the App Store, whoever contributes.
- The hosted CircleTools service must stay recognizable as the official one.
- Simple enough for one maintainer to keep correct.

## Considered options

1. **Apache-2.0 for the whole repository**
2. **MIT for the whole repository**
3. **Keep AGPL-3.0 + CLA** (ADR-0092)

## Decision outcome

**Chosen: Option 1 — Apache-2.0 for the whole repository.**

- **Contributions need no CLA.** Apache-2.0 §5 says a contribution intentionally submitted to the project is
  under the Apache License unless stated otherwise. That is enough to ship it in every app, the stores
  included: Apache-2.0 has no conflict with store terms.
- **The brand is protected without the license's help.** Apache-2.0 §6 grants no trademark rights; the
  CircleTools name and logos stay reserved by [ADR-0094](0094-trademark-and-brand-policy.md). That, not
  copyleft, is what keeps the hosted service recognizable as the official one.
- **Over MIT:** the same freedoms, plus an explicit patent grant and patent-retaliation clause, a rule for
  contributions (§5), and the explicit trademark exclusion (§6).
- **The accepted cost:** anyone may run a modified, closed copy as a hosted service without publishing
  their changes — the protection AGPL §13 gave. For a free family app suite this risk is low, and the
  brand rule covers the part that matters (no look-alike "CircleTools" services or apps).

**In the repository:**

- `LICENSE` holds the Apache-2.0 text verbatim (from apache.org, so GitHub detects it); `NOTICE` holds the
  copyright line and the third-party components, as Apache-2.0 §4(d) expects.
- Every hand-written source file carries `SPDX-License-Identifier: Apache-2.0`; the build (IDE0073) and
  `SpdxHeaderTests` enforce it, as set up for ADR-0092.
- About & legal names the license and links it; the source link appears once the repository is public.
- `CONTRIBUTING.md` says contributions are under Apache-2.0. No CLA, no sign-off required.

## Consequences

- **Positive:** contributing is a pull request and nothing else; no store conflict ever; no CLA bot, gist
  or legal review; the license is widely understood and accepted by companies.
- **Negative:** closed hosted forks are allowed.
- **Follow-ups:** remove the CLA Assistant GitHub app from the `CircleTools-ch` organization (owner); delete
  the CLA page; keep `NOTICE` in step with `Directory.Packages.props`.
- **New risks:** none beyond the closed-fork cost above. If it ever becomes a real problem, relicensing
  future versions to a copyleft license would need the agreement of every contributor — the price of not
  having a CLA, accepted knowingly.

## Pros and cons of the options

### Apache-2.0
- 👍 No CLA needed; store-compatible; patent grant; explicit trademark exclusion.
- 👎 Allows closed hosted forks.

### MIT
- 👍 Shortest license; same freedoms.
- 👎 No patent grant, no contribution rule, no trademark clause.

### AGPL-3.0 + CLA
- 👍 Hosted forks must publish their changes.
- 👎 CLA, gist, bot and legal review for every contribution path — too heavy for this project.

## Links

- Supersedes: [ADR-0092](0092-open-source-license-and-contributions.md)
- Related ADRs: [ADR-0093](0093-publish-the-repository.md) (making the repository public),
  [ADR-0094](0094-trademark-and-brand-policy.md) (brand), [ADR-0030](0030-mobile-store-publisher-identity.md)
  (store publisher identity), [ADR-0095](0095-self-host-deployment-topology.md) (self-hosting)
- Backlog: [Open source & self-hosting](../roadmap/backlog.md#open-source--self-hosting-2026-11-13) (OSS-1)
