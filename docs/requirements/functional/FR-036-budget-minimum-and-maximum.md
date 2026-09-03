---
id: FR-036
title: Gift budget supports an optional minimum as well as a maximum
type: functional
status: Implemented
priority: Could
source: UC-SS-015
verification: test
related: [UC-SS-015, FR-001, FR-034, NFR-001, NFR-003, ADR-0014, ADR-0042, ADR-0056]
---

# FR-036 — Gift budget supports an optional minimum as well as a maximum

## Description

An [Event](../../glossary/event.md)'s [Budget](../../glossary/budget.md) is a spending **maximum** plus
one currency (today's behaviour). This requirement adds an **optional minimum** (lower bound) to the
same budget, in the same currency:

- The **maximum** stays required for a budget to exist; clearing it removes the whole budget.
- The **minimum** is optional. When set it must satisfy `0 ≤ minimum ≤ maximum`. A blank, zero or
  negative minimum means "no minimum" — a bare maximum, exactly as before.
- The budget shows as a **range** ("10–30 CHF") wherever it is displayed — the public
  [Reveal link](../../glossary/reveal-link.md) and a linked member's own assignment — when a minimum is
  set, and as the bare maximum ("30 CHF") when it is not.
- The minimum travels in the existing self-contained reveal payload
  ([ADR-0014](../../adr/0014-reveal-link-encoding-and-privacy.md)) as one **additive, optional** field
  (`bm`). It is **omitted from the encoded link when unset**, so links written before this field
  existed, and every max-only link, are byte-for-byte unchanged and the payload version does not bump.

It is a **local, account-free** operation on the one `Group` document
([ADR-0042](../../adr/0042-events-as-first-class-entities.md)); no server or sync change. The budget is
already **per event** ([FR-034](FR-034-multiple-events-per-group.md)), so the range is per event too.

## Rationale

The DE/EN/FR app-store review scan
([competitor research, 2026-09-02](../../brainstorming/2026-09-02-secret-santa-competitor-research.md),
**CR-2**) found that **every** surveyed app offers only a maximum price, while a recurring wish is a
**minimum** too — so a participant can't cheap-out and undercut the exchange. It is a small, universal
gap: the event already holds a budget amount and currency, so a lower bound is a cheap, high-fit
addition that closes a complaint no competitor addresses.

## Acceptance criteria

```gherkin
Given an event with no budget
When the organizer sets a maximum of 30 CHF and a minimum of 10 CHF and saves
Then the event's budget is 10–30 CHF
And each reveal link and the linked member's assignment show "10–30 CHF"

Given an event with a maximum of 30 CHF and no minimum
When a reveal link is generated
Then it shows "30 CHF" with no range, and the encoded link carries no minimum field

Given the organizer enters a minimum greater than the maximum
When they save
Then the save is rejected with an inline error and nothing is stored

Given the organizer enters a zero or blank minimum with a valid maximum
When they save
Then the budget is stored as a bare maximum (no minimum)

Given the organizer clears the maximum
When they save
Then the whole budget is removed, minimum included

Given a group document saved before the minimum existed
When it is loaded
Then its budget reads as a bare maximum (minimum absent), unchanged
```

## Dependencies & related

- Derived from: [UC-SS-015](../../use-cases/secret-santa/UC-SS-015-budget-minimum-and-maximum.md)
- Related: [FR-001](FR-001-create-secret-santa-group.md) (budget first defined),
  [FR-034](FR-034-multiple-events-per-group.md) (budget is per event),
  [NFR-003](../non-functional/NFR-003-multilingual-de-en-fr.md) (DE/EN/FR labels + range display),
  [ADR-0056](../../adr/0056-decouple-ui-language-from-formatting-region.md) (amount formatting by region)
- Governing ADR: [ADR-0014](../../adr/0014-reveal-link-encoding-and-privacy.md) (reveal payload; the
  minimum is an additive, version-compatible field — **no new ADR required**),
  [ADR-0042](../../adr/0042-events-as-first-class-entities.md) (budget is per-event, local)
- Implemented by: [FEAT-011](../../features/FEAT-011-budget-minimum-and-maximum.md), verified by
  [TP-011](../../features/TP-011-budget-minimum-and-maximum.md)
- Glossary: [Budget](../../glossary/budget.md), [Reveal link](../../glossary/reveal-link.md)
