---
id: FR-040
title: An admin link grants a shared, account-free, revocable admin capability
type: functional
status: Approved
priority: Must
source: UC-SS-018
verification: test
related: [UC-SS-018, FR-038, FR-039, FR-041, FR-043, NFR-009, ADR-0058, ADR-0017]
---

# FR-040 — An admin link grants a shared, account-free, revocable admin capability

## Description

A [shared-by-link group](../../glossary/shared-by-link-group.md) has an
[admin link](../../glossary/admin-link.md) that grants the **admin (organizer) capability** — manage
members and exclusions, set the budget, run and redo the [Draw](../../glossary/draw.md), and manage
sharing ([ADR-0058](../../adr/0058-account-free-link-shared-groups.md)). It behaves as follows:

- **It is a shared capability, not a single seat.** More than one co-organizer can hold the admin link
  at the same time; anyone holding it can act as admin.
- **It works only while no account owns the group.** In *Shared by link* mode the admin link is live; the
  moment an account **[claims admin](../../glossary/claim.md)** ([FR-043](FR-043-release-ownership-and-deletion-disposition.md)),
  the admin link is **retired** and a holder opening it sees *"&lt;name&gt; has claimed the admin role for
  this group."* Only the *admin* capability locks — the [join link](../../glossary/join-link.md) is
  unaffected and keeps working.
- **An admin can disable and regenerate it.** Disabling mints a fresh
  [share token](../../glossary/share-token.md) for the admin link; the old token stops resolving
  immediately (the "someone I didn't mean to has it" escape).
- **It is delivered through the existing share seam** — native share sheet where available, Copy on every
  platform, and a QR code on every platform.
- **Its account-free write path is bounded** by anti-abuse rules
  ([NFR-009](../non-functional/NFR-009-account-free-write-abuse-resistance.md)).

## Rationale

Running a group with other people should not require anyone to sign up, and it should not create a
single point of failure: a shared, revocable admin link lets a couple of family members co-organize,
and lets a mis-shared link be rotated away. Tying "the admin link only works while no account owns the
group" to the claim-admin transition gives a single, unambiguous owner the moment anyone decides to take
real ownership — no two people both believing they run the draw.

## Acceptance criteria

```gherkin
Given a shared-by-link group with an admin link
When two different people open that link
Then both can manage members, exclusions, budget and the draw

Given an admin holding the admin link
When they choose "disable & create a new admin link"
Then a new link works and the previous link no longer resolves

Given a shared-by-link group with an active admin link
When someone signs in and claims admin
Then the admin link is retired, a holder opening it sees "<name> has claimed the admin role for this group", and the join link still works

Given the admin link management screen
When an admin shares the link
Then Share (where available), Copy, and Show QR are all offered
```

## Dependencies & related

- Derived from: [UC-SS-018](../../use-cases/secret-santa/UC-SS-018-share-and-manage-as-admin.md)
- Related: [FR-038](FR-038-per-group-storage-mode.md) (modes),
  [FR-039](FR-039-shared-by-link-storage-and-sync.md) (storage),
  [FR-041](FR-041-join-link-and-person-claim.md) (join link, unaffected by claim-admin),
  [FR-043](FR-043-release-ownership-and-deletion-disposition.md) (claim-admin / release-admin),
  [NFR-009](../non-functional/NFR-009-account-free-write-abuse-resistance.md)
- Governing ADR: [ADR-0058](../../adr/0058-account-free-link-shared-groups.md); claim reuses
  [ADR-0017](../../adr/0017-anonymous-to-account-claim.md)
- Implemented by: [FEAT-013](../../features/FEAT-013-account-free-link-shared-groups.md)
- Glossary: [Admin link](../../glossary/admin-link.md), [Share token](../../glossary/share-token.md),
  [Claim](../../glossary/claim.md), [Shared-by-link group](../../glossary/shared-by-link-group.md),
  [Join link](../../glossary/join-link.md)
