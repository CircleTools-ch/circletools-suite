---
id: FR-005
title: Generate self-contained reveal links
type: functional
status: Implemented
priority: Must
source: UC-SS-005
verification: test
related: [UC-SS-005, FR-004, FR-006, NFR-001, NFR-002]
---

# FR-005 — Generate self-contained reveal links

## Description

After a successful [Draw](../../glossary/draw.md), the Secret-Santa app must generate **one**
[Reveal link](../../glossary/reveal-link.md) per [Member](../../glossary/member.md). Each link must
**encode the full per-giver payload into the link itself** — the recipient's name, the recipient's
[Wishlist](../../glossary/wishlist.md), the group [Budget](../../glossary/budget.md), and the
[Event details](../../glossary/event-details.md) — using a URL-safe encoding. Generation must be
entirely on-device: **nothing is uploaded to or stored on a server**. The app must offer to copy a
link, render it as a QR code, or hand it to a messaging app for out-of-band distribution.

Per [ADR-0014](../../adr/0014-reveal-link-encoding-and-privacy.md), the payload is carried in the URL
**fragment** (never sent to a server) and compressed + Base64Url-encoded so it is not casually
readable; the **only human-readable token is the owner label** (`for=<giver>`) so the organizer can
route each link, while the recipient (giftee) stays in the cryptic encoded part.

## Rationale

Self-contained links deliver a private, cross-device reveal with no backend and no account, which
is the chosen reveal mechanism for the MVP. Because the payload lives in the link, the link **is**
the secret — one link per giver keeps each assignment private.

## Acceptance criteria

```gherkin
Given a group with a successful current draw
When the user generates reveal links
Then the app produces exactly one distinct link per member
And each link encodes that giver's recipient, the recipient's wishlist, the group budget, and the event details

Given a generated reveal link
When the organizer inspects the URL
Then the giver the link is "for" is human-readable
And the recipient (giftee) and their wishlist are not readable from the URL

Given the user generates reveal links
When the links are produced
Then no assignment data is sent to or stored on any server

Given a generated reveal link
When the user chooses to share it
Then the app can copy it, show it as a QR code, or pass it to a messaging app

Given the group's draw is stale after member or exclusion edits, or no draw exists
When the user attempts to generate reveal links
Then generation is blocked and the user is prompted to run or re-run the draw
```

## Amendment (2026-10-16) — share message and short links

From the [2026-10-16 two-account test](../../brainstorming/2026-10-16-secret-santa-two-account-device-test.md) (Q32), decided in [ADR-0082](../../adr/0082-short-reveal-links-for-online-groups.md):

- [ ] Sharing a link sends a message with context: a greeting with the person's name, the group and
      occasion, date, place and budget (one budget format), "only you see who you got", and the link on its
      own last line — DE/EN/FR.
- [ ] For an online group the link is short: `…/r#for=<name>&rt=<token>`; the server answers it from the
      group's current state (ADR-0086). For a group kept on this device the long link is handed out as before.

## Dependencies & related

- Derived from: [UC-SS-005](../../use-cases/secret-santa/UC-SS-005-share-reveal-links.md)
- Related: [FR-004](FR-004-run-fair-draw.md), [FR-006](FR-006-reveal-page-decode.md), [NFR-001](../non-functional/NFR-001-local-first-offline.md), [NFR-002](../non-functional/NFR-002-assignment-privacy.md)
- Encoding & privacy are specified in [ADR-0014](../../adr/0014-reveal-link-encoding-and-privacy.md).
- Glossary: [Reveal link](../../glossary/reveal-link.md), [Assignment](../../glossary/assignment.md), [Wishlist](../../glossary/wishlist.md), [Budget](../../glossary/budget.md), [Event details](../../glossary/event-details.md), [Member](../../glossary/member.md)
