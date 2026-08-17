---
term: Exclusion
contexts: [Secret-Santa]
status: Draft
---

# Exclusion

## Definition

A rule between two [Member](member.md)s that forbids [Assignment](assignment.md)s in one or both
directions. It is either **one-way** — "giver X must **not** give to recipient Y" — or **mutual**,
forbidding both directions so neither member is ever assigned the other (the usual choice for a
couple or people in the same household who buy gifts together). Mutual is the default when adding an
exclusion. Each member pair carries a single exclusion rule, so reconfiguring a pair replaces the
previous rule rather than adding a second. Exclusions constrain the [Draw](draw.md).

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
