---
id: FR-014
title: Export personal data
type: functional
status: Draft
priority: Must
source: UC-MAIN-006
verification: test
related: [UC-MAIN-006, NFR-002, NFR-004, ADR-0020, ADR-0022]
---

# FR-014 — Export personal data

## Description

A signed-in [User](../../glossary/user.md) must be able to obtain a **machine-readable (JSON)** export
of their data. The export contains exactly what the user can already see in the app — profile, circle
memberships, [Groups](../../glossary/group.md) they own (including the member names and
[Wishlists](../../glossary/wishlist.md) visible to them within those groups), their own wishlist
entries, and the [Assignments](../../glossary/assignment.md) they are entitled to see.

**The export endpoint must reuse the [ADR-0020](../../adr/0020-server-side-assignment-privacy.md)
authorization boundary rather than defining its own.** It must never return data the user could not
read through the UI.

Small exports are delivered as a direct download; large ones are generated asynchronously and
delivered by a **short-lived, single-use link** — because deletion is immediate
([FR-015](FR-015-delete-account.md)), an emailed archive can otherwise outlive the account it
describes.

## Rationale

Once the suite stores personal data server-side, export is a legal obligation (revDSG / GDPR), not a
feature. Mirroring in-app visibility is the decisive design choice from
[ADR-0022](../../adr/0022-account-deletion-and-data-export.md): a parallel "what belongs in an export"
rule would become a second place for the assignment-privacy rule to be got wrong, and it would drift
from the first. Reusing the authorization boundary means the export is covered by the same tests that
protect the UI.

## Acceptance criteria

```gherkin
Given a signed-in user
When they request a data export
Then they receive a JSON archive containing their profile, circle memberships,
     groups they own, their own wishlist entries and the assignments they may see

Given a user who is a member but not the organizer of a synced group
When their export is generated
Then it contains their own assignment only
And it contains no other member's assignment

Given a user with no groups and no circle memberships
When they request an export
Then a valid archive containing their profile is produced

Given an export archive delivered by link
When the link is used a second time, or after its lifetime has elapsed
Then it is rejected

Given a user requests an export
When the archive is large enough to require asynchronous generation
Then the user is told it is being prepared rather than left waiting
And they are notified when it is ready

Given any data the requesting user could not read through the UI
When an export is generated
Then that data is absent from the archive
```

## Dependencies & related

- Derived from: [UC-MAIN-006](../../use-cases/main/UC-MAIN-006-delete-account-and-export-data.md)
- Related: [FR-015](FR-015-delete-account.md),
  [NFR-002](../non-functional/NFR-002-assignment-privacy.md),
  [NFR-004](../non-functional/NFR-004-data-protection-and-retention.md)
- Governing ADRs: [ADR-0022](../../adr/0022-account-deletion-and-data-export.md),
  [ADR-0020](../../adr/0020-server-side-assignment-privacy.md)
- Glossary: [User](../../glossary/user.md), [Assignment](../../glossary/assignment.md),
  [Wishlist](../../glossary/wishlist.md), [Group](../../glossary/group.md)
