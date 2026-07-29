---
id: UC-MAIN-013
title: Leave a circle
app: Main
actors: [User]
status: Approved
priority: Must
related: [FR-021, NFR-006, ADR-0018]
---

# UC-MAIN-013 — Leave a circle

## Summary / goal

A [User](../../glossary/user.md) removes themselves from a [Circle](../../glossary/circle.md) they no
longer belong to — a friend group that dissolved, a partner's family after a separation — without
needing anyone's permission and without losing their account or their own data.

## Primary actor & stakeholders

- **Primary actor:** any member or admin of the circle.
- **Stakeholders & interests:** the remaining members, who lose a person from their list; the owner,
  who cannot simply leave and must hand the circle on first.

## Preconditions

- The actor is signed in and holds a [membership](../../glossary/circle-membership.md) in the circle.
- The device is online.

## Postconditions

- **Success:** the membership is gone; the circle no longer appears for them; the circle still has
  exactly one owner.
- **Failure:** the membership is unchanged.

## Trigger

The actor selects **Leave this circle**.

## Main success scenario

1. Actor selects **Leave this circle**.
2. System states what leaving does and does not do: the membership ends and access ends immediately;
   the **account stays**; their own Secret-Santa groups, members and completed
   [Draws](../../glossary/draw.md) are untouched; rejoining needs a fresh
   [Invitation](../../glossary/invitation.md).
3. Actor confirms.
4. System deletes the membership. The circle disappears from their list.

## Extensions / alternate flows

- **1a. The actor is the owner and there is someone else in the circle** → leaving is refused until
  they **transfer ownership** ([UC-MAIN-012](UC-MAIN-012-manage-circle-people.md)). The refusal offers
  the transfer directly rather than sending them to find it; a dead end here is what makes people
  delete things they meant to keep.
- **1b. The actor is the owner and the only person in the circle** → leaving is offered as **deleting
  the circle** ([UC-MAIN-014](UC-MAIN-014-delete-circle.md)), because a circle with nobody in it is
  not a state the model has. Managed members do not count as someone to transfer to — they cannot
  sign in and therefore cannot own anything.
- **4a. The actor was the last *account-backed* person but managed members remain** → the same as 1b:
  the managed profiles exist only inside the circle, so leaving means deleting it, and the
  confirmation says so by name.
- **4b. The actor is re-invited later** → an ordinary new invitation; nothing about the old membership
  is restored, and their old role is not remembered.

## Exceptions

- **A managed member "leaves"** → impossible; they cannot sign in. An owner or admin removes them
  ([UC-MAIN-011](UC-MAIN-011-manage-managed-members.md)).
- **The membership ended concurrently** (they were removed in the meantime) → the circle is simply
  already gone from their list; the action reports no error worth showing.
- **The device is offline** → unavailable.

## Non-functional notes

- **Offline/local-first:** online-only, and it changes nothing about local Secret-Santa data — which
  is the point of the confirmation wording.
- **Localization:** DE/EN/FR like every other screen
  ([NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)).
- **Privacy:** leaving ends the actor's access to the circle's data on the next request
  ([NFR-006](../../requirements/non-functional/NFR-006-circle-authorization.md)). It does **not**
  erase them from the past: Secret-Santa groups already created from the circle keep the copy they
  took, because a completed draw is history and other people's reveal links depend on it
  ([ADR-0018](../../adr/0018-circle-and-sharing-model.md)).
- **Performance:** immediate; a single round trip.

## Open questions

- **Are the remaining members told who left?** Recommendation: no explicit notification in M2 — the
  person disappears from the list, which is how a small shared list is normally read.

## Traceability

- Requirement: [FR-021](../../requirements/functional/FR-021-leave-circle.md); constrained by
  [NFR-006](../../requirements/non-functional/NFR-006-circle-authorization.md)
- ADR: [ADR-0018](../../adr/0018-circle-and-sharing-model.md) ("membership is what a person leaves";
  the sole-owner edge case it deferred to this milestone)
- Related use cases: [UC-MAIN-012](UC-MAIN-012-manage-circle-people.md) (transfer, removal by someone
  else), [UC-MAIN-014](UC-MAIN-014-delete-circle.md)
- Glossary: [Circle membership](../../glossary/circle-membership.md), [Circle](../../glossary/circle.md)
- Origin: [brainstorming — accounts, families & sync (v2)](../../brainstorming/2026-07-24-accounts-families-sync.md)
