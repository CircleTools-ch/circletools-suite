---
term: Exclusion
contexts: [Secret-Santa]
status: Draft
---

# Exclusion

## Definition

A rule that forbids a particular [Assignment](assignment.md) — i.e. a giver who must **not** be
matched to a specific recipient (e.g. partners or people in the same household who buy gifts
together). Exclusions constrain the [Draw](draw.md).

## Bounded context(s)

- **Secret-Santa:** defined per [Group](group.md) before drawing; the draw must satisfy all
  exclusions or report that no valid draw exists.

## Synonyms / do-not-use

- **Synonyms:** restriction, constraint, "no-match".
- **Do NOT use:** "ban", "block" (harsh; reserved for other meanings).

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | Ausschluss |
| EN | Exclusion |
| FR | Exclusion |

## Related terms

- [Draw](draw.md), [Assignment](assignment.md), [Member](member.md)

## First appearance

- [UC-SS-001](../use-cases/secret-santa/UC-SS-001-create-group.md)
