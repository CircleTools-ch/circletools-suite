---
term: Budget
contexts: [Secret-Santa]
status: Draft
---

# Budget

## Definition

The agreed spending guidance for gifts in an [Event](event.md), expressed as a **maximum** amount and a
currency, with an **optional minimum** (lower bound) in the same currency — so a budget may be a single
limit ("up to 30 CHF") or a **range** ("10–30 CHF"). A budget is optional and belongs to exactly one
event ([FR-034](../requirements/functional/FR-034-multiple-events-per-group.md)); the maximum is required
for a budget to exist and the minimum, when set, satisfies `0 ≤ minimum ≤ maximum`
([FR-036](../requirements/functional/FR-036-budget-minimum-and-maximum.md)). It is carried into each
[Reveal link](reveal-link.md) so every giver sees the same spending guidance.

## Bounded context(s)

- **Secret-Santa:** one budget per event; amounts and currency are formatted for the active region
  ([ADR-0056](../adr/0056-decouple-ui-language-from-formatting-region.md)), and the range shows with an
  en-dash when a minimum is set — see
  [NFR-003](../requirements/non-functional/NFR-003-multilingual-de-en-fr.md).

## Synonyms / do-not-use

- **Synonyms:** spending limit, price limit.
- **Do NOT use:** "price" (that is a gift's cost, not the group limit), "cost".

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | Budget |
| EN | Budget |
| FR | Budget |

## Related terms

- [Group](group.md), [Reveal link](reveal-link.md), [Event details](event-details.md)

## First appearance

- [UC-SS-001](../use-cases/secret-santa/UC-SS-001-create-group.md)
