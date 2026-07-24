---
term: Budget
contexts: [Secret-Santa]
status: Draft
---

# Budget

## Definition

The agreed spending limit for gifts in a [Group](group.md), expressed as an amount and a currency.
A budget belongs to exactly one group and is optional. It is carried into each
[Reveal link](reveal-link.md) so every giver sees the same spending guidance.

## Bounded context(s)

- **Secret-Santa:** one budget per group; amount and currency are formatted for the active culture
  (`de-CH` default) — see [NFR-003](../requirements/non-functional/NFR-003-multilingual-de-en-fr.md).

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
