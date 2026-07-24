---
term: Event details
contexts: [Secret-Santa]
status: Draft
---

# Event details

## Definition

The practical information about a [Group](group.md)'s gift-exchange occasion — its **date**,
**location**, and free-text **notes**. Event details belong to exactly one group and are optional.
They are carried into each [Reveal link](reveal-link.md) so every giver knows when and where the
exchange happens.

## Bounded context(s)

- **Secret-Santa:** one set of event details per group; the date is formatted for the active
  culture (`de-CH` default) — see [NFR-003](../requirements/non-functional/NFR-003-multilingual-de-en-fr.md).

## Synonyms / do-not-use

- **Synonyms:** event info, occasion details.
- **Do NOT use:** "meeting", "appointment" (imply calendar semantics not modelled here).

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | Veranstaltungsdetails |
| EN | Event details |
| FR | Détails de l'événement |

## Related terms

- [Group](group.md), [Budget](budget.md), [Reveal link](reveal-link.md)

## First appearance

- [UC-SS-001](../use-cases/secret-santa/UC-SS-001-create-group.md)
