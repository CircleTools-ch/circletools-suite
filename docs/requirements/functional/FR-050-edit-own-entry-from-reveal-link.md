---
id: FR-050
title: Keep my own wishlist and gift note from my reveal link
type: functional
status: Implemented
priority: Should
source: UC-SS-006, backlog BTP-11 (2026-10-17 browser pass)
verification: test
related: [UC-SS-006, FR-006, FR-030, FR-037, NFR-002, NFR-009, ADR-0084, ADR-0086, FEAT-026, FEAT-027]
---

# FR-050 — Keep my own wishlist and gift note from my reveal link

> **Rewritten 2026-10-25 for [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)** ([FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)): one way to be in a group. The previous version described the four-kind storage model (on this device, shared by link, my account, my account · shared) and is in the git history.

## Description

The reveal link of an **online group** — a short link the server answers — lets its holder keep their own wishlist
for that event and their gift note, below their pairing, as their [member entry](../../glossary/member-entry.md):

- It saves when a field loses focus, and says so.
- Their giver sees it at once, on the giver's link or participant page; the organizer sees it in the event's
  wishlists, marked as kept by the member.
- It is offered only for a member **without a participant**. A member who takes part keeps their entry on their
  participant page ([FR-030](FR-030-edit-my-wishlist.md)), so there is one place for it.
- The reveal link of a group kept on this device is a snapshot and offers no editing.

The input is bounded like every account-free write ([NFR-009](../non-functional/NFR-009-account-free-write-abuse-resistance.md)).

## Rationale

Many people only ever open their reveal link. Letting them write their wishes there reaches the people who will
never install the app or take part.

## Acceptance criteria

```gherkin
Given the reveal link of an online group for a member without a participant
When the holder writes a wishlist and leaves the field
Then the page says it is saved, and the giver's reveal page shows it

Given a member with a participant
When their reveal link is opened
Then it shows the pairing without an editor

Given the reveal link of a group kept on this device
When it is opened
Then it shows the snapshot without an editor
```

## Dependencies & related

- Related: [FR-006](FR-006-reveal-page-decode.md), [FR-030](FR-030-edit-my-wishlist.md), [FR-037](FR-037-member-gift-note.md),
  [NFR-009](../non-functional/NFR-009-account-free-write-abuse-resistance.md)
- Governing ADRs: [ADR-0084](../../adr/0084-own-entry-from-the-reveal-link.md), [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)
- Implemented by: [FEAT-026](../../features/FEAT-026-own-entry-from-the-reveal-link.md), [FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)
