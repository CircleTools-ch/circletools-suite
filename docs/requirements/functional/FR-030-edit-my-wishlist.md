---
id: FR-030
title: Edit my own wishlist
type: functional
status: Draft
priority: Should
source: UC-SS-012
verification: test
related: [UC-SS-012, UC-SS-010, UC-SS-011, FR-024, NFR-001, NFR-003, ADR-0019, ADR-0014, FEAT-005]
---

# FR-030 — Edit my own wishlist

## Description

A signed-in [User](../../glossary/user.md) who is a **linked** [Member](../../glossary/member.md) must
be able to maintain **their own** [Wishlist](../../glossary/wishlist.md) for a
[Group](../../glossary/group.md) — the free-text gift ideas their giver sees at reveal — instead of
the organizer maintaining it.

- On linking, **ownership of the wishlist moves to the linked user**
  ([ADR-0019](../../adr/0019-member-user-linking.md)); on unlinking it returns to the organizer with
  content intact ([FR-028](FR-028-link-member-to-account.md)).
- While the link stands, the **organizer may still see** the wishlist (they need it to issue reveal
  links for unlinked givers) but **may not edit** it.
- Edits sync ([FR-023](FR-023-sync-across-devices.md)); editing **offline** is stored locally and
  reconciled on the next sync, last-write-wins per aggregate
  ([FR-024](FR-024-offline-editing-and-reconciliation.md)).
- Empty items are ignored, matching organizer-side wishlist editing
  ([FR-002](FR-002-manage-members-and-wishlists.md)).
- A user who is a member of **several** groups has **one wishlist per membership** (a member is per
  group); editing one does not change another. When joining a new group, the system **offers to copy**
  a previous wishlist rather than sharing one.
- **Reveal links already issued do not update.** A reveal link is a self-contained snapshot encoded
  when generated ([ADR-0014](../../adr/0014-reveal-link-encoding-and-privacy.md)); a wishlist edited
  afterwards reaches a giver holding an **old** link only if the organizer re-issues it. In-app reveal
  ([FR-029](FR-029-see-my-assignment.md)) is always current. The UI must **state this** to the member,
  so an edit made "in time" does not silently fail to reach a link-holding giver.

## Rationale

The person who wants the gift is the best author of the list, and self-owned wishlists remove a chore
from the organizer while making the list more accurate — the value [ADR-0019](../../adr/0019-member-user-linking.md)
(decision D3) attached to linking. Keeping wishlists **per group** matches the model (membership is
per group) and is genuinely useful — a work exchange and a family exchange want different lists — while
the "copy my last one" offer removes the busywork that per-group would otherwise create.

The reveal-link snapshot caveat is called out because it is a silent trap: a member who updates their
list the day before Christmas would reasonably expect their giver to see it, but a giver holding a link
generated last week would not. Saying so is cheaper than the disappointment.

## Acceptance criteria

```gherkin
Given a signed-in linked member
When they add, edit, reorder or remove gift ideas in their wishlist
Then the changes are saved and synced

Given a linked member editing their wishlist
When they add an empty item
Then it is ignored and not saved

Given a member is linked to a user
When the link is established
Then the linked user can edit the wishlist
And the organizer can see it but cannot edit it

Given a linked member's wishlist
When the member is unlinked
Then wishlist ownership returns to the organizer
And the content is unchanged

Given a linked member editing offline
When the device reconnects and syncs
Then the offline edits are reconciled last-write-wins

Given a user linked in two groups
When they edit their wishlist in one group
Then the wishlist in the other group is unchanged

Given a user joining a new group in which they are linked
When they start their wishlist
Then they are offered to copy a previous wishlist

Given a wishlist edited after a reveal link was generated
When a giver opens that already-issued link
Then it shows the wishlist as it was when the link was generated
And the member was told in advance that in-app reveal is current but old links are not

Given a linked member's wishlist in German, English and French
When it is edited
Then the editor UI is localized
And the free-text content is stored and shown untranslated
```

## Dependencies & related

- Derived from: [UC-SS-012](../../use-cases/secret-santa/UC-SS-012-edit-my-wishlist.md)
- Related: [FR-028](FR-028-link-member-to-account.md) (ownership moves on link),
  [FR-029](FR-029-see-my-assignment.md) (where the wishlist is shown to the giver),
  [FR-002](FR-002-manage-members-and-wishlists.md) (organizer-side wishlist editing this mirrors),
  [FR-024](FR-024-offline-editing-and-reconciliation.md),
  [NFR-001](../non-functional/NFR-001-local-first-offline.md),
  [NFR-003](../non-functional/NFR-003-multilingual-de-en-fr.md)
- Governing ADRs: [ADR-0019](../../adr/0019-member-user-linking.md) (wishlist ownership; per-group
  membership), [ADR-0014](../../adr/0014-reveal-link-encoding-and-privacy.md) (reveal links are
  snapshots)
- Implemented by: [FEAT-005](../../features/FEAT-005-m4-linking.md) *(M4 — linking; designed, `Draft`)*, verified by [TP-005](../../features/TP-005-m4-linking.md)
- ~~Open for the feature spec: confirm the per-group-vs-unified wishlist choice and the "copy previous"
  offer.~~ **Settled in [FEAT-005](../../features/FEAT-005-m4-linking.md):** wishlists are **per
  membership**, with a client-side **offer to copy** a previous one when joining a new linked group; the
  organizer may **see but not edit** a linked member's wishlist while the link stands.
- Glossary: [Wishlist](../../glossary/wishlist.md), [Member](../../glossary/member.md),
  [User](../../glossary/user.md), [Group](../../glossary/group.md)
