---
term: Wishlist
contexts: [Secret-Santa]
status: Draft
---

# Wishlist

## Definition

The set of gift ideas a [Member](member.md) provides for themselves — free-text hints that help the
member who is assigned to give to them. A wishlist belongs to a member **within one
[Event](event.md)** (ADR-0042): the same member can have a different wishlist for each occasion. It is
optional (it may be empty). When a [Draw](draw.md) is revealed, the giver sees their recipient's
wishlist **for that event**.

## Bounded context(s)

- **Secret-Santa:** one wishlist per member **per event** (ADR-0042); authored content is not
  translated (see [NFR-003](../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)).

## Synonyms / do-not-use

- **Synonyms:** gift ideas, gift list.
- **Do NOT use:** "registry" (implies external retailer integration), "cart".

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | Wunschliste |
| EN | Wishlist |
| FR | Liste de souhaits |

## Related terms

- [Member](member.md), [Event](event.md), [Assignment](assignment.md), [Reveal link](reveal-link.md)

## First appearance

- [UC-SS-002](../use-cases/secret-santa/UC-SS-002-manage-members.md)
