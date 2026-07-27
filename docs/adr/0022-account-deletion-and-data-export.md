---
status: Accepted
date: 2026-07-27
deciders: [Adrian]
tags: [architecture, privacy, identity, legal, data-protection]
---

# ADR-0022 — Account deletion & data export model

## Context and problem statement

v2 stores personal data server-side for the first time — display name, email, circle memberships and
[Assignments](../glossary/assignment.md) protected by authorization rather than by absence
([ADR-0020](0020-server-side-assignment-privacy.md)). That makes **deletion and export legally
non-optional** (revDSG / GDPR), which the v2 brainstorming already recognised when it selected the
item on exactly those grounds.

Two questions have to be answered before [UC-MAIN-006](../use-cases/main/UC-MAIN-006-delete-account-and-export-data.md)
can become requirements:

1. **Is deletion immediate, or is there a recovery window?**
2. **What counts as "the user's data" in an export**, when the user may own a
   [Group](../glossary/group.md) full of other people's wishlists?

## Decision drivers

- **Deletion must not be a device wipe.** [ADR-0017](0017-anonymous-to-account-claim.md) established
  that local data survives signing out; an account that is "additive" on the way in must not become
  destructive on the way out.
- **An export must not become a privacy hole.** It is a bulk read of exactly the data
  [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md) protects — the easiest place
  in the system to hand someone a completed draw they were never meant to see.
- **A completed draw is history** and stays reproducible even as people leave
  ([ADR-0018](0018-circle-and-sharing-model.md), snapshot-at-draw).
- **One person's deletion may not destroy another person's data.** Groups other people own must
  survive.
- Hobby scale: no ops team to run and monitor a deferred-purge pipeline.

## Considered options

**Deletion timing**

1. **Immediate hard delete** on confirmation + re-authentication.
2. **30-day grace period** — soft-delete state, sign-in restores, scheduled purge.
3. **7-day grace period** — the same machinery, tighter window.

**Export scope**

1. **Mirror in-app visibility** — export exactly what the user can already see.
2. **Strictly personal to them** — profile, memberships, own wishlist, own assignment only.
3. **Everything they own, in full** — complete contents of every group they created.

## Decision outcome

**Chosen: immediate hard delete (deletion option 1) + export mirrors in-app visibility (export
option 1).**

### Deletion is immediate

On confirmation and recent re-authentication, the account, its credentials, its passkeys and every
session are deleted, and server-side data attributable solely to the user goes with them. There is no
soft-delete state and no recoverable window.

This is defensible here specifically **because of ADR-0017**: the user's local device copy is
untouched by deletion. The blast radius of a mistaken deletion is "lost sync, lost circle
memberships" — not "lost data". A grace period would buy protection against a loss that the
local-first architecture already prevents, and would cost a lifecycle state that every query, every
uniqueness check and every invitation lookup then has to reason about.

Three rules survive deletion, and they are part of this decision:

- **The `UserId` link is removed from [Members](../glossary/member.md), the member is not.** A member
  in someone else's group is that organizer's data ([ADR-0019](0019-member-user-linking.md)).
- **Completed draws are not rewritten.** History stands; already-sent
  [reveal links](../glossary/reveal-link.md) keep working.
- **A sole circle owner must transfer ownership or delete the circle first.** Deletion may not
  silently orphan a circle. The transfer UI is M2, but the *rule* is M1's, because M1 ships deletion.

### Export mirrors in-app visibility

The export contains what the user could already read in the app: profile, circle memberships, groups
they own (including the member names and wishlists visible to them within those groups), their own
[Wishlist](../glossary/wishlist.md) entries, and the assignments they are entitled to see.

The decisive property is that **the export endpoint reuses the ADR-0020 authorization boundary rather
than defining a second one.** A parallel "what's in an export" rule would be a second place for the
assignment-privacy rule to be got wrong, and it would drift. Delivered as JSON, synchronously when
small, by emailed link when large.

