---
id: UC-MAIN-011
title: Add and maintain managed members
app: Main
actors: [Circle admin]
status: Approved
priority: Must
related: [FR-019, NFR-003, NFR-004, NFR-006, ADR-0018]
---

# UC-MAIN-011 — Add and maintain managed members

## Summary / goal

An owner or admin adds people who have **no account and never will** — young children, relatives who
will not sign up — as [Managed members](../../glossary/managed-member.md) of the
[Circle](../../glossary/circle.md), so a family Secret-Santa can include everyone in the family.

## Primary actor & stakeholders

- **Primary actor:** a circle **owner** or **admin**.
- **Stakeholders & interests:** the managed person, whose personal data is held by someone else and
  who cannot correct it themselves; the other members, who see them as ordinary people in the circle;
  the operator, holding data about a person who never agreed to anything
  ([NFR-004](../../requirements/non-functional/NFR-004-data-protection-and-retention.md)).

## Preconditions

- The actor is signed in, verified, and holds the **owner** or **admin** role in the circle.
- The device is online.

## Postconditions

- **Success:** the managed member has a profile and a
  [membership](../../glossary/circle-membership.md) in the **member** role, and appears in the circle's
  people list marked as managed.
- **Failure:** nothing is added and nothing is partially written.

## Trigger

The actor selects **Add a managed member** on the circle page.

## Main success scenario

1. Actor selects **Add a managed member**.
2. System asks for a **display name** and, optionally, a **preferred language** — used later for the
   content this person receives (their [reveal link](../../glossary/reveal-link.md) page in M4).
3. Actor confirms.
4. System creates the profile and its membership, and shows the person in the people list, visibly
   marked as **managed** — the mark matters, because this is the person who will need a reveal link
   passed to them by hand rather than an in-app assignment.

## Extensions / alternate flows

- **2a. No email address is asked for** — by definition. An address is what turns someone into an
  [Invitation](../../glossary/invitation.md) instead ([UC-MAIN-009](UC-MAIN-009-invite-to-circle.md)).
- **2b. The name duplicates an existing person** → allowed. Display names are not unique anywhere in
  the suite, and two cousins called Anna are a normal family, not an error.
- **4a. Actor edits a managed member** → name and language can be changed at any time by an owner or
  admin. The person cannot edit their own profile; that is what *managed* means.
- **4b. Actor removes a managed member** → the profile and its membership are deleted. Secret-Santa
  groups that already copied this person in are **untouched** — the copy belongs to the group from the
  moment it is made ([ADR-0018](../../adr/0018-circle-and-sharing-model.md)), and a completed
  [Draw](../../glossary/draw.md) is history.
- **4c. The person later gets an account — promotion** → the actor invites their address **from the
  managed profile**. On acceptance, the existing membership is **linked to the new
  [User](../../glossary/user.md)** rather than a second one being created, so the person keeps their
  place and their history. Every enumeration rule from
  [UC-MAIN-010](UC-MAIN-010-respond-to-invitation.md) applies unchanged, including that the membership
  is granted only to a verified owner of that address.
- **4d. The admin who created them leaves or deletes their account** → managed members are **circle
  data, not the creator's data**. They stay, and the remaining owner and admins maintain them. This
  resolves the question [FR-015](../../requirements/functional/FR-015-delete-account.md) deferred to M2.

## Exceptions

- **The actor holds only the `member` role** → not offered, and rejected server-side
  ([NFR-006](../../requirements/non-functional/NFR-006-circle-authorization.md)).
- **The display name is empty** → required; there is nothing else to identify the person by.
- **A managed member is proposed as owner or admin** → refused. Roles above `member` imply someone who
  can sign in and act; a managed member cannot sign in at all.
- **The device is offline** → unavailable.

## Non-functional notes

- **Offline/local-first:** online-only in M2.
- **Localization:** the managed member's preferred language is *their* language, not the admin's — it
  exists precisely because the person who reads the content is not the person who typed it
  ([NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)).
- **Privacy:** deliberately minimal — a name and a language. No birth date, no address, no photo:
  this is data about someone (often a child) who did not enter it themselves and cannot review it, so
  data minimisation is not a nicety here. It is visible to circle members only, exports with the
  circle, and is deleted with the circle ([UC-MAIN-014](UC-MAIN-014-delete-circle.md)).
- **Performance:** adding is a single round trip; adding five children in a row must not require five
  page loads.

## Open questions

- **Does a managed member appear in another member's data export?** They are circle data that the
  member can see in the app, and [ADR-0022](../../adr/0022-account-deletion-and-data-export.md) says an
  export mirrors in-app visibility — so yes. To be confirmed in the M2 feature spec.
- **Is there a cap on managed members per circle?** An abuse limit rather than a product one; number
  in the M2 feature spec.

## Traceability

- Requirement: [FR-019](../../requirements/functional/FR-019-manage-managed-members.md); constrained by
  [NFR-004](../../requirements/non-functional/NFR-004-data-protection-and-retention.md),
  [NFR-006](../../requirements/non-functional/NFR-006-circle-authorization.md)
- ADR: [ADR-0018](../../adr/0018-circle-and-sharing-model.md) (decision D7 — managed members are in
  the model from the start), [ADR-0022](../../adr/0022-account-deletion-and-data-export.md)
- Glossary: [Managed member](../../glossary/managed-member.md), [Circle](../../glossary/circle.md),
  [Circle membership](../../glossary/circle-membership.md), [Member](../../glossary/member.md)
- Origin: [brainstorming — accounts, families & sync (v2)](../../brainstorming/2026-07-24-accounts-families-sync.md), decision D7
