---
id: UC-SS-003
title: Define exclusions between members
app: Secret-Santa
actors: [Organizer]
status: Draft
priority: Must
related: [FR-003, FR-004, NFR-001]
---

# UC-SS-003 — Define exclusions between members

## Summary / goal

An organizer defines [Exclusions](../../glossary/exclusion.md) within a
[Group](../../glossary/group.md) — one-directional "giver X must not give to recipient Y" rules —
so the [Draw](../../glossary/draw.md) avoids unwanted [Assignments](../../glossary/assignment.md)
(e.g. partners or same-household members).

## Primary actor & stakeholders

- **Primary actor:** Organizer.
- **Stakeholders:** Members (want the draw to respect real-life constraints).

## Preconditions

- A group exists with at least two members (see [UC-SS-002](UC-SS-002-manage-members.md)).

## Postconditions

- **Success:** the group's exclusion set reflects the organizer's changes in local storage.
- **Failure:** no change is persisted; the organizer sees why.

## Trigger

The organizer opens a group and manages its exclusions.

## Main success scenario

1. Organizer opens a group's exclusions.
2. Organizer selects a **giver** and a **recipient** that the giver must not be matched to.
3. System records the one-directional exclusion.
4. Organizer optionally adds the reverse direction as a second exclusion, or removes an exclusion.
5. System validates the change and **saves it to local storage**.

## Extensions / alternate flows

- **2a.** Giver and recipient are the same member → system rejects it (a member never gives to themselves; that is implicit, not an exclusion).
- **3a.** The exclusion already exists → system ignores the duplicate.
- **4a.** The organizer wants a mutual exclusion → the system offers to add both directions in one step.
- **5a.** Exclusions leave no valid draw possible → the exclusion is still saved, but the system warns that the draw will fail until constraints are relaxed (see [UC-SS-004](UC-SS-004-run-draw.md)).

## Exceptions

- **Local storage unavailable** → system reports the error and does not claim success.

## Non-functional notes

- **Offline/local-first:** fully works with no account and no network (see [NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md)).
- **Localization:** all labels available in DE/EN/FR.
- **Privacy:** exclusions stay on device; no backend is involved.
- **Performance:** add/remove are instant (local writes).

## Open questions

- Should household/partner grouping auto-generate mutual exclusions? (defer)

## Traceability

- Requirement: [FR-003](../../requirements/functional/FR-003-define-exclusions.md); feeds [FR-004](../../requirements/functional/FR-004-run-fair-draw.md)
- Glossary: [Exclusion](../../glossary/exclusion.md), [Member](../../glossary/member.md), [Group](../../glossary/group.md), [Draw](../../glossary/draw.md)
- Origin: [2026-07-24 brainstorming](../../brainstorming/2026-07-24-secret-santa-scope.md)
