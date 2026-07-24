---
term: Draw
contexts: [Secret-Santa]
status: Draft
---

# Draw

## Definition

The act (and its result) of randomly assigning each [Member](member.md) a recipient to give a gift
to. A successful draw produces a complete set of [Assignments](assignment.md) — every member gives
once and receives once — while honoring three rules: **(a)** all [Exclusions](exclusion.md); **(b)**
no reciprocal pairs (if A→B then not B→A); and **(c)** no repeat of the group's **previous** draw.
Enforcing rule (c) requires keeping a **draw history**: each successful draw is recorded, and the
last one is retained to compare against. When no valid assignment exists, the draw **fails
gracefully** (no partial result) and explains why.

## Bounded context(s)

- **Secret-Santa:** performed once a group is ready; may be re-run (each run is appended to the draw
  history and the previous run becomes the baseline for rule (c)). Assignments are computed and kept
  locally only (see [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md)).

## Synonyms / do-not-use

- **Synonyms:** drawing of lots, matching.
- **Do NOT use:** "raffle", "lottery" (imply prizes/chance of winning).

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | Auslosung |
| EN | Draw |
| FR | Tirage |

## Related terms

- [Assignment](assignment.md), [Exclusion](exclusion.md), [Group](group.md), [Reveal link](reveal-link.md)

## First appearance

- [UC-SS-001](../use-cases/secret-santa/UC-SS-001-create-group.md); rules and history detailed in [UC-SS-004](../use-cases/secret-santa/UC-SS-004-run-draw.md)
