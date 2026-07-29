---
id: UC-MAIN-014
title: Delete a circle
app: Main
actors: [Circle owner]
status: Approved
priority: Must
related: [FR-022, FR-015, NFR-004, NFR-006, ADR-0018, ADR-0022]
---

# UC-MAIN-014 — Delete a circle

## Summary / goal

The owner deletes a [Circle](../../glossary/circle.md) that has served its purpose — and the suite is
explicit about what disappears with it, because a circle holds data about **other people**, including
[Managed members](../../glossary/managed-member.md) who cannot object.

## Primary actor & stakeholders

- **Primary actor:** the circle **owner**.
- **Stakeholders & interests:** every other member, who loses the circle without being asked; managed
  members, whose profiles exist nowhere else; the operator, for whom this is a data-retention
  obligation ([NFR-004](../../requirements/non-functional/NFR-004-data-protection-and-retention.md)).

## Preconditions

- The actor is signed in, verified, and is the circle's **owner**.
- The device is online.

## Postconditions

- **Success:** the circle, all its memberships, all its managed-member profiles and all its pending
  [Invitations](../../glossary/invitation.md) are gone. User accounts and Secret-Santa data are
  untouched.
- **Failure:** the circle is exactly as it was. There is no partially deleted circle.

## Trigger

The owner selects **Delete this circle** in circle settings — or reaches it from
[UC-MAIN-013](UC-MAIN-013-leave-circle.md) as the only way out of a circle they alone are in, or from
account deletion ([UC-MAIN-006](UC-MAIN-006-delete-account-and-export-data.md)).

## Main success scenario

1. Owner selects **Delete this circle**.
2. System shows precisely what will happen — the same two-column honesty account deletion uses
   ([ADR-0022](../../adr/0022-account-deletion-and-data-export.md)):
   - **Deleted:** the circle, its name and label, every membership, every managed-member profile, every
     pending invitation.
   - **Not deleted:** anyone's user account; Secret-Santa groups created from this circle, their
     members, their completed [Draws](../../glossary/draw.md) and their already-issued
     [reveal links](../../glossary/reveal-link.md); anything on anyone's device.
3. Owner confirms explicitly — the confirmation names the circle, so a mis-tap on the wrong circle in a
   list cannot destroy it.
4. System deletes the circle and everything listed, in one transaction, and returns the owner to their
   circle list.

## Extensions / alternate flows

- **2a. The circle has other members** → they are **not** asked. Ownership is exactly the right to do
  this ([ADR-0018](../../adr/0018-circle-and-sharing-model.md)), and a vote would give a departed or
  inactive member a veto. The confirmation says how many people will lose it.
- **2b. Managed members exist** → they are named in the confirmation, not merely counted. Their
  profiles exist only here, so this is the one deletion with no copy anywhere else.
- **3a. Reached from account deletion** → the owner is deleting the circle in order to delete their
  account; on success they return to the deletion flow, whose guard is now satisfied
  ([FR-015](../../requirements/functional/FR-015-delete-account.md)).
- **4a. Secret-Santa groups referenced this circle** → they keep working. A group holds at most a
  circle identifier and copied its people in when it was created; that identifier now resolves to
  nothing, which the Secret-Santa side must tolerate rather than treat as corruption. The dependency
  points one way only, by design.
- **4b. Other members had the circle open** → their next request finds it gone and they are told the
  circle no longer exists — not an error screen.

## Exceptions

- **The actor is an admin, not the owner** → refused. Deletion and transfer are the two rights the
  owner does not share.
- **The deletion fails partway** → nothing is deleted and the owner is not told it succeeded. A circle
  half-deleted — memberships gone, managed members orphaned — is worse than one that still exists.
- **The device is offline** → unavailable.

## Non-functional notes

- **Offline/local-first:** online-only; nothing on any device is touched, which is what makes deletion
  safe to make immediate.
- **Localization:** the confirmation is one of the screens most worth getting right in all three
  languages — a mistranslated irreversible action is a defect, not a typo
  ([NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)).
- **Privacy:** deletion is immediate and hard, with no soft-delete state and no grace period —
  consistent with account deletion ([ADR-0022](../../adr/0022-account-deletion-and-data-export.md)).
  A tombstone that keeps names for later would defeat the point of deleting a social graph.
- **Performance:** one transaction; the owner is not left watching a spinner over a partly deleted
  circle.

## Open questions

- **Should members be able to export the circle's data before it is deleted?** They can export their
  own at any time ([FR-014](../../requirements/functional/FR-014-export-personal-data.md)), but a
  deletion gives no warning. Recommendation: no pre-deletion export window in M2 — revisit if it turns
  out people lose managed-member profiles they curated.
- **Does deleting the circle need re-authentication, as account deletion does?** Recommendation: no —
  it is destructive but recoverable by re-creating and re-inviting, and the named confirmation already
  guards against the mis-tap. To be confirmed in the M2 feature spec.

## Traceability

- Requirement: [FR-022](../../requirements/functional/FR-022-delete-circle.md); constrained by
  [NFR-004](../../requirements/non-functional/NFR-004-data-protection-and-retention.md),
  [NFR-006](../../requirements/non-functional/NFR-006-circle-authorization.md)
- ADRs: [ADR-0018](../../adr/0018-circle-and-sharing-model.md) (owner rights; the sole-owner edge
  case), [ADR-0022](../../adr/0022-account-deletion-and-data-export.md) (immediate hard delete, and
  the "what will and will not be deleted" screen this one mirrors)
- Related: [FR-015](../../requirements/functional/FR-015-delete-account.md) — its sole-circle-owner
  guard is unblocked by this use case
- Glossary: [Circle](../../glossary/circle.md), [Managed member](../../glossary/managed-member.md),
  [Invitation](../../glossary/invitation.md)
- Origin: [brainstorming — accounts, families & sync (v2)](../../brainstorming/2026-07-24-accounts-families-sync.md)
