---
id: UC-SS-004
title: Run the fair draw
app: Secret-Santa
actors: [Organizer]
status: Draft
priority: Must
related: [FR-004, NFR-001, NFR-002]
---

# UC-SS-004 — Run the fair draw

## Summary / goal

An organizer runs a fair random [Draw](../../glossary/draw.md) that produces one
[Assignment](../../glossary/assignment.md) per [Member](../../glossary/member.md) while honoring
all draw rules, then records the result in the group's draw history — all **offline, without an
account**.

## Primary actor & stakeholders

- **Primary actor:** Organizer.
- **Stakeholders:** Members (want a fair, valid, non-repeating result).

## Preconditions

- A group exists with at least two members.
- Any [Exclusions](../../glossary/exclusion.md) are defined (see [UC-SS-003](UC-SS-003-define-exclusions.md)).

## Postconditions

- **Success:** a complete set of assignments exists for the group and is recorded in the group's
  [draw history](../../glossary/draw.md); the previous draw (if any) is retained for the no-repeat rule.
- **Failure:** no assignments are produced; the organizer is told why (e.g. over-constrained).

## Trigger

The organizer selects **Run draw**.

## Main success scenario

1. Organizer selects **Run draw**.
2. System computes a random assignment where every member gives to exactly one member and receives from exactly one member.
3. System enforces the draw rules:
   - **(a) Exclusions** — no assignment violates a defined exclusion.
   - **(b) No reciprocal pairs** — if A→B then not B→A.
   - **(c) No repeat of the previous draw** — no giver keeps the same recipient as in the group's last recorded draw.
4. System records the result as the new current draw and appends it to the **draw history**.
5. System confirms the draw succeeded, ready to share reveal links (see [UC-SS-005](UC-SS-005-share-reveal-links.md)).

## Extensions / alternate flows

- **2a.** No previous draw exists → rule (c) does not apply on the first draw.
- **3a.** Group has exactly two members → a valid non-reciprocal draw is impossible; system explains this before attempting (two members can only give to each other).
- **4a.** Organizer re-runs the draw → the newly computed draw replaces the current one and is appended to history; the just-replaced draw becomes the "previous draw" for rule (c).

## Exceptions

- **No valid assignment exists** (over-constrained by exclusions, no-reciprocal, and/or no-repeat) → the draw **fails gracefully**: no partial result is saved, and the system explains the likely cause and suggests relaxing exclusions or adding members.
- **Local storage unavailable** → system reports the error and does not claim success.

## Non-functional notes

- **Offline/local-first:** the draw runs entirely on-device with no account or network (see [NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md)).
- **Localization:** result and failure messages available in DE/EN/FR.
- **Privacy:** assignments are computed and stored locally only; they are never sent to a server (see [NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md)). The full mapping is not shown to the group.
- **Performance:** the draw completes near-instantly for typical group sizes.

## Open questions

- Upper bound on retries/attempts before declaring "no valid draw"? (implementation detail — defer to feature spec)

## Traceability

- Requirement: [FR-004](../../requirements/functional/FR-004-run-fair-draw.md); related [NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md)
- Glossary: [Draw](../../glossary/draw.md), [Assignment](../../glossary/assignment.md), [Exclusion](../../glossary/exclusion.md), [Member](../../glossary/member.md), [Group](../../glossary/group.md)
- Origin: [2026-07-24 brainstorming](../../brainstorming/2026-07-24-secret-santa-scope.md)
