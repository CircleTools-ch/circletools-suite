---
term: Gift note
contexts: [Secret-Santa]
status: Draft
---

# Gift note

## Definition

A short, free-text note attached to a [Member](member.md) that the assigned giver sees — allergies,
dietary or lifestyle needs, and things **not** to buy (FR-037). Unlike a [Wishlist](wishlist.md) (what a
member *wants*, which is per [Event](event.md)), a gift note is a **stable property of the person**: it
is set once and shown on **every** occasion's [Reveal link](reveal-link.md). It is distinct from an
[Exclusion](exclusion.md), which constrains *who may draw whom* rather than *what to buy*. It is optional
and length-capped so it fits the encoded reveal link.

## Bounded context(s)

- **Secret-Santa:** one gift note per member (roster-level), organizer-authored; shown to the giver on
  the reveal and on a linked giver's my-assignments view. The note text is user content and not
  translated (see [NFR-003](../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)).

## Synonyms / do-not-use

- **Synonyms:** do-not-gift note, dietary/allergy note, "good to know", giver hint.
- **Do NOT use:** "wishlist" (that is what they *want*, and is per event), "exclusion" (that is a draw
  constraint, not gift guidance), "note" alone (ambiguous with event notes).

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | Geschenk-Notiz |
| EN | Gift note |
| FR | Note pour le cadeau |

## Related terms

- [Member](member.md), [Wishlist](wishlist.md), [Reveal link](reveal-link.md), [Exclusion](exclusion.md)

## First appearance

- [UC-SS-016](../use-cases/secret-santa/UC-SS-016-member-gift-note.md)
