---
id: UC-SS-019
title: Join a shared-by-link group via the join link
app: Secret-Santa
actors: [Participant]
status: Draft
priority: Should
related: [FR-041, FR-039, NFR-002, UC-SS-018, UC-SS-020, ADR-0058, ADR-0019, ADR-0043]
---

# UC-SS-019 — Join a shared-by-link group via the join link

## Summary / goal

A person opens a [join link](../../glossary/join-link.md) and joins a
[Shared-by-link group](../../glossary/shared-by-link-group.md) — **with or without an account** — so
they can see the group and their **own** [Assignment](../../glossary/assignment.md) once it is drawn,
and keep their own [Wishlist](../../glossary/wishlist.md) / [Gift note](../../glossary/gift-note.md) up
to date. Joining is a [person claim](../../glossary/person-claim.md): the joiner says **which
[Member](../../glossary/member.md) they are** and **how they want to be remembered**.

## Primary actor & stakeholders

- **Primary actor:** Participant (the joiner).
- **Stakeholders:** the organizer/admin (wants everyone in without a forced sign-up); other participants
  (their spots must not be taken by someone else — first-claim-locks); the operator (account-free join
  is an account-free write path, bounded by anti-abuse limits).

## Preconditions

- The group is in **Shared by link** mode and the joiner has opened a valid join link.

## Postconditions

- **Success:** the joiner is bound to exactly one member (by an account, or by this device); that member
  is **locked** to them; they see the group and, once drawn, their own assignment.
- **Failure:** no binding is created; the joiner is told why (e.g. the member was already claimed).

## Trigger

The joiner opens the join link (deep-links into the app on Android, or the web app otherwise).

## Main success scenario

1. The join link opens onto a short **"Who are you in this group?"** step showing the roster.
2. The joiner **claims an existing member** (e.g. "I'm Chloé").
3. The app asks **"How should we remember you?"** — **create/sign in to an account**, or **just this
   device**.
4. The joiner picks one; the app **locks** that member to the chosen account or device (a
   [person claim](../../glossary/person-claim.md)).
5. The joiner sees the group; once the draw has run, they see **their own** assignment and can edit
   their own wishlist / gift note.

## Extensions / alternate flows

- **2a. The joiner is not yet on the roster** → they choose **Add me as a new person**, entering a name;
  a new member is created and claimed to them.
- **3a. Account chosen** → the member is linked to the [User](../../glossary/user.md) (member↔user
  linking, [ADR-0019](../../adr/0019-member-user-linking.md); invitee-chosen promotion,
  [ADR-0043](../../adr/0043-invitee-chosen-managed-member-promotion.md)) and travels to their other
  signed-in devices.
- **3b. Just this device chosen** → the claim is device-local; nothing syncs beyond this device. If the
  joiner later switches devices, an admin must **free** the member so they can rejoin
  ([UC-SS-020](UC-SS-020-release-a-claimed-person.md)).
- **2b. The chosen member is already claimed** → the claim is **refused** (first claim wins and locks);
  the joiner is told to pick another member or ask an admin to free it.
- **1a. The admin has since claimed admin to an account** → the join link **still works**; only the
  admin capability changed ([UC-SS-018](UC-SS-018-share-and-manage-as-admin.md)).
- **1b. The join link's token was regenerated / the group was made local** → the link no longer resolves;
  the joiner is told to ask the organizer for the current link.

## Exceptions

- **Backend unreachable** → the join cannot complete (it needs the server); the joiner is told to retry,
  and no partial binding is left behind.

## Non-functional notes

- **Privacy — trust-bounded:** because the join link is a single shared secret, a holder could claim a
  member other than themselves and see that member's assignment. This is accepted under the family/friend
  **trust context**, bounded by **first-claim-locks** and admin release
  ([NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md),
  [ADR-0058](../../adr/0058-account-free-link-shared-groups.md)); a joiner only ever sees the assignment
  of the member they hold, never the full mapping.
- **Offline/local-first:** once joined on a device, reading the group follows the normal
  [sync](../../glossary/sync.md) model; the *initial* join needs the server.
- **Localization:** the join steps, the identity/persistence choices and their helper text are DE/EN/FR
  ([NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)).

## Open questions

- A device-only joiner has the weakest identity; should the app nudge them to add an account before the
  draw so a device change doesn't need an admin? Suggest a gentle prompt; confirm in the feature spec.

## Traceability

- Requirements: [FR-041](../../requirements/functional/FR-041-join-link-and-person-claim.md)
  (join link & person claim), [FR-039](../../requirements/functional/FR-039-shared-by-link-storage-and-sync.md)
  (shared-by-link storage & sync)
- Related use cases: [UC-SS-018](UC-SS-018-share-and-manage-as-admin.md),
  [UC-SS-020](UC-SS-020-release-a-claimed-person.md)
- Governing ADR: [ADR-0058](../../adr/0058-account-free-link-shared-groups.md),
  [ADR-0019](../../adr/0019-member-user-linking.md),
  [ADR-0043](../../adr/0043-invitee-chosen-managed-member-promotion.md)
- Feature spec: [FEAT-013](../../features/FEAT-013-account-free-link-shared-groups.md)
- Glossary: [Join link](../../glossary/join-link.md), [Person claim](../../glossary/person-claim.md),
  [Member](../../glossary/member.md), [Assignment](../../glossary/assignment.md)
