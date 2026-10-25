---
id: FR-030
title: Keep my own wishlist and gift note
type: functional
status: Implemented
priority: Should
source: UC-SS-012
verification: test
related: [UC-SS-012, UC-SS-011, FR-024, FR-037, FR-050, NFR-001, ADR-0086, FEAT-027]
---

# FR-030 — Keep my own wishlist and gift note

> **Rewritten 2026-10-25 for [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)** ([FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)): one way to be in a group. The previous version described the four-kind storage model (on this device, shared by link, my account, my account · shared) and is in the git history.

## Description

A [participant](../../glossary/participant.md) keeps their own wishlist per event and their gift note on their participant page
— their [member entry](../../glossary/member-entry.md):

- Saving sends it to the server; offline it is kept on the device and sent on the next sync
  ([FR-024](FR-024-offline-editing-and-reconciliation.md)).
- Their giver sees it at once, on the giver's participant page and reveal link.
- The organizer sees it in the event's wishlists, marked as kept by the member, and cannot overwrite it while the
  person takes part. If the person leaves or is freed, the entry stays and the organizer can edit it again.

An organizer who is also in the roster edits their own entry in the event's wishlists, like everyone else's.

## Rationale

The person who wants something knows it best; keeping it in one copy that every view reads means the organizer and
the giver never see an older version.

## Acceptance criteria

```gherkin
Given a participant saves a wishlist for an event
When their giver opens their pairing
Then the giver sees that wishlist

Given a participant's entry
When the organizer opens the event's wishlists
Then the entry is shown as kept by the member and is not editable there

Given a participant saves while offline
When the connection returns
Then the entry is sent and the page no longer says it is pending
```

## Dependencies & related

- Derived from: [UC-SS-012](../../use-cases/secret-santa/UC-SS-012-edit-my-wishlist.md)
- Related: [FR-037](FR-037-member-gift-note.md), [FR-050](FR-050-edit-own-entry-from-reveal-link.md),
  [FR-024](FR-024-offline-editing-and-reconciliation.md)
- Governing ADR: [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)
- Implemented by: [FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)
