---
id: FR-050
title: Edit my own wishlist and gift note from my reveal link
type: functional
status: Implemented
priority: Should
source: UC-SS-006, backlog BTP-11 (2026-10-17 browser pass)
verification: test
related: [UC-SS-006, FR-006, FR-030, FR-037, NFR-002, NFR-009, ADR-0068, ADR-0080, ADR-0084, FEAT-020, FEAT-026]
---

# FR-050 — Edit my own wishlist and gift note from my reveal link

## Description

A person who opens their [Reveal link](../../glossary/reveal-link.md) for a group whose reveal links are
live (*Shared by link* or *My account · shared*, [ADR-0068](../../adr/0068-live-reveal-links-for-backend-backed-groups.md))
must be able to view and change, on the reveal page and with no account, **their own**
[Wishlist](../../glossary/wishlist.md) for that link's [Event](../../glossary/event.md) and **their own**
gift note. The change is saved as they type (no Save button) and becomes theirs to maintain: the
organizer's copy of those fields turns read-only. Groups without live reveal links show no editor.

## Rationale

The reveal page told every recipient to send their wishes to the organizer, who then retyped them — the
2026-10-17 browser pass flagged this as a dead end. The reveal token already proves who the holder is
([ADR-0084](../../adr/0084-own-entry-from-the-reveal-link.md)).

## Acceptance criteria

```gherkin
Given a Shared-by-link group with a current draw and a published live reveal link for Dario
When Dario opens his reveal link online
Then he sees his current wishlist for that event and his gift note, editable — already before he reveals
whom he drew, since it is about him and gives nothing away

Given Dario types "Lego" into his wishlist and pauses or leaves the field
Then it is saved and the page says so, with no Save button
And the organizer's device shows "Lego" for Dario after its next sync, read-only
And Dario's Secret Santa sees "Lego" on their live reveal link after the organizer's device has synced

Given Dario's change could not be sent (offline)
Then the text stays in the field, the page says it is not saved yet, and the next change or leaving the field retries

Given a link for an on-this-device or private My-account group, or a link opened offline
Then no editor is shown and the page keeps the hint to tell the organizer

Given the member is linked to an account
Then no editor is shown on the reveal page (they use "My wishlists", FR-030)

Given the organizer releases Dario
Then Dario's entry is deleted and the organizer can edit those fields again

Given a reveal token
Then it can change only its own member's gift note and only its own event's wishlist — never another
member, another event, a claim or the draw
```

## Dependencies & related

- Derived from: [UC-SS-006](../../use-cases/secret-santa/UC-SS-006-open-reveal-link.md)
- Related: [FR-030](FR-030-edit-my-wishlist.md), [NFR-002](../non-functional/NFR-002-assignment-privacy.md),
  [ADR-0080](../../adr/0080-participant-access-to-shared-groups.md), [FEAT-026](../../features/FEAT-026-own-entry-from-the-reveal-link.md)
