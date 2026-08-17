---
id: FR-032
title: Add the organizer as the first member
type: functional
status: Implemented
priority: Should
source: UC-SS-001
verification: test
related: [UC-SS-001, UC-SS-010, UC-SS-011, UC-SS-012, FR-001, FR-028, FR-029, FR-030, NFR-001, ADR-0019, ADR-0020]
---

# FR-032 — Add the organizer as the first member

## Description

When a **signed-in** user creates a [Group](../../glossary/group.md), the system adds them as the
group's first [Member](../../glossary/member.md) and links that member to their own
[User](../../glossary/user.md) automatically — the organizer is a participant in the group they
create, without having to type their own name in like anyone else.

- The added member's name is the organizer's account **display name**.
- The self-link is established **without an invitation round-trip and without an address**: the
  organizer is both the requester and the target, so the same auto-accept a self-link already gets
  ([FR-028](FR-028-link-member-to-account.md)) applies. It confers exactly what any link confers —
  the member owns their [Wishlist](../../glossary/wishlist.md)
  ([FR-030](FR-030-edit-my-wishlist.md)) and, once the draw has run, may read **only their own**
  assignment ([FR-029](FR-029-see-my-assignment.md)) — and **no organizer rights beyond the ones
  they already hold as the group's creator**.
- The behaviour is **signed-in only**. The account-free/offline create path is **unchanged**: with
  no identity to add, the group starts empty ([NFR-001](../non-functional/NFR-001-local-first-offline.md)).
- It is **best-effort and degrades gracefully**. If the linking server is unreachable at create
  time, the organizer is still added as a member; the link is applied on a later sync. A failure to
  add or link never blocks group creation.
- The one-user-per-group guard still holds: the self-link is **idempotent** and never produces a
  second member for the same account.

## Rationale

The very first thing an organizer did after creating a group was add themselves — a chore the app
made them do manually even though it already knows who they are when signed in. Doing it for them
removes a step and makes "the group I created" also "a group I'm in", so the organizer's own
wishlist and assignment show up on the My-screens with no extra action.

Keeping it **signed-in only** respects the local-first design: there is no identity to add offline,
and inventing a placeholder member would be worse than an empty group. Reusing the existing
self-link path (rather than a new consent flow) is correct because **consent is trivially present** —
you cannot link someone else, only yourself — so the round-trip an ordinary link needs is exactly
the part a self-link is allowed to skip ([FR-028](FR-028-link-member-to-account.md),
[ADR-0019](../../adr/0019-member-user-linking.md)).

## Acceptance criteria

```gherkin
Given a signed-in user on the groups screen
When they create a new group
Then they are added as the group's first member
And that member is linked to their own account
And the member's name is their account display name

Given a signed-out visitor on the groups screen
When they create a new group
Then the group is created with no members

Given a signed-in user creating a group while the linking server is unreachable
When the group is created
Then they are still added as a member
And the account link is applied on a later sync
And group creation is not blocked

Given a signed-in user who creates a group
When the self-link is recorded more than once
Then they remain a member of the group exactly once
```

## Dependencies & related

- Derived from: [UC-SS-001](../../use-cases/secret-santa/UC-SS-001-create-group.md)
- Related: [FR-001](FR-001-create-secret-santa-group.md) (the create path this extends),
  [FR-028](FR-028-link-member-to-account.md) (the self-link it reuses),
  [FR-029](FR-029-see-my-assignment.md) and [FR-030](FR-030-edit-my-wishlist.md) (what the link
  unlocks), [NFR-001](../non-functional/NFR-001-local-first-offline.md) (why it is signed-in only)
- Governing ADRs: [ADR-0019](../../adr/0019-member-user-linking.md) (optional `UserId`, self-link),
  [ADR-0020](../../adr/0020-server-side-assignment-privacy.md) (what the link grants)
- Glossary: [Member](../../glossary/member.md), [User](../../glossary/user.md),
  [Group](../../glossary/group.md), [Wishlist](../../glossary/wishlist.md)
