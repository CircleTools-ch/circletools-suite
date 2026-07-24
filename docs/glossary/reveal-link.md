---
term: Reveal link
contexts: [Secret-Santa]
status: Draft
---

# Reveal link

## Definition

A self-contained, URL-safe link that **encodes one giver's full [Assignment](assignment.md)
payload** — the recipient's name, the recipient's [Wishlist](wishlist.md), the group
[Budget](budget.md), and the [Event details](event-details.md) — directly into the link itself. A
reveal page decodes it **entirely client-side**; nothing is stored on or fetched from a server. One
reveal link is generated per [Member](member.md), and **the link is the secret**: whoever holds a
member's link can see that member's assignment.

## Bounded context(s)

- **Secret-Santa:** generated after a successful [Draw](draw.md); distributed out-of-band (copy /
  QR / messaging). Scoped to a single giver so no member sees another's assignment (see
  [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md)).

## Synonyms / do-not-use

- **Synonyms:** encoded reveal link, self-contained link.
- **Do NOT use:** "invite link" (no invitation/account is implied), "share URL" (ambiguous),
  "backend link" (there is no backend).

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | Auflösungs-Link |
| EN | Reveal link |
| FR | Lien de révélation |

## Related terms

- [Assignment](assignment.md), [Draw](draw.md), [Wishlist](wishlist.md), [Budget](budget.md), [Event details](event-details.md), [Member](member.md)

## First appearance

- [UC-SS-005](../use-cases/secret-santa/UC-SS-005-share-reveal-links.md)
