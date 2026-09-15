---
id: FR-037
title: A member has a stable gift note (allergies / dietary / do-not-gift) shown to their giver
type: functional
status: Implemented
priority: Could
source: UC-SS-016
verification: test
related: [UC-SS-016, FR-002, FR-029, NFR-001, NFR-002, NFR-003, ADR-0014, ADR-0020, ADR-0042, ADR-0052]
---

# FR-037 — A member has a stable gift note shown to their giver

## Description

A [Member](../../glossary/member.md) may carry a **gift note** ([Gift note](../../glossary/gift-note.md)):
a short, free-text note the assigned giver sees — allergies, dietary or lifestyle needs, and things
**not** to buy. It is:

- **A stable per-person attribute**, held on the member — **not** per event like a
  [Wishlist](../../glossary/wishlist.md). One note serves every occasion's draw, so a recurring
  constraint (a nut allergy) is set once, never re-typed. This distinguishes it from the wishlist, which
  is per event ([ADR-0042](../../adr/0042-events-as-first-class-entities.md)), and from an
  [Exclusion](../../glossary/exclusion.md), which constrains **who draws whom**.
- **Organizer-authored** in v1 (it is roster data). A linked member self-authoring their own note (as
  they can their wishlist, [FR-030](FR-030-edit-my-wishlist.md)) is a parked extension.
- **Optional and capped.** Empty or whitespace means "no note". The length is bounded (500 characters)
  so the encoded reveal link stays within its size budget
  ([ADR-0014](../../adr/0014-reveal-link-encoding-and-privacy.md)); an over-long note is rejected.
- **Shown to the giver**, with the revealed giftee: on the account-free
  [Reveal link](../../glossary/reveal-link.md) (an **additive, optional** field `gn` in the
  self-contained payload, omitted when unset so links are unchanged and the payload version does not
  bump) and, for a **linked** giver, on their my-assignments view ([FR-029](FR-029-see-my-assignment.md))
  — the note travels with the assignment the organizer already publishes, stored in the assignment
  projection ([ADR-0020](../../adr/0020-server-side-assignment-privacy.md)).

It is a stable attribute that resolves through archival like the name
([ADR-0052](../../adr/0052-member-archival-and-permanent-draw-history.md)): a member archived after a
draw keeps their note, so a past occasion's reveal still shows it.

## Rationale

The DE/EN/FR app-store review scan
([competitor research, 2026-09-02](../../brainstorming/2026-09-02-secret-santa-competitor-research.md),
**CR-3**) found a repeated, cross-market wish — "please don't get me…", allergies, dietary and
lifestyle notes — that **no surveyed app does well**. Modelling it on the member (not the event) matches
how people think of it: a person's allergy is the same at Christmas and at a birthday, so it should be
set once and always shown, unlike the wishlist. It reuses the reveal payload and the assignment
projection, so it is a small, high-fit addition.

## Acceptance criteria

```gherkin
Given a member with a gift note "No nuts — allergy"
When a draw is run and revealed
Then whoever drew that member sees "No nuts — allergy" with their giftee
And a member with no note shows nothing extra, and the encoded link carries no note field

Given the same member is in two occasions
When each occasion is drawn
Then the note shows on both — it is not re-entered per occasion

Given the organizer clears a member's note
When they save
Then the note is removed everywhere it showed

Given a note longer than the 500-character cap
When the organizer saves
Then it is rejected and nothing is stored

Given the giver is a linked member reading their my-assignments
When the organizer has published the draw
Then the giver sees the recipient's gift note (and the budget range, FR-036) on their own device

Given a member is archived after a draw
When the past occasion is viewed
Then that member's note still shows on the kept draw's reveal
```

## Dependencies & related

- Derived from: [UC-SS-016](../../use-cases/secret-santa/UC-SS-016-member-gift-note.md)
- Related: [FR-002](FR-002-manage-members-and-wishlists.md) (members & wishlists),
  [FR-029](FR-029-see-my-assignment.md) (linked-member assignment view),
  [FR-036](FR-036-budget-minimum-and-maximum.md) (the budget range now travels the same projection),
  [NFR-002](../non-functional/NFR-002-assignment-privacy.md) (shown after the giftee is revealed)
- Governing ADR: [ADR-0014](../../adr/0014-reveal-link-encoding-and-privacy.md) (additive payload field),
  [ADR-0020](../../adr/0020-server-side-assignment-privacy.md) (the assignment projection carries it) —
  **no new ADR required**
- Implemented by: [FEAT-012](../../features/FEAT-012-member-gift-note.md), verified by
  [TP-012](../../features/TP-012-member-gift-note.md)
- Glossary: [Gift note](../../glossary/gift-note.md), [Member](../../glossary/member.md),
  [Wishlist](../../glossary/wishlist.md), [Reveal link](../../glossary/reveal-link.md)
