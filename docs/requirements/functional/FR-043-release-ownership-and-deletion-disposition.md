---
id: FR-043
title: Ownership is not a trap — release a group to shared-by-link, and dispose of owned groups on account deletion
type: functional
status: Draft
priority: Must
source: UC-SS-017, UC-MAIN-006
verification: test
related: [UC-SS-017, UC-SS-018, UC-MAIN-006, FR-038, FR-040, ADR-0058, ADR-0017, ADR-0022]
---

# FR-043 — Ownership is not a trap: release to shared-by-link, and dispose of owned groups on deletion

## Description

Two related capabilities keep account ownership of a [Group](../../glossary/group.md) reversible, so
taking a group into an account is never a one-way trap
([ADR-0058](../../adr/0058-account-free-link-shared-groups.md)).

**(a) Release-admin — My account → Shared by link.** The mirror of *claim-admin*
([FR-040](FR-040-admin-link-capability.md)). An account owner can hand a group back to a
[shared-by-link group](../../glossary/shared-by-link-group.md): the server copy is **re-keyed from the
user to a fresh [share token](../../glossary/share-token.md)**, a **new
[admin link](../../glossary/admin-link.md) is minted**, and the account **stops owning** it. Existing
[Assignments](../../glossary/assignment.md) and [person claims](../../glossary/person-claim.md) are
preserved (a device-only claim stays device-only; an account claim on *another* member is untouched).
Because no account then owns the group, the admin link is live again.

**(b) Account-deletion disposition.** Deleting an account
([ADR-0022](../../adr/0022-account-deletion-and-data-export.md)) must not silently strand the other
participants of a group the departing user owns. So deletion routes **each account-owned Secret-Santa
group** through a per-group choice — exactly mirroring ADR-0022's existing "a sole circle owner must
transfer or delete the circle first" rule:

- **Hand off as shared by link** — runs release-admin (a), so the group stays online with an admin link
  and everyone keeps access. **This is the default when the group has other joined people.**
- **Keep on this device** — the online copy is removed; the local copy survives
  ([ADR-0017](../../adr/0017-anonymous-to-account-claim.md)). **The default when the owner is the only
  participant.**
- **Delete the online copy** — the explicit destructive choice.

The deletion confirmation screen ADR-0022 already requires must **enumerate the account-owned shared
groups and their chosen disposition**, not merely say "your data will be deleted".

## Rationale

The shared-by-link tier introduces a group that other people depend on without having their own account.
Under the pre-existing rule, deleting the owning account would delete the server copy and cut off every
join-link participant. Adding **release-admin** and routing deletion through a per-group disposition —
defaulting to *hand off as shared by link* whenever others have joined — means a user can always leave,
downgrade, or delete their account **without** destroying a group other people are using. It reuses the
same claim/re-key machinery as claim-admin, so it is a small, symmetric addition.

## Acceptance criteria

```gherkin
Given an account-owned group with other joined participants
When the owner chooses "release to shared by link"
Then the group is re-keyed to a fresh share token with a new admin link
And the account no longer owns it, and assignments and person claims are preserved

Given a user deletes their account and owns a shared group others have joined
When the deletion flow runs
Then that group defaults to "hand off as shared by link" and the confirmation lists it and its disposition
And after deletion the group is still reachable by its admin and join links

Given a user deletes their account and owns a group with no other participants
When the deletion flow runs
Then that group defaults to "keep on this device" and its online copy is removed while the local copy survives

Given a user deletes their account and chooses "delete the online copy" for an owned group
When the deletion completes
Then the online copy is gone and any shared links for it stop resolving
```

## Dependencies & related

- Derived from: [UC-SS-017](../../use-cases/secret-santa/UC-SS-017-choose-and-switch-storage-mode.md),
  [UC-MAIN-006](../../use-cases/main/UC-MAIN-006-delete-account-and-export-data.md)
- Related: [FR-038](FR-038-per-group-storage-mode.md) (release-admin is one of the mode switches),
  [FR-040](FR-040-admin-link-capability.md) (claim-admin, which this reverses)
- Governing ADR: [ADR-0058](../../adr/0058-account-free-link-shared-groups.md), which **extends**
  [ADR-0022](../../adr/0022-account-deletion-and-data-export.md) (deletion flow) and reuses
  [ADR-0017](../../adr/0017-anonymous-to-account-claim.md) (claim/re-key shape)
- Implemented by: [FEAT-013](../../features/FEAT-013-account-free-link-shared-groups.md)
- Glossary: [Shared-by-link group](../../glossary/shared-by-link-group.md),
  [Share token](../../glossary/share-token.md), [Admin link](../../glossary/admin-link.md),
  [Claim](../../glossary/claim.md)
