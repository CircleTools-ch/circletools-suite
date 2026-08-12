---
id: UC-SS-012
title: Edit my own wishlist
app: Secret-Santa
actors: [Member]
status: Draft
priority: Should
related: [FR-030, NFR-003, ADR-0019, ADR-0014]
---

# UC-SS-012 — Edit my own wishlist

## Summary / goal

A signed-in [User](../../glossary/user.md) who is a **linked** [Member](../../glossary/member.md)
maintains **their own** [Wishlist](../../glossary/wishlist.md) for a
[Group](../../glossary/group.md) — the gift ideas their giver sees at reveal — instead of the
organizer typing it for them. Ownership of the wishlist moves to the person it describes.

## Primary actor & stakeholders

- **Primary actor:** the **linked member** — a signed-in user linked to a member of a group.
- **Stakeholders & interests:** the giver, who wants an accurate, current wishlist at reveal time; the
  organizer, who is **relieved** of maintaining this member's wishlist while the link stands, but must
  still be able to serve unlinked members and issue reveal links.

## Preconditions

- The actor is signed in and **linked** to a member of a group
  ([UC-SS-010](UC-SS-010-link-member-to-account.md) or
  [UC-SS-009](UC-SS-009-create-group-from-circle.md)).
- The group is account-scoped and syncs
  ([UC-MAIN-015](../main/UC-MAIN-015-sync-across-devices.md)).

## Postconditions

- **Success:** the member's wishlist reflects the actor's edits and has synced; givers see the current
  content the next time it is served to them.
- **Failure:** the wishlist is unchanged; the actor sees why and no partial write is claimed
  ([ADR-0029](../../adr/0029-storage-write-failure-contract.md)).

## Trigger

The actor opens their entry in a group and edits their wishlist.

## Main success scenario

1. Actor opens a group they are linked in and selects **my wishlist**.
2. Actor adds, edits, reorders or removes free-text gift ideas.
3. System saves the change and syncs it ([UC-MAIN-016](../main/UC-MAIN-016-work-offline-and-reconcile.md)).
4. Givers see the updated wishlist wherever it is served to them next — **in-app reveal is live**
   ([UC-SS-011](UC-SS-011-see-my-assignment.md)).

## Extensions / alternate flows

- **2a. Empty item** → ignored, not saved, matching organizer-side wishlist editing
  ([UC-SS-002](UC-SS-002-manage-members.md)).
- **Editing offline** → the change is stored locally and reconciled on the next sync; a concurrent
  edit of the *same* wishlist from another device resolves last-write-wins per aggregate
  ([UC-MAIN-016](../main/UC-MAIN-016-work-offline-and-reconcile.md),
  [FR-024](../../requirements/functional/FR-024-offline-editing-and-reconciliation.md)). In practice a
  member edits their own wishlist from one device at a time, so a real collision is rare.
- **The actor is a member of two groups** → they have **two** wishlists, one per membership (a member
  is per group — [ADR-0019](../../adr/0019-member-user-linking.md)); editing one does not change the
  other. See the open question on whether these should ever be unified.
- **The actor is unlinked** while editing / after edits → wishlist **ownership returns to the
  organizer** with the content intact; the actor can no longer edit it
  ([UC-SS-010](UC-SS-010-link-member-to-account.md) unlink). Nothing is deleted.

## Exceptions

- **Reveal links already issued do not update.** A [reveal link](../../glossary/reveal-link.md) is a
  **self-contained snapshot** encoded when it was generated
  ([ADR-0014](../../adr/0014-reveal-link-encoding-and-privacy.md)); a wishlist edited afterwards
  reaches a giver **holding an old link** only if the organizer re-issues it. In-app reveal, by
  contrast, is always current. This asymmetry must be **stated to the member**, or a self-owned
  wishlist edited "in time" silently fails to reach a link-holding giver.

## Non-functional notes

- **Offline/local-first:** editing works offline and reconciles on sync
  ([NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md),
  [FR-024](../../requirements/functional/FR-024-offline-editing-and-reconciliation.md)).
- **Localization:** DE/EN/FR for the editor UI; wishlist **content is free text authored by the
  member and is not translated**
  ([NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)).
- **Privacy:** the wishlist is account-scoped group data, readable by the organizer and by whichever
  giver it is served to; it is not an assignment and carries no giver→recipient information, so it is
  not subject to the NFR-002 authorization rule — only ordinary group authorization.
- **Performance:** wishlist edits are small local writes; sync is incremental.

## Open questions

- **Should a user's wishlist be per-group or one wishlist reused across their memberships?** The model
  is per-group ([ADR-0019](../../adr/0019-member-user-linking.md)), which can surprise a user who
  expects "my wishlist" to be one thing. Recommendation: **keep per-group** for M4 (a wishlist can
  legitimately differ per exchange — a work group and a family want different lists) but **offer to
  copy** a previous wishlist when joining a new group. Decide in the feature spec.
- **May the organizer still see (not edit) a linked member's wishlist?** Recommendation: **yes, see** —
  the organizer needs it to issue reveal links for unlinked givers and to reason about the group — but
  **not edit** while the link stands. Confirm in the feature spec.

## Traceability

- Requirement: [FR-030](../../requirements/functional/FR-030-edit-my-wishlist.md); related
  [NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md),
  [FR-024](../../requirements/functional/FR-024-offline-editing-and-reconciliation.md)
- ADRs: [ADR-0019](../../adr/0019-member-user-linking.md) (wishlist ownership moves on link, returns
  on unlink; per-group membership), [ADR-0014](../../adr/0014-reveal-link-encoding-and-privacy.md)
  (reveal links are snapshots)
- Related use cases: [UC-SS-010](UC-SS-010-link-member-to-account.md),
  [UC-SS-011](UC-SS-011-see-my-assignment.md), [UC-SS-002](UC-SS-002-manage-members.md),
  [UC-SS-005](UC-SS-005-share-reveal-links.md)
- Glossary: [Wishlist](../../glossary/wishlist.md), [Member](../../glossary/member.md),
  [User](../../glossary/user.md), [Group](../../glossary/group.md)
- Origin: [brainstorming — accounts, families & sync (v2)](../../brainstorming/2026-07-24-accounts-families-sync.md),
  decision D3
