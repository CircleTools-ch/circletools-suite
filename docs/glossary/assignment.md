---
term: Assignment
contexts: [Secret-Santa]
status: Draft
---

# Assignment

## Definition

A single giver→recipient pairing produced by a [Draw](draw.md): one [Member](member.md) gives a
gift to exactly one other member. Each member is a giver once and a recipient once.

## Bounded context(s)

- **Secret-Santa:** the private result each giver sees ("you have X"); the full mapping is not
  revealed to the group.

## Synonyms / do-not-use

- **Synonyms:** pairing, match.
- **Do NOT use:** "target" (unfriendly), "gift" (that's the present, not the pairing).

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | Zuteilung |
| EN | Assignment |
| FR | Attribution |

## Related terms

- [Draw](draw.md), [Member](member.md)

## First appearance

- [UC-SS-001](../use-cases/secret-santa/UC-SS-001-create-group.md)
