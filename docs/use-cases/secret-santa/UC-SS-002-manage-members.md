---
id: UC-SS-002
title: Manage members and their wishlists
app: Secret-Santa
actors: [Organizer]
status: Approved
priority: Must
related: [FR-002, NFR-001, NFR-003]
---

# UC-SS-002 — Manage members and their wishlists

## Summary / goal

An organizer adds, edits, and removes [Members](../../glossary/member.md) of a
[Group](../../glossary/group.md) and maintains each member's
[Wishlist](../../glossary/wishlist.md) (gift ideas), so the draw and the reveal can carry useful
gift guidance — all **offline, without an account**.

## Primary actor & stakeholders

- **Primary actor:** Organizer.
- **Stakeholders:** Members (want their gift ideas represented accurately); givers (want the
  recipient's wishlist at reveal time).

## Preconditions

- A group exists (see [UC-SS-001](UC-SS-001-create-group.md)).

## Postconditions

- **Success:** the group's member list and their wishlists reflect the organizer's changes in local storage.
- **Failure:** no change is persisted; the organizer sees why.

## Trigger

The organizer opens a group and manages its members.

## Main success scenario

1. Organizer opens a group.
2. Organizer adds a member by **display name**.
3. Organizer optionally adds **wishlist** items (free-text gift ideas) to that member.
4. Organizer edits a member's name or wishlist items as needed.
5. Organizer removes a member who is no longer participating.
6. System validates each change and **saves it to local storage**.

## Extensions / alternate flows

- **2a.** Member name is empty → system blocks adding and prompts for a name.
- **2b.** Duplicate member name → system warns but allows it (names may legitimately repeat).
- **3a.** Wishlist item is empty → the empty item is ignored (not saved).
- **5a.** Removing a member referenced by an [Exclusion](../../glossary/exclusion.md) → system removes the affected exclusions and informs the organizer.
- **4a./5b.** A draw already exists for the group → system warns that editing members invalidates the current draw and requires a re-draw before sharing.

## Exceptions

- **Local storage unavailable** → system reports the error and does not claim success.

## Non-functional notes

- **Offline/local-first:** fully works with no account and no network (see [NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md)).
- **Localization:** UI labels in DE/EN/FR; wishlist content is free text authored by the organizer and is not translated — see [NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md).
- **Privacy:** member names and wishlists stay on device; no backend is involved.
- **Performance:** add/edit/remove are instant (local writes).

## Open questions

- Wishlist item limit / links / images? (defer)

## Traceability

- Requirement: [FR-002](../../requirements/functional/FR-002-manage-members-and-wishlists.md); related [NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md)
- Glossary: [Member](../../glossary/member.md), [Wishlist](../../glossary/wishlist.md), [Group](../../glossary/group.md), [Exclusion](../../glossary/exclusion.md)
- Origin: [2026-07-24 brainstorming](../../brainstorming/2026-07-24-secret-santa-scope.md)
