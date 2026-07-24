---
id: NFR-002
title: Assignment privacy — the link is the secret
type: non-functional
status: Draft
priority: Must
source: UC-SS-004, UC-SS-005, UC-SS-006
verification: review
related: [UC-SS-004, UC-SS-005, UC-SS-006, FR-004, FR-005, FR-006, NFR-001]
---

# NFR-002 — Assignment privacy — the link is the secret

## Description

A member must be able to learn **only their own** [Assignment](../../glossary/assignment.md), and no
[Draw](../../glossary/draw.md) result may be stored on any server. Each
[Reveal link](../../glossary/reveal-link.md) must carry exactly one giver's payload; opening it must
reveal only that giver's recipient, wishlist, budget, and event details. The full giver→recipient
mapping is never shown to the group, and assignments exist only in the organizer's local storage and
inside the individual links — **the link is the secret**.

## Rationale

The surprise and fairness of a gift exchange depend on each giver seeing only their own recipient.
Keeping assignments off any server, and scoping each link to a single giver, preserves that secrecy
without accounts or trusted infrastructure.

## Acceptance criteria

```gherkin
Given a set of generated reveal links
When any single link is decoded
Then it exposes exactly one giver's assignment and no other member's assignment

Given the draw has run and links are generated and shared
When servers and network traffic are inspected
Then no giver→recipient assignment is stored server-side or transmitted to a server

Given the reveal page for one member's link
When it is displayed
Then it never shows the full group mapping or any other member's recipient
```

## Quality attribute *(NFR only)*

- **Attribute:** privacy / confidentiality.
- **Metric & target:** each reveal link decodes to exactly **one** giver's assignment (0 leakage of
  other assignments); **zero** assignments stored server-side.
- **Measurement approach:** decoding one member's link in tests yields only that member's payload;
  code/data review and a network-capture check confirm no server-side assignment storage or
  transmission. Tamper handling is specified in the reveal-link ADR (see [ADR backlog](../../adr/index.md)).

## Dependencies & related

- Derived from: [UC-SS-004](../../use-cases/secret-santa/UC-SS-004-run-draw.md), [UC-SS-005](../../use-cases/secret-santa/UC-SS-005-share-reveal-links.md), [UC-SS-006](../../use-cases/secret-santa/UC-SS-006-open-reveal-link.md)
- Related: [FR-004](../functional/FR-004-run-fair-draw.md), [FR-005](../functional/FR-005-generate-reveal-links.md), [FR-006](../functional/FR-006-reveal-page-decode.md), [NFR-001](NFR-001-local-first-offline.md)
- Glossary: [Assignment](../../glossary/assignment.md), [Reveal link](../../glossary/reveal-link.md), [Draw](../../glossary/draw.md)
