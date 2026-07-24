---
id: FR-006
title: Reveal page decodes a link client-side
type: functional
status: Draft
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

Given the recipient has an empty wishlist, or the group has no budget or event details
When the page displays the assignment
Then it omits the missing sections gracefully and still shows the recipient

Given a reveal link is malformed, truncated, or tampered with
When the page tries to decode it
Then it shows a clear "link could not be read" message
And it does not display a partial or guessed assignment
```

## Dependencies & related

- Derived from: [UC-SS-006](../../use-cases/secret-santa/UC-SS-006-open-reveal-link.md)
- Related: [FR-005](FR-005-generate-reveal-links.md), [NFR-001](../non-functional/NFR-001-local-first-offline.md), [NFR-002](../non-functional/NFR-002-assignment-privacy.md), [NFR-003](../non-functional/NFR-003-multilingual-de-en-fr.md)
- Glossary: [Reveal link](../../glossary/reveal-link.md), [Assignment](../../glossary/assignment.md), [Wishlist](../../glossary/wishlist.md), [Budget](../../glossary/budget.md), [Event details](../../glossary/event-details.md)
