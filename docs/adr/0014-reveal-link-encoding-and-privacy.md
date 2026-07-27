---
status: Accepted
date: 2026-07-24
deciders: [Adrian]
tags: [secret-santa, privacy, web, encoding]
---

# ADR-0014 — Secret-Santa reveal-link encoding & privacy

> **Partly superseded by [ADR-0020](0020-server-side-assignment-privacy.md) (2026-07-27).** This ADR
> assumed assignments are **never** stored server-side. In v2 **account mode** they are, protected by
> authorization instead ([NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md) was
> amended accordingly). Everything else here — encode-only payloads, the URL fragment, the readable
> owner label, the anti-spoiler UX — **still stands**, and the entire account-free flow still depends
> on it, which is why this ADR remains **Accepted** rather than superseded.

## Context and problem statement

The Secret-Santa MVP reveals each giver's [Assignment](../glossary/assignment.md) through a
**self-contained [reveal link](../glossary/reveal-link.md)**: the payload is carried in the link
itself and decoded fully client-side, with **no backend** (see
[FR-005](../requirements/functional/FR-005-generate-reveal-links.md),
[FR-006](../requirements/functional/FR-006-reveal-page-decode.md),
[NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md)). How exactly is the payload
encoded, where does it live in the URL, and what privacy properties do we guarantee for the MVP?

## Decision drivers

- No backend and fully offline; the reveal page is static/PWA.
- **Nothing human-readable in the URL except *who the link is for*** — so the organizer can tell which
  link goes to which person, while *who they are Secret Santa for* is not readable.
- An **accidental open must not spoil** the result.
- Low friction for non-technical family members (ideally: tap a link).

## Considered options

1. **Encode-only** — compress + Base64Url, carried in the URL **fragment**; a readable owner label alongside.
2. **Encrypt, key embedded in the link** — obfuscates casual reading, but the key travels with the link.
3. **Encrypt with a separate code** shared out-of-band — a leaked link alone reveals nothing, but every recipient must enter a code.
4. Encode-only **+ HMAC** — tamper-proofing on top of option 1.

## Decision outcome

**Chosen: option 1 — encode-only, in the URL fragment.**

- The reveal page is a fixed static route; **all data lives in the URL fragment** (`#…`), so it is
  **never transmitted to any server** (fragments are not sent in HTTP requests).
- The fragment carries two parts:
  - a **human-readable owner label** — `for=<giver display name>` — the *only* legible token, so the
    organizer knows who to send each link to;
  - a **cryptic payload** — `d=<Base64Url(Deflate(UTF-8(JSON)))>` with a leading **format-version**
    marker — containing the **recipient (giftee) name, the giftee's [Wishlist](../glossary/wishlist.md),
    the [Budget](../glossary/budget.md), and the [Event details](../glossary/event-details.md)**.
- **No encryption and no separate code.** The model is "**the link is the secret**": whoever holds a
  specific link can decode that one assignment, but the payload is compressed+Base64Url so it is not
  casually readable, and each link contains only one giver's data (it cannot reveal anyone else's).
- **No HMAC/integrity for the MVP.** A link is self-scoped, so editing it only changes what the
  tamperer themselves sees — there is no cross-user impact to protect against.
- **Anti-spoiler UX (see [FR-006](../requirements/functional/FR-006-reveal-page-decode.md) /
  [UC-SS-006](../use-cases/secret-santa/UC-SS-006-open-reveal-link.md)):** on open, the page shows a
  summary — a greeting to the named giver, the budget, and the event details — **immediately**, but the
  **giftee's name and wishlist are hidden behind a "Reveal" button**.

## Consequences

- **Positive:** zero backend, fully offline, works as a cached PWA; the organizer can route links by the
  readable owner label; the secret is unreadable at a glance; accidental opens don't spoil; no keys or
  codes to manage.
- **Negative / accepted:** anyone who obtains a specific link can (with a decoder) read that one
  assignment — this is the intended "link is the secret" model, **not confidentiality against a
  determined holder**. URL length grows with wishlist size (see constraints). Base64 is encoding, not
  encryption.
- **Negative / accepted — links cannot be revoked.** Because a link carries its whole payload and is
  decoded offline, **there is no way to invalidate one after sending it**. A link from a superseded
  draw keeps resolving and keeps showing the *old* recipient; nothing on the reveal page can detect
  this, since the page never sees the group. Two consequences follow, both handled in the UI rather
  than the link format:
  - Running a **second draw** silently obsoletes every link already sent, so the organizer must
    confirm before re-drawing.
  - **Changing the members** voids the draw entirely (a draw belongs to the exact member set it was
    generated for), so the organizer must draw again and re-send every link.

  Adding a draw identifier to the payload would let a *newer* link identify itself, but could not
  help the recipient of a stale one — they have only the old link. Detecting staleness would require
  shared state, i.e. a backend, which this ADR exists to avoid.
- **Follow-ups:** the exact **JSON schema + version strategy** and the **URL-length budget** are pinned
  in [FEAT-001](../features/FEAT-001-secret-santa-mvp.md). If links later travel over broadcast/untrusted
  channels, revisit with **optional encryption (separate code)** and/or **HMAC** — a future ADR that
  would supersede this one.

## Pros and cons of the options

### 1. Encode-only (chosen)
- 👍 No backend/keys; offline; readable owner label; not casually readable; lowest friction.
- 👎 A held link is decodable by a motivated party (no confidentiality beyond possession).

### 2. Encrypt, key in link
- 👍 Hides content from casual URL inspection.
- 👎 Functionally same as encode-only (key travels with the link); added complexity for no real gain.

### 3. Encrypt, separate code
- 👍 A leaked link alone reveals nothing.
- 👎 Every recipient must receive and enter a code — friction for non-technical family users.

### 4. Encode-only + HMAC
- 👍 Detects/blocks link tampering.
- 👎 Low value here (self-scoped links; tampering only affects the tamperer); extra machinery.

## Links

- Requirements: [FR-005](../requirements/functional/FR-005-generate-reveal-links.md), [FR-006](../requirements/functional/FR-006-reveal-page-decode.md), [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md)
- Use cases: [UC-SS-005](../use-cases/secret-santa/UC-SS-005-share-reveal-links.md), [UC-SS-006](../use-cases/secret-santa/UC-SS-006-open-reveal-link.md)
- Feature spec: [FEAT-001](../features/FEAT-001-secret-santa-mvp.md)
- Partly superseded by: [ADR-0020](0020-server-side-assignment-privacy.md) (server-side assignment storage in account mode)
- Glossary: [Reveal link](../glossary/reveal-link.md)
