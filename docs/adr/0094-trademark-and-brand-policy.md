---
status: Accepted
date: 2026-11-13
deciders: [Adrian]
tags: [open-source, legal, branding, self-hosting]
---

# ADR-0094 — Trademark and brand policy: open code, protected name

## Context and problem statement

Once the code is open ([ADR-0099](0099-apache-2-license.md)), anyone may build and
publish it. If a fork could publish an app called "CircleTools" or "Secret Santa · CircleTools" with our
logo, users could not tell the official apps (and the hosted service they connect to) from a copy. The
Apache-2.0 covers copyright and patents, not names and logos (its section 6 says so).

**What may others do with the CircleTools name, logos and store listings?**

## Decision drivers

- Users must be able to trust that an app called CircleTools is the official one.
- Self-hosting must stay easy: running the unmodified images under one's own domain must not raise any
  brand question.
- Keep the policy short and readable — a single page, not a legal treatise.

## Considered options

1. **Written trademark policy (Mozilla/Nextcloud style), name and logos reserved**
2. **No policy** — rely on goodwill
3. **Registered trademark** now, plus a policy

## Decision outcome

> **Accepted 2026-11-17 (owner)**, including the default of a written policy only — no registered
> trademark for now (cheaper; weaker in a dispute; Option 3 stays the follow-up if a real conflict appears).

**Chosen: Option 1 — a short written policy in the repo (`TRADEMARKS.md`)**, with registration
(Option 3) left as a follow-up if a real conflict appears.

The policy says:

- **Allowed without asking:** running the unmodified or modified software for yourself, your family,
  friends or organisation, and saying it is "based on CircleTools" or "compatible with CircleTools".
  A self-hosted instance may keep the name and logo **in the running app** as long as it is not offered
  to the public as a service that looks like ours.
- **Not allowed without permission:** publishing an app in an app store, or offering a public hosted
  service, under the CircleTools name or any app name of the suite, or with its logos; implying that a
  fork is official or endorsed.
- **Forks that publish** must rename and replace the logos. Brand assets are kept in one place so a fork
  can swap them in one step (see Follow-ups).

## Consequences

- **Positive:** clear line between "self-host freely" and "don't impersonate the official apps".
- **Negative:** an unregistered mark is weaker in a dispute. Accepted for now.
- **Follow-ups:** write `TRADEMARKS.md`; keep brand assets (logo files, app names, colours) in one
  documented location so a rebrand is a single change; link the policy from `README.md`,
  `CONTRIBUTING.md` and the self-hosting guide.
- **New risks:** none beyond the weaker protection noted above.

## Pros and cons of the options

### Written policy
- 👍 Cheap, clear, standard in open-source projects.
- 👎 Weaker without registration.

### No policy
- 👍 No effort.
- 👎 Look-alike apps in the stores would be hard to fight.

### Registration now
- 👍 Strongest protection.
- 👎 Cost and effort before there is any sign it is needed.

## Links

- Related ADRs: [ADR-0099](0099-apache-2-license.md) (license),
  [ADR-0057](0057-app-store-naming-and-localization.md) (store names),
  [ADR-0059](0059-brand-mark-ring-and-central-glyph.md) (brand mark)
- Naming: [`docs/process/naming.md`](../process/naming.md)
- Backlog: [Open source & self-hosting](../roadmap/backlog.md#open-source--self-hosting-2026-11-13) (OSS-6)
