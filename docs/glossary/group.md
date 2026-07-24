---
term: Group
contexts: [Secret-Santa]
status: Draft
---

# Group

## Definition

A set of participants who take part in the same Secret-Santa gift exchange. A group owns its
[Members](member.md) (each with a [Wishlist](wishlist.md)), its [Exclusions](exclusion.md), an
optional [Budget](budget.md) and [Event details](event-details.md), and — once run — a
[Draw](draw.md) together with a **draw history** of previous draws. It is the primary aggregate of
the Secret-Santa app.

## Bounded context(s)

- **Secret-Santa:** the organizing unit; created and managed by an organizer, and the scope that
  carries budget and event details into each [Reveal link](reveal-link.md).

## Synonyms / do-not-use

- **Synonyms:** circle, round (informal).
- **Do NOT use:** "team", "party" (ambiguous in code).

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | Gruppe |
| EN | Group |
| FR | Groupe |

## Related terms

- [Member](member.md), [Draw](draw.md), [Exclusion](exclusion.md), [Budget](budget.md), [Event details](event-details.md), [Wishlist](wishlist.md)

## First appearance

- [UC-SS-001](../use-cases/secret-santa/UC-SS-001-create-group.md)
