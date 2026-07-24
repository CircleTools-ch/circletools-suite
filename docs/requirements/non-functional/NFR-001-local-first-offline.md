---
id: NFR-001
title: Local-first and fully offline (no account or backend for MVP)
type: non-functional
status: Draft
priority: Must
source: UC-SS-001, UC-SS-004, UC-SS-006
verification: test
related: [UC-SS-001, UC-SS-002, UC-SS-003, UC-SS-004, UC-SS-005, UC-SS-006, FR-001, FR-004, FR-005, FR-006]
---

# NFR-001 — Local-first and fully offline (no account or backend for MVP)

## Description

Every Secret-Santa MVP flow — creating and configuring a [Group](../../glossary/group.md), managing
[Members](../../glossary/member.md) and [Wishlists](../../glossary/wishlist.md), defining
[Exclusions](../../glossary/exclusion.md), running the [Draw](../../glossary/draw.md), generating
[Reveal links](../../glossary/reveal-link.md), and opening the reveal page — must work with **no
account and no network connection**. All application state is held in **local storage**; the MVP
has no backend dependency.

## Rationale

Local-first is core quality goal Q1: the app must be usable anywhere, instantly, and privately. It
is also what makes the self-contained-link reveal possible without a server.

## Acceptance criteria

```gherkin
Given the device has no network connection and the user has no account
When the user creates a group, adds members and wishlists, sets exclusions, runs the draw, and generates reveal links
Then all operations succeed and their results are persisted in local storage

Given the device is offline
When a member opens a reveal link
Then the reveal page decodes and displays the assignment without any network request

Given any MVP flow is exercised
When network traffic is inspected
Then no request is made to obtain an account, or to store or fetch group, draw, or assignment data
```

## Quality attribute *(NFR only)*

- **Attribute:** reliability / availability (local-first, offline-capable).
- **Metric & target:** 100% of MVP flows complete with the network disabled and no account signed
  in; **zero** outbound requests carry group, member, draw, or assignment data.
- **Measurement approach:** automated tests run with networking disabled; a network-capture check
  in CI asserts no data-bearing outbound requests during MVP flows.

## Dependencies & related

- Derived from: [UC-SS-001](../../use-cases/secret-santa/UC-SS-001-create-group.md), [UC-SS-004](../../use-cases/secret-santa/UC-SS-004-run-draw.md), [UC-SS-006](../../use-cases/secret-santa/UC-SS-006-open-reveal-link.md)
- Related: [FR-001](../functional/FR-001-create-secret-santa-group.md), [FR-004](../functional/FR-004-run-fair-draw.md), [FR-005](../functional/FR-005-generate-reveal-links.md), [FR-006](../functional/FR-006-reveal-page-decode.md), [NFR-002](NFR-002-assignment-privacy.md)
- Related quality goal: Q1 (local-first) — [arc42 ch. 1](../../arc42/01-introduction-and-goals.md)
