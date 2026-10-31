---
status: Accepted
date: 2026-10-31
deciders: [Adrian]
tags: [api, compatibility, mobile, release, deployment]
---

# ADR-0090 — The backend stays compatible with every released app

## Context and problem statement

The server (API, database, background jobs) is deployed by hand whenever it is ready
([ADR-0038](0038-ci-cd-pipeline.md)), while the Android and iOS apps reach users through store reviews and
updates people install when they like. So a new backend always meets **older apps** — on 2026-10-31,
version 1.0 was in Apple's review while the backend had already moved on.
[ADR-0010](0010-api-style-and-versioning.md) set the intent ("must evolve without breaking installed apps")
but left the concrete rule as a follow-up. A review of the legal-page changes found a case that was
compatible on paper but could still lose data: a stale push from an older copy of a group deleted entries of a
member the copy did not know yet.

**What exactly must a new backend guarantee to apps already out there?**

## Decision drivers

- No forced update exists: an installed app can stay on an old version for months.
- Users must never lose data because their app is older than the server.
- Store reviews take days, so server and app releases cannot be made atomic.

## Considered options

1. **Compatible with every released app version** — any app ever shipped through a store keeps working.
2. **Compatible with the last N versions** — older apps may break.
3. **Minimum-version gate** — the server refuses apps below a version and asks for an update.

## Decision outcome

**Chosen: Option 1** while there is no minimum-version gate. Every backend change must keep working with
**every app version released through a store** (Google Play incl. the closed test, App Store, TestFlight), and
the web app counts as the same release as the backend it ships with. Concretely:

- **API contract:** existing endpoints, fields, status codes and `ProblemDetails` codes keep their meaning. Add,
  don't change or remove; a breaking change needs a new API version (`/api/v2`, ADR-0010) with v1 kept.
- **Documents and data:** the server reads and keeps documents written by older apps (the group document's
  `schemaVersion`), and never writes something an older app cannot read.
- **Behaviour:** a request from an older app must not lead to data loss it would not have caused before —
  stale pushes included (last write wins must never become *delete for good*).
- **Every change touching the server** is checked against the oldest released app before it is deployed: the
  question "what happens when the oldest app does this?" is part of the review, and a test covers any new
  server-side deletion or rejection path.

Option 3 is the way out if Option 1 ever becomes too costly; it is not built yet.

## Consequences

- **Positive:** users can update whenever they like; server deploys never wait for store reviews.
- **Negative:** old behaviour has to be carried along; some clean-ups must wait until a version gate exists.
- **Follow-ups:** a "minimum supported app version" gate (Option 3), once old versions become a burden;
  record the oldest released app version per platform in the release notes / store docs.
- **New risks:** a compatibility slip is only found when an old app hits it — hence the review question and the
  tests above.

## Pros and cons of the options

### Compatible with every released version
- 👍 No user is ever broken or loses data; deploys are independent of stores.
- 👎 Old paths live on.

### Last N versions
- 👍 Less to carry.
- 👎 Without a gate, the oldest apps break silently — the thing this ADR exists to prevent.

### Minimum-version gate
- 👍 Lets old paths be removed cleanly.
- 👎 Needs app + server work and an update prompt; not built.

## Links

- Related ADRs: [ADR-0010](0010-api-style-and-versioning.md) (API versioning — this is its compatibility policy),
  [ADR-0016](0016-sync-and-conflict-resolution.md) (last write wins),
  [ADR-0038](0038-ci-cd-pipeline.md) (manual deploy),
  [ADR-0047](0047-versioning-and-release-strategy.md) (versions),
  [ADR-0052](0052-member-archival-and-permanent-draw-history.md) (member removal, amended 2026-10-31)
- Definition of Done: "Server changes" section
