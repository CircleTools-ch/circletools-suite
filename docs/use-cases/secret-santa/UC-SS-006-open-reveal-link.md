---
id: UC-SS-006
title: Open a reveal link
app: Secret-Santa
actors: [Member]
status: Approved
priority: Must
related: [FR-006, NFR-001, NFR-002, NFR-003]
---

# UC-SS-006 — Open a reveal link

## Summary / goal

A [Member](../../glossary/member.md) opens the [Reveal link](../../glossary/reveal-link.md) the
organizer sent them; the reveal page **decodes the link entirely client-side** and privately shows
that giver their [Assignment](../../glossary/assignment.md) — the recipient, the recipient's
[Wishlist](../../glossary/wishlist.md), the group [Budget](../../glossary/budget.md), and the
[Event details](../../glossary/event-details.md) — with **no backend and no account**.

## Primary actor & stakeholders

- **Primary actor:** Member (the giver who received the link).
- **Stakeholders:** Organizer (wants each member to see a correct, private result without any server).

## Preconditions

- The member has received their own reveal link (see [UC-SS-005](UC-SS-005-share-reveal-links.md)).

## Postconditions

- **Success:** the giver has privately seen their recipient, wishlist, budget, and event details.
- **Failure:** nothing is shown beyond an explanatory message; no data is transmitted.

## Trigger

The member opens the reveal link (tap/click, or scan its QR code).

## Main success scenario

1. Member opens the reveal link.
2. The reveal page **decodes the encoded payload from the link, entirely in the browser** (no request to a server for the assignment).
3. The page shows a **summary immediately** — a greeting to the named giver (from the `for=<giver>` label), the group **budget**, and the **event details** (date, location, notes).
4. The **recipient's name and wishlist stay hidden behind a "Reveal" button**, so an accidental open does not spoil the result. The member taps **Reveal** to see **you give to** the recipient and their **wishlist** (per [ADR-0014](../../adr/0014-reveal-link-encoding-and-privacy.md)).
5. The member reads their assignment privately and closes the page.

## Extensions / alternate flows

- **1a.** Member opens the link offline → it still works; decoding is fully client-side (see [NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md)).
- **3a.** The recipient has an empty wishlist → the page shows the recipient and budget/event, and notes that no gift ideas were provided.
- **3b.** The group has no budget or no event details → the page omits the missing sections gracefully.

## Exceptions

- **Link is malformed or truncated** → the page shows a clear "this link could not be read" message and does not display a partial or guessed assignment.
- **Link edited / tampered** → per [ADR-0014](../../adr/0014-reveal-link-encoding-and-privacy.md) the MVP does not sign links; a corrupt payload fails to decode (clear error, no partial guess), and editing a link only ever changes what the editor themselves sees (no cross-member impact).

## Non-functional notes

- **Offline/local-first:** the page decodes and renders without any network call for the assignment (see [NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md)).
- **Localization:** the reveal page renders its labels in DE/EN/FR, defaulting to `de-CH` (see [NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)).
- **Privacy:** only this giver's assignment is present in the link, and nothing is stored or fetched server-side — **the link is the secret** (see [NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md)).
- **Performance:** decode-and-display is near-instant.

## Open questions

- Should the page offer to add the event to a calendar? (defer)

## Traceability

- Requirement: [FR-006](../../requirements/functional/FR-006-reveal-page-decode.md); related [NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md), [NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md), [NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)
- Decision: [ADR-0014](../../adr/0014-reveal-link-encoding-and-privacy.md) (reveal-link encoding & privacy)
- Glossary: [Reveal link](../../glossary/reveal-link.md), [Assignment](../../glossary/assignment.md), [Wishlist](../../glossary/wishlist.md), [Budget](../../glossary/budget.md), [Event details](../../glossary/event-details.md), [Member](../../glossary/member.md)
- Origin: [2026-07-24 brainstorming](../../brainstorming/2026-07-24-secret-santa-scope.md)
