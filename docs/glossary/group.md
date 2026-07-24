---
term: Group
contexts: [Secret-Santa]
status: Draft
---

# Group

## Definition

A set of participants who take part in the same Secret-Santa gift exchange. A group owns its
members, exclusions, and (once run) a draw. It is the primary aggregate of the Secret-Santa app.

## Bounded context(s)

- **Secret-Santa:** the organizing unit; created and managed by an organizer.

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

- [Member](member.md), [Draw](draw.md), [Exclusion](exclusion.md)

## First appearance

- [UC-SS-001](../use-cases/secret-santa/UC-SS-001-create-group.md)
