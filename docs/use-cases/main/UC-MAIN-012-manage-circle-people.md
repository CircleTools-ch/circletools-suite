---
id: UC-MAIN-012
title: Manage a circle's people, roles and ownership
app: Main
actors: [Circle owner, Circle admin]
status: Approved
priority: Must
related: [FR-020, NFR-006, ADR-0018]
---

# UC-MAIN-012 — Manage a circle's people, roles and ownership

## Summary / goal

An owner or admin keeps the [Circle](../../glossary/circle.md) correct: renames it, changes what
people may do in it, removes those who no longer belong, and — for the owner alone — hands the circle
on to someone else.

## Primary actor & stakeholders

- **Primary actor:** the circle **owner** (roles, ownership, everything an admin may) or an **admin**
  (name, label, invite, remove members).
- **Stakeholders & interests:** the person whose role or membership changes, who may lose access
  without warning; the other members, whose shared data the roles protect.

## Preconditions

- The actor is signed in, verified, and holds the required role in the circle.
- The device is online.

## Postconditions

- **Success:** the circle reflects the change immediately, and the affected person's access matches
  their new role on their very next request.
- **Failure:** nothing changes; in particular the circle still has **exactly one owner**.

## Trigger

The actor opens the circle's people list and acts on a person, or opens the circle's settings.

## Main success scenario — change a role

1. Owner opens the people list and selects a person.
2. System offers the roles that person may hold: **admin** or **member**.
3. Owner picks one and confirms.
4. System applies the new role. The person's next request is authorized under it — no sign-out, no
   token refresh, because membership and role are evaluated per request
   ([NFR-006](../../requirements/non-functional/NFR-006-circle-authorization.md)).

## Main success scenario — remove a person

1. Actor selects a person and chooses **Remove from circle**.
2. System states plainly what removal does and does not do: the membership ends; the person's
   **account is untouched**; their Secret-Santa groups, [Members](../../glossary/member.md) and
   completed [Draws](../../glossary/draw.md) are untouched.
3. Actor confirms; the membership is deleted and access ends immediately.

## Main success scenario — transfer ownership

1. Owner opens circle settings and chooses **Transfer ownership**.
2. System lists the people who may receive it — account-backed members and admins only.
3. Owner picks one and confirms, understanding that this is not reversible without the new owner's
   cooperation.
4. System makes that person the **owner** and the previous owner an **admin**, in one step. The new
   owner sees an in-app notice.

## Extensions / alternate flows

- **Renaming and re-labelling** → owner or admin may change the circle's name and its label at any
  time. The label stays user data and is not re-translated
  ([UC-MAIN-008](UC-MAIN-008-create-circle.md)).
- **Role changes are owner-only.** An admin may invite and remove members and maintain managed
  members, but may **not** create another admin. Otherwise one admin can manufacture a majority of
  admins, and the owner's exclusive rights — delete, transfer — become negotiable.
- **An admin may not remove another admin**, only members and managed members. Removing an admin is
  the owner's call.
- **The owner cannot be removed or demoted by anyone**, including themselves. The way out is transfer
  ([this use case](UC-MAIN-012-manage-circle-people.md)), leaving
  ([UC-MAIN-013](UC-MAIN-013-leave-circle.md)) or deletion
  ([UC-MAIN-014](UC-MAIN-014-delete-circle.md)).
- **Managed members** are removed here too, but have no role to change
  ([UC-MAIN-011](UC-MAIN-011-manage-managed-members.md)).
- **Transfer is immediate, not an offer.** A pending transfer would create exactly the state the model
  forbids — a circle whose ownership is in limbo — and the new owner can always transfer it back or
  leave. Being made owner grants no access to anything they could not already see.

## Exceptions

- **The actor lacks the role for the action** → not offered, and rejected server-side with the same
  response a non-member gets. Hiding the button is presentation, not authorization.
- **The transfer target is a managed member** → refused; they cannot sign in, and an owner who cannot
  act is an orphaned circle.
- **The transfer target's membership ended concurrently** (they left, or were removed in another
  session) → the transfer fails and the circle keeps its current owner.
- **The person being removed is the actor** → that is [UC-MAIN-013](UC-MAIN-013-leave-circle.md);
  the removal flow does not double as a leave flow, because the confirmations differ.
- **The device is offline** → unavailable.

## Non-functional notes

- **Offline/local-first:** online-only.
- **Localization:** role names are UI strings in DE/EN/FR; the circle's own label is not
  ([NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)).
- **Privacy:** the people list shows display names, not email addresses. Circle membership does not
  entitle anyone to another member's address — that is identity data, and exposing it would also
  reintroduce the enumeration surface
  [NFR-005](../../requirements/non-functional/NFR-005-account-enumeration-resistance.md) closed.
- **Performance:** role and membership changes take effect on the next request, with no cached
  authorization to wait out.

## Open questions

- **Is a removed person told?** Recommendation: no notification in M2 — the circle simply no longer
  appears for them. A "you were removed from X" mail invites drama and adds a mail path for an event
  the app already shows.
- **Should the number of admins be capped?** Probably not, but the owner should be able to see at a
  glance who holds elevated roles. UI detail for the M2 feature spec.

## Traceability

- Requirement: [FR-020](../../requirements/functional/FR-020-manage-circle-roles-and-membership.md);
  constrained by [NFR-006](../../requirements/non-functional/NFR-006-circle-authorization.md)
- ADR: [ADR-0018](../../adr/0018-circle-and-sharing-model.md) (the role table and the "exactly one
  owner" rule, whose transfer flow it left to this milestone)
- Glossary: [Circle](../../glossary/circle.md),
  [Circle membership](../../glossary/circle-membership.md),
  [Managed member](../../glossary/managed-member.md)
- Origin: [brainstorming — accounts, families & sync (v2)](../../brainstorming/2026-07-24-accounts-families-sync.md)
