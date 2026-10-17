---
id: FR-006
title: Reveal page decodes a link client-side
type: functional
status: Implemented
priority: Must
source: UC-SS-006
verification: test
related: [UC-SS-006, FR-005, NFR-001, NFR-002, NFR-003]
---

# FR-006 — Reveal page decodes a link client-side

## Description

The Secret-Santa reveal page must **decode a [Reveal link](../../glossary/reveal-link.md) entirely
on the client** and display the giver's [Assignment](../../glossary/assignment.md): the recipient's
name, the recipient's [Wishlist](../../glossary/wishlist.md), the group
[Budget](../../glossary/budget.md), and the [Event details](../../glossary/event-details.md). The
page must not make any server request to obtain the assignment, and must handle malformed or
tampered links by failing cleanly rather than showing a partial or incorrect assignment.

Per [ADR-0014](../../adr/0014-reveal-link-encoding-and-privacy.md), on load the page shows a
**summary** (greeting to the named giver, budget, event details) but keeps the **recipient's name
and wishlist hidden behind a "Reveal" button**, so an accidental open does not spoil the result.

## Rationale

Client-side decoding completes the account-free, backend-free reveal: the assignment never leaves
the device that generated the link except inside the link the giver already holds. Clean failure
protects the giver from acting on a wrong or corrupted assignment.

## Acceptance criteria

```gherkin
Given a member opens a valid reveal link
When the page loads
Then it decodes the payload in the browser without any server request for the assignment
And it displays the recipient, the recipient's wishlist, the group budget, and the event details

Given a member opens a valid reveal link while offline
When the page loads
Then it still decodes and displays the assignment

Given a member opens a valid reveal link
When the page loads
Then the budget and event details are shown but the recipient and wishlist are hidden
When the member taps "Reveal"
Then the recipient and the recipient's wishlist are shown

Given the recipient has an empty wishlist, or the group has no budget or event details
When the page displays the assignment
Then it omits the missing sections gracefully and still shows the recipient

Given a reveal link is malformed, truncated, or tampered with
When the page tries to decode it
Then it shows a clear "link could not be read" message
And it does not display a partial or guessed assignment
```

## Amendment (2026-10-16) — opening a short link

From the [2026-10-16 two-account test](../../brainstorming/2026-10-16-secret-santa-two-account-device-test.md) (Q32b, [ADR-0082](../../adr/0082-short-reveal-links-for-online-groups.md)):

- [ ] A short link fetches the giver's slice; the slice is kept on the device so the link reopens offline.
- [ ] Opened offline before it was ever fetched, the page says the link needs a connection once, with
      "Try again" — not that the link is invalid.

## Dependencies & related

- Derived from: [UC-SS-006](../../use-cases/secret-santa/UC-SS-006-open-reveal-link.md)
- Related: [FR-005](FR-005-generate-reveal-links.md), [NFR-001](../non-functional/NFR-001-local-first-offline.md), [NFR-002](../non-functional/NFR-002-assignment-privacy.md), [NFR-003](../non-functional/NFR-003-multilingual-de-en-fr.md)
- Decision: [ADR-0014](../../adr/0014-reveal-link-encoding-and-privacy.md) (reveal-link encoding & privacy)
- Glossary: [Reveal link](../../glossary/reveal-link.md), [Assignment](../../glossary/assignment.md), [Wishlist](../../glossary/wishlist.md), [Budget](../../glossary/budget.md), [Event details](../../glossary/event-details.md)
