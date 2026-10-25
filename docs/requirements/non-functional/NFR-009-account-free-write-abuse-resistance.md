---
id: NFR-009
title: Writes without an account are abuse-resistant
type: non-functional
status: Implemented
priority: Must
source: FR-039, FR-040
verification: test
related: [FR-039, FR-040, FR-041, FR-050, NFR-010, ADR-0086, ADR-0050]
---

# NFR-009 — Writes without an account are abuse-resistant

> **Rewritten 2026-10-25 for [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)** ([FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)): one way to be in a group. The previous version described the four-kind storage model (on this device, shared by link, my account, my account · shared) and is in the git history.

## Description

Online groups can be created and changed **without an account** — by the [organizer link](../../glossary/organizer-link.md),
a [device key](../../glossary/device-key.md) or a reveal link ([ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)). Those writes have no identity behind them and must
resist spam, storage exhaustion and guessing:

- **Unguessable secrets.** Organizer tokens, join tokens, device keys and reveal tokens carry **≥128 bits** of
  entropy and are stored only as hashes. A wrong secret gets the same answer as a group that does not exist.
- **Rate limiting.** Every route usable without an account is limited per IP (120 requests a minute by default,
  `RateLimiting:SharedWritePermitLimit`).
- **Bounded size.** A group's document is refused above **256 KiB** or **200 members**
  (`SharedGroups:MaxBodyBytes`, `SharedGroups:MaxMembers`); a member's own entry is bounded per field (gift note
  500 characters, bounded wishlists). Oversized or malformed input is refused, not stored.
- **No amplification.** A single request cannot fan out into unbounded server work or storage.

This complements retention ([NFR-010](NFR-010-shared-group-retention-lifecycle.md)) and does not touch groups on
this device, which never reach a server ([NFR-001](NFR-001-local-first-offline.md)).

## Rationale

Organizing and taking part without an account are part of the product's promise, and they open a write path a
hobby-scale operator cannot watch around the clock. Entropy, rate limits and hard size caps are cheap, testable
controls that keep it bounded.

## Acceptance criteria

```gherkin
Given a secret issued for an online group
When it is inspected
Then it carries at least 128 bits of entropy and is stored only as a hash

Given a wrong organizer token, device key or reveal token
When it is used
Then the answer is the same as for a group that does not exist

Given one IP sending more requests than the limit within a minute
When it sends one more to a route usable without an account
Then the request is refused with 429

Given a document with more than 200 members or over 256 KiB
When it is created or pushed
Then it is refused and nothing is stored
```

## Quality attribute *(NFR only)*

- **Attribute:** security / availability.
- **Metric & target:** ≥128-bit secrets; per-IP limit enforced on every account-free route; zero documents stored
  above the size limits.
- **Measurement approach:** integration tests (limits refused on create and push, rate limit answered with 429);
  review of the token factory.

## Dependencies & related

- Related: [FR-039](../functional/FR-039-shared-by-link-storage-and-sync.md), [FR-040](../functional/FR-040-admin-link-capability.md),
  [FR-041](../functional/FR-041-join-link-and-person-claim.md), [FR-050](../functional/FR-050-edit-own-entry-from-reveal-link.md),
  [NFR-010](NFR-010-shared-group-retention-lifecycle.md)
- Governing ADRs: [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md), [ADR-0050](../../adr/0050-secrets-management.md)
- Implemented by: [FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)