## Consequences

- **Positive:** no lifecycle state to thread through the data model; the legal obligation is met
  plainly; the export cannot leak more than the UI already does, and is testable with the same
  authorization tests; the local-first promise holds in both directions — signing up doesn't take
  your data, and deleting doesn't destroy it.
- **Negative / accepted — a mistaken deletion is final server-side.** No undo. Mitigated by requiring
  re-authentication and by a confirmation screen that states precisely what is and is not deleted, but
  not eliminated. Accepted because the local copy survives.
- **Negative / accepted — deletion is blocked behind circle ownership transfer.** A user who wants to
  leave *right now* may first have to hand over a circle. This is friction at the worst moment, and it
  is preferred to destroying other people's groups or orphaning them.
- **Negative / accepted — the export is only as good as the authorization layer.** Reusing that
  boundary is the point, but it also means a bug there is now a bulk-disclosure bug rather than a
  single-page one. The export needs its own authorization tests, not just a "does it produce a file"
  test.
- **Follow-ups:**
  - **A data-protection & retention NFR does not exist** — NFR-001..003 cover offline, assignment
    privacy and multilingual only. Add **NFR-004 (data protection & retention)** as the first task of
    the M1 requirements step, covering this ADR, the 30-day purge of unverified pending accounts, and
    Swiss residency once hosting is decided.
  - Pin the export's JSON shape in the M1 feature spec.
  - The **managed-member** question — what happens to [Managed members](../glossary/managed-member.md)
    created by a departing user — is deferred to **M2**, where managed members are actually built. The
    principle set here: a managed member is circle data, so it survives with the circle.
- **New risks:** hard deletion plus an emailed export link means the archive can outlive the account;
  export links must be short-lived and single-use.

## Pros and cons of the options

### Deletion 1 — Immediate hard delete (chosen)
- 👍 No soft-delete state anywhere; honest privacy story; nothing to purge later.
- 👎 No undo server-side.

### Deletion 2/3 — Grace period (30 or 7 days)
- 👍 Protects against the accidental or angry click; the industry norm.
- 👎 Adds a lifecycle state to every query and uniqueness check; retains personal data after the user
  asked for deletion; needs a scheduled purge job nobody is on call for.

### Export 1 — Mirror in-app visibility (chosen)
- 👍 One authorization boundary, reused; cannot leak more than the UI; genuinely useful to the user.
- 👎 Makes the export a bulk read through the most privacy-sensitive path in the system.

### Export 2 — Strictly personal
- 👍 Smallest possible disclosure surface.
- 👎 An organizer of five exchanges gets a near-empty file that misrepresents what the account holds.

### Export 3 — Everything owned, in full
- 👍 Doubles as a backup.
- 👎 Ignores per-item visibility — the most likely way to hand someone a draw they should not see.

## Links

- Related ADRs: [ADR-0017](0017-anonymous-to-account-claim.md) (local data survives — the premise for
  immediate deletion), [ADR-0018](0018-circle-and-sharing-model.md) (circle roles, snapshot-at-draw),
  [ADR-0019](0019-member-user-linking.md) (`UserId` unlink), [ADR-0020](0020-server-side-assignment-privacy.md)
  (the authorization boundary the export reuses), [ADR-0009](0009-authentication-and-passkeys.md)
- arc42: [08 Cross-cutting concepts](../arc42/08-crosscutting-concepts.md) §8.8
- Guideline: [security & privacy guidelines](../guidelines/security-privacy-guidelines.md)
- Use case: [UC-MAIN-006](../use-cases/main/UC-MAIN-006-delete-account-and-export-data.md)
- Requirements: [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md); NFR-004
  (data protection & retention) proposed above
- Source: [brainstorming — accounts, families & sync (v2)](../brainstorming/2026-07-24-accounts-families-sync.md), theme A / slice M1
