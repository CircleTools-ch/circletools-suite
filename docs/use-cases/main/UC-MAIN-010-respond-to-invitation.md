---
id: UC-MAIN-010
title: Accept or decline a circle invitation
app: Main
actors: [User, Invited person]
status: Approved
priority: Must
related: [FR-018, FR-019, NFR-003, NFR-005, NFR-006, ADR-0018, ADR-0043]
---

# UC-MAIN-010 — Accept or decline a circle invitation

## Summary / goal

Someone who has been invited to a [Circle](../../glossary/circle.md) joins it — or turns it down.
This is the side of the [Invitation](../../glossary/invitation.md) where the two paths (no account yet
/ account already) actually diverge, and the only side that is allowed to know which one applied.

## Primary actor & stakeholders

- **Primary actor:** the invited person — either a [User](../../glossary/user.md) or someone with no
  account yet.
- **Stakeholders & interests:** the inviter, waiting; the circle's members, who get a new person; the
  operator, because this is where an emailed link turns into access to other people's data.

## Preconditions

- A pending, unexpired, unrevoked invitation exists for the recipient's address.
- The device is online.

## Postconditions

- **Success (accept):** the person holds a **member** membership in the circle, the invitation is
  consumed, and they can see the circle and its people.
- **Success (decline):** no membership exists, the invitation is consumed, and the circle keeps no
  record of the person beyond the address the inviter typed.
- **Failure:** no membership is created and the invitation remains as it was — pending, expired or
  revoked.

## Trigger

The recipient opens the invitation mail, or opens the app and sees a pending join request.

## Main success scenario — the address has no account

1. Recipient opens the link in the mail.
2. System shows who invited them, to which circle, and offers **Join** or **No thanks**.
3. Recipient selects **Join** and is taken to sign-up with the **invited address pre-filled and not
   editable** ([UC-MAIN-001](UC-MAIN-001-create-account.md)).
4. Recipient completes sign-up and **verifies the address**.
5. System creates the account, then the **member** membership, and lands the recipient in the circle.

## Main success scenario — the address already has an account

1. Recipient receives a mail telling them they have been invited and to open the app.
2. Recipient signs in; the pending **join request** is shown in the Main shell.
3. Recipient selects **Accept** (or **Decline**).
4. System creates the **member** membership and shows the circle.

## Extensions / alternate flows

- **The membership invariant:** in both paths the membership is only ever granted to an account whose
  **verified** address is the invited one. The mailed link is therefore not a bearer key to a circle —
  a forwarded mail lets the wrong person read the invitation, but never join. This is why the sign-up
  path locks the address field and why the account path needs no token at all.
- **2a. Recipient selects "No thanks" (no-account path)** → the invitation is declined without any
  account being created. **The decline action exists on both paths deliberately:** if only account
  holders could decline, "declined" would itself prove an account exists, and the inviter's view would
  become the oracle that [ADR-0018](../../adr/0018-circle-and-sharing-model.md) closed.
- **3a. Recipient is signed in as a *different* account** → the join request is not theirs and is not
  offered. They are told the invitation belongs to another address and can sign out and in as its
  owner. No hint is given as to whether that address has an account.
- **4a. Recipient already belongs to the circle** → they are told so; the invitation is consumed and no
  second membership is created.
- **3b / 4c. Recipient joins *as an existing offline person*** → when the circle has
  [managed members](../../glossary/managed-member.md), the acceptance screen asks **who they are
  joining as**: *someone new*, or one of those offline people. Picking one **promotes** that profile —
  the account is linked to it, keeping its place and history
  ([FR-019](../../requirements/functional/FR-019-manage-managed-members.md)). When the inviter already
  aimed the invitation at a specific offline person, that is authoritative: the screen states *"you
  will join as <name>"* and there is nothing to choose. This choice is offered **only** to the
  signed-in recipient whose verified address is the invited one — never on the anonymous landing
  preview, and never to a forwarded-link holder
  ([ADR-0043](../../adr/0043-invitee-chosen-managed-member-promotion.md)).
- **4b. Recipient never responds** → the invitation expires on its own. The inviter sees it leave the
  pending list, with no distinction between *ignored* and *expired*.

## Exceptions

- **The invitation has expired, was revoked, or was already used** → the recipient is told plainly and
  offered to ask the inviter for a new one. Invitations are single-use so an archived or forwarded mail
  cannot be replayed after the fact.
- **The circle was deleted meanwhile** ([UC-MAIN-014](UC-MAIN-014-delete-circle.md)) → the recipient is
  told the circle no longer exists.
- **Sign-up fails or is abandoned (no-account path)** → the invitation stays pending until it expires,
  so the recipient can come back to the same link.
- **The device is offline** → responding is unavailable; nothing is queued.

## Non-functional notes

- **Offline/local-first:** online-only, and irrelevant to account-free Secret-Santa use.
- **Localization:** the acceptance screens follow the recipient's own language — for a new user, the
  browser's, until they choose one in their profile
  ([UC-MAIN-005](UC-MAIN-005-manage-profile.md)); for an existing user, their stored preference
  ([NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)).
- **Privacy:** before accepting, the recipient sees only the circle's name, its label and the inviter's
  display name — never the member list. What the circle contains is member-only data
  ([NFR-006](../../requirements/non-functional/NFR-006-circle-authorization.md)), and someone deciding
  whether to join has not joined yet. The **one** deliberate exception is the join-as choice above: a
  signed-in recipient whose verified address is the invited one may be shown the circle's **offline
  people** (display names only, no addresses, no account-backed members) so they can say who they are
  — unlocked to precisely the person who is about to join, and to nobody else
  ([ADR-0043](../../adr/0043-invitee-chosen-managed-member-promotion.md)).
- **Performance:** accepting is a single round trip and puts the person in the circle immediately —
  no admin approval step.

## Open questions

- **Is the inviter notified of an acceptance?** Recommendation: yes, in-app — the new person simply
  appears in the people list, which is notification enough for M2. An email notification is deferred.
- **Should declining block re-invitation for a cooldown period?** Recommendation: yes, a short one, so
  "no thanks" cannot be answered with an immediate resend. Number to be fixed in the M2 feature spec.

## Traceability

- Requirement: [FR-018](../../requirements/functional/FR-018-respond-to-circle-invitation.md);
  constrained by
  [NFR-005](../../requirements/non-functional/NFR-005-account-enumeration-resistance.md),
  [NFR-006](../../requirements/non-functional/NFR-006-circle-authorization.md)
- ADR: [ADR-0018](../../adr/0018-circle-and-sharing-model.md) (decision D5 — the branch resolves on
  the recipient's side)
- Related use cases: [UC-MAIN-009](UC-MAIN-009-invite-to-circle.md) (the inviter's side),
  [UC-MAIN-001](UC-MAIN-001-create-account.md) (the sign-up path it borrows)
- Glossary: [Invitation](../../glossary/invitation.md),
  [Circle membership](../../glossary/circle-membership.md), [User](../../glossary/user.md)
- Origin: [brainstorming — accounts, families & sync (v2)](../../brainstorming/2026-07-24-accounts-families-sync.md), decision D5
