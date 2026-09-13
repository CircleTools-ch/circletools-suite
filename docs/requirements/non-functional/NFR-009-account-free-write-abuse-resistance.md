---
id: NFR-009
title: Account-free backend writes are abuse-resistant
type: non-functional
status: Draft
priority: Must
source: FR-039, FR-040
verification: test
related: [FR-039, FR-040, NFR-010, ADR-0058, ADR-0050]
---

# NFR-009 — Account-free backend writes are abuse-resistant

## Description

The [shared-by-link](../../glossary/shared-by-link-group.md) storage mode adds the system's first
**account-free write path to the server**: anyone can create a group and, holding a link, push changes
to it, with **no identity to hold accountable** ([ADR-0058](../../adr/0058-account-free-link-shared-groups.md)).
That path must be resistant to spam, storage exhaustion, and enumeration:

- **Unguessable [share tokens](../../glossary/share-token.md).** A token carries **≥128 bits** of
  cryptographic entropy, so admin/join links cannot be guessed or brute-forced; a wrong token is
  indistinguishable from a non-existent one (no oracle).
- **Rate limiting.** Group **creation** and **writes** are rate-limited per client/IP so the endpoint
  cannot be used to mass-create or mass-mutate groups.
- **Bounded size.** Each shared group aggregate is size-capped — a bounded number of members and
  occasions, and bounded per-field lengths (wishlist entries, gift notes, names) — and oversized or
  malformed payloads are **rejected**, not stored.
- **No amplification.** A single request cannot fan out into unbounded server work or storage.

This complements retention/expiry ([NFR-010](NFR-010-shared-group-retention-lifecycle.md)) and does not
weaken the on-this-device default, which touches no server at all
([NFR-001](NFR-001-local-first-offline.md)).

## Rationale

Every other write path in the product is gated by an authenticated account. Opening an account-free
write path is what makes the middle tier valuable, but it is also a spam and cost surface an operator at
hobby scale cannot babysit. Entropy, rate limits and hard size caps are the cheap, testable controls
that keep the surface bounded without adding accounts.

## Acceptance criteria

```gherkin
Given a share token
When it is inspected
Then it carries at least 128 bits of entropy and reveals nothing about the group

Given a wrong or expired share token
When it is used
Then the response is indistinguishable from a token that never existed (no enumeration oracle)

Given repeated group-creation requests from one client beyond the configured limit
When the limit is exceeded
Then further creations are throttled or rejected

Given a write that exceeds the per-group size caps (members, occasions, or field lengths)
When it is submitted
Then it is rejected and nothing is stored
```

## Quality attribute *(NFR only)*

- **Attribute:** security / abuse-resistance.
- **Metric & target:**
  - share-token entropy **≥ 128 bits**; **zero** successful guesses in fuzz/enumeration tests;
  - group-create and write requests **rate-limited** to a configured ceiling per client/IP (0 breaches
    of the ceiling in load tests);
  - **100%** of over-cap payloads rejected before storage; per-group storage bounded to the configured
    maximum.
- **Measurement approach:** unit/property tests on token generation entropy; API tests asserting equal
  responses for wrong vs. non-existent tokens; integration tests driving create/write past the limits
  and asserting throttling and rejection; a size-cap test matrix. Secrets/token generation follows
  [ADR-0050](../../adr/0050-secrets-management.md).

## Dependencies & related

- Derived from: [FR-039](../functional/FR-039-shared-by-link-storage-and-sync.md),
  [FR-040](../functional/FR-040-admin-link-capability.md)
- Related: [NFR-010](NFR-010-shared-group-retention-lifecycle.md) (retention/expiry),
  [NFR-001](NFR-001-local-first-offline.md) (the default path stays server-free)
- Governing ADR: [ADR-0058](../../adr/0058-account-free-link-shared-groups.md),
  [ADR-0050](../../adr/0050-secrets-management.md) (token generation)
- Verified by: [FEAT-013](../../features/FEAT-013-account-free-link-shared-groups.md) /
  [TP-013](../../features/TP-013-account-free-link-shared-groups.md)
- Glossary: [Share token](../../glossary/share-token.md),
  [Shared-by-link group](../../glossary/shared-by-link-group.md)
