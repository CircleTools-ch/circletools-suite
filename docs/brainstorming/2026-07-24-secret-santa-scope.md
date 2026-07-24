---
date: 2026-07-24
facilitator: Adrian
participants: [Adrian]
topic: Secret-Santa MVP scope
status: Draft
---

# Brainstorming — Secret-Santa MVP scope (2026-07-24)

> **Example session** created with the harness to demonstrate the template chain
> (brainstorming → use case → requirement). Content is illustrative, not final.

## Objective / framing question

What is the smallest Secret-Santa app that is genuinely useful **offline**, and delightful enough
to build a suite around?

## Divergent — raw ideas

- Create a group; add members by name.
- Draw names fairly; each gives once, receives once.
- Exclusions (partners can't draw each other).
- Each giver privately sees only their own recipient.
- Wish lists / gift ideas per member.
- Budget per group.
- Re-draw until "shared/locked".
- Share results via link when online.
- Reminders before the event.
- Multiple groups (family + friends).

## Clustering / themes

- **Core draw:** group, members, exclusions, draw, private assignment.
- **Nice-to-have:** wish lists, budget, reminders.
- **Needs backend:** sharing via link, cross-device reveal.

## Convergent — selection

| Idea | Impact | Effort (low=better) | Fit | Decision |
|------|:------:|:-------------------:|:---:|----------|
| Group + members | 5 | 5 | 5 | **Selected (MVP)** |
| Exclusions | 4 | 3 | 5 | **Selected (MVP)** |
| Draw + private assignment | 5 | 3 | 5 | **Selected (MVP)** |
| Wish lists | 3 | 3 | 4 | Parked (v2) |
| Budget | 2 | 4 | 3 | Parked (v2) |
| Share via link | 4 | 2 | 4 | Parked (needs account/sync) |

## Decisions & selected ideas

- **MVP = offline core:** create group → add members → set exclusions → draw → each giver sees
  their private assignment. No account required.

## Rejected / parked (for the record)

- Wish lists, budgets, reminders → v2.
- Link-sharing & cross-device reveal → after account/sync lands.

## Follow-up actions

- [x] Use case for group creation → [UC-SS-001](../use-cases/secret-santa/UC-SS-001-create-group.md)
- [x] Requirement for offline group creation → [FR-001](../requirements/functional/FR-001-create-secret-santa-group.md)
- [ ] Use cases for members, exclusions, and draw (next)
