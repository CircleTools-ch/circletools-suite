---
id: FR-005
title: Generate self-contained reveal links
type: functional
status: Draft
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

## Dependencies & related

- Derived from: [UC-SS-005](../../use-cases/secret-santa/UC-SS-005-share-reveal-links.md)
- Related: [FR-004](FR-004-run-fair-draw.md), [FR-006](FR-006-reveal-page-decode.md), [NFR-001](../non-functional/NFR-001-local-first-offline.md), [NFR-002](../non-functional/NFR-002-assignment-privacy.md)
- A reveal-link **encoding & privacy/tamper** ADR is to be recorded at implementation time — see [ADR backlog](../../adr/index.md).
- Glossary: [Reveal link](../../glossary/reveal-link.md), [Assignment](../../glossary/assignment.md), [Wishlist](../../glossary/wishlist.md), [Budget](../../glossary/budget.md), [Event details](../../glossary/event-details.md), [Member](../../glossary/member.md)
