---
term: Carry-forward
contexts: [Secret-Santa]
status: Agreed
---

# Carry-forward

## Definition

Creating a new [Event](event.md) in a [Group](group.md) by **copying, at creation time**, from an
existing event of that group: each current member's [Wishlist](wishlist.md), the [Budget](budget.md),
and the source event's [Draw](draw.md) recorded as the new event's **draw-history baseline** (so the
new draw honours rule (c) — *no repeat of the previous draw* — and does not reproduce the source
event's pairings). It is a **copy, not a link** — the two events stay independent (ADR-0042) and
everything copied is editable afterwards; the source event is never changed.

[Exclusions](exclusion.md) are **not** part of carry-forward because they are already **shared** at
the group level and apply to every event. Carry-forward is optional (the organizer may start blank)
and works fully **offline / account-free** — it is a local operation on the one group document.

## Bounded context(s)

- **Secret-Santa:** an organizer-initiated operation when adding an event to a group that already has
  one. Distinct from the linked-member **self-copy** of their own wishlist across memberships on the
  My-wishlists page ([FR-030](../requirements/functional/FR-030-edit-my-wishlist.md)), which is
  account-only and copies a single person's list.

## Synonyms / do-not-use

- **Synonyms:** copy previous, seed from last occasion, "same as last year".
- **Do NOT use:** "duplicate the group" (a group is not duplicated — a new *event* is added to the same
  group); "link" / "reference" (carry-forward copies; it does not tie the new event to the source).

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | Aus letztem Anlass übernehmen |
| EN | Carry forward from last event |
| FR | Reprendre du dernier événement |

## Related terms

- [Event](event.md), [Wishlist](wishlist.md), [Budget](budget.md), [Draw](draw.md),
  [Exclusion](exclusion.md), [Group](group.md)

## First appearance

- [UC-SS-014](../use-cases/secret-santa/UC-SS-014-carry-forward-new-occasion.md); required by
  [FR-035](../requirements/functional/FR-035-carry-forward-new-occasion.md), realised by
  [FEAT-010](../features/FEAT-010-carry-forward-new-occasion.md).
