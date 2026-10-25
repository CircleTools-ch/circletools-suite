---
id: UC-SS-016
title: Give a member a gift note (allergies, dietary, do-not-gift) their giver sees
app: Secret-Santa
actors: [Organizer]
status: Reviewed
priority: Could
related: [FR-037, UC-SS-002, FR-002, NFR-001, NFR-002, NFR-003, ADR-0014, ADR-0020]
---

# UC-SS-016 — Give a member a gift note their giver sees

## Summary / goal

An organizer wants to tell whoever draws a given person things the **giver should know before buying**
— a nut allergy, "vegetarian", "no scented products", or "please don't get me another mug". This is a
[Gift note](../../glossary/gift-note.md): a short, free-text, **per-person** note that shows on that
person's reveal. Unlike a [Wishlist](../../glossary/wishlist.md) (what they *want*, which differs per
occasion) it is a **stable property of the person**, so it is set once and appears on every occasion's
draw.

## Primary actor & stakeholders

- **Primary actor:** Organizer (authors the note in v1).
- **Stakeholders:** Givers (want to avoid buying the wrong thing — an allergen, a duplicate, something
  against the recipient's lifestyle); recipients (want their constraints respected without having to
  publish a full wishlist).

## Preconditions

- A group with at least one [Member](../../glossary/member.md) exists; the organizer is on the members
  screen.

## Postconditions

- **Success:** the member carries a gift note (or none). It shows to whoever draws them, on the reveal
  link and — for a linked giver — on their my-assignments view, for **every** occasion.
- **Failure:** nothing is saved; an over-long note is rejected before it is stored.

## Trigger

The organizer edits a member and fills in (or clears) their gift note.

## Main success scenario

1. Organizer opens the **Members** screen and edits a member.
2. Alongside the name, the organizer types a **gift note** (e.g. "No nuts — severe allergy").
3. Organizer saves.
4. The app validates the note length and stores it on the member.
5. On the next draw's reveal, whoever drew that member sees the note with their giftee (with the
   wishlist), on the public reveal link and on a linked giver's my-assignments view.

## Extensions / alternate flows

- **2a.** Organizer leaves the note blank → the member has no note; nothing extra shows on the reveal
  (today's behaviour).
- **2b.** Organizer clears an existing note → it is removed.
- **4a.** The note exceeds the length cap → the app rejects the save with an inline message; nothing is
  stored. (The editor also caps input at the limit.)
- **5a.** A member is **archived** after a draw ([ADR-0052](../../adr/0052-member-archival-and-permanent-draw-history.md))
  → their note is kept with them, so a past occasion's reveal still shows it.

## Exceptions

- **Local storage unavailable** → the system reports the error and does not claim the note was saved.

## Non-functional notes

- **Offline/local-first:** authoring the note is a local operation on the one `Group` document; nothing
  new crosses to the server for the account-free flow
  ([NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md)). For a **linked** giver,
  the note travels with the assignment the organizer already publishes (ADR-0020), so the giver sees it
  on their own device.
- **Privacy:** the note is about the recipient and is shown only to their giver (on the reveal link, the
  same self-contained payload that already carries the wishlist —
  [ADR-0014](../../adr/0014-reveal-link-encoding-and-privacy.md); or the authorized my-assignments read).
  It is **not** spoiler-sensitive about *who* drew whom, but it names the giftee, so on the reveal it is
  shown **after** the giftee is revealed, with the wishlist
  ([NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md)).
- **Link size:** the note is capped so the encoded reveal link stays within its size budget
  ([ADR-0014](../../adr/0014-reveal-link-encoding-and-privacy.md)); it is omitted from the link entirely
  when unset.
- **Localization:** the field label, its helper text, and the reveal display label are DE/EN/FR
  ([NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)); the note's own text is
  user content and not translated.

## Open questions

- ~~Should a linked member self-author their own gift note?~~ Settled by ADR-0086: a participant keeps their own
  gift note with their wishlist (FR-030), and a member without one can write it from their reveal link (FR-050).
  Deferred; v1 is organizer-authored (the note is roster data). Revisit if asked.
- Structured **tags** (allergen chips) vs free text? v1 is free text; tags are a parked extension.

## Traceability

- Requirement: [FR-037](../../requirements/functional/FR-037-member-gift-note.md)
- Builds on: [UC-SS-002](UC-SS-002-manage-members.md) (managing members and wishlists)
- Governing ADR: [ADR-0014](../../adr/0014-reveal-link-encoding-and-privacy.md) (reveal payload — the
  note is an additive, version-compatible field), [ADR-0020](../../adr/0020-server-side-assignment-privacy.md)
  (the linked-member assignment projection carries it) — no new ADR needed
- Feature spec: [FEAT-012](../../features/FEAT-012-member-gift-note.md)
- Glossary: [Gift note](../../glossary/gift-note.md), [Member](../../glossary/member.md),
  [Wishlist](../../glossary/wishlist.md), [Reveal link](../../glossary/reveal-link.md)
