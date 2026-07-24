---
id: UC-SS-005
title: Generate and share reveal links
app: Secret-Santa
actors: [Organizer]
status: Draft
priority: Must
related: [FR-005, NFR-001, NFR-002]
---

# UC-SS-005 — Generate and share reveal links

## Summary / goal

After a successful [Draw](../../glossary/draw.md), an organizer generates **one**
[Reveal link](../../glossary/reveal-link.md) per [Member](../../glossary/member.md) and distributes
each link out-of-band, so every giver can privately learn their
[Assignment](../../glossary/assignment.md) — all **offline, without an account or backend**.

## Primary actor & stakeholders

- **Primary actor:** Organizer.
- **Stakeholders:** Members (each needs exactly their own link, and only their own); the organizer
  (wants easy, private distribution).

## Preconditions

- A successful draw exists for the group (see [UC-SS-004](UC-SS-004-run-draw.md)).

## Postconditions

- **Success:** the organizer holds one distinct reveal link per member, each self-contained.
- **Failure:** no links are produced; the organizer sees why.

## Trigger

The organizer selects **Share reveal links**.

## Main success scenario

1. Organizer selects **Share reveal links**.
2. For each member, the system builds the giver's payload — recipient name, recipient
   [Wishlist](../../glossary/wishlist.md), group [Budget](../../glossary/budget.md), and
   [Event details](../../glossary/event-details.md).
3. The system **encodes the full payload into a URL-safe reveal link** (self-contained; nothing is
   uploaded or stored on a server).
4. The system presents one link per member with the member's name, and offers to **copy**, show a
   **QR code**, or hand the link to a **messaging** app.
5. Organizer distributes each link to the corresponding member out-of-band.

## Extensions / alternate flows

- **1a.** The group has no draw yet, or the draw is stale after member/exclusion edits → the system blocks sharing and prompts to run (or re-run) the draw.
- **4a.** Organizer regenerates links → new links encode the current draw; previously distributed links reflect the old draw and should be discarded.

## Exceptions

- **Payload cannot be encoded** (unexpected internal error) → the system reports it and produces no links, rather than a malformed one.

## Non-functional notes

- **Offline/local-first:** link generation is entirely on-device; no account or network is needed (see [NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md)).
- **Localization:** the sharing UI is available in DE/EN/FR; the reveal page localizes its own labels when opened (see [UC-SS-006](UC-SS-006-open-reveal-link.md)).
- **Privacy:** each link carries only that giver's assignment; no member's link exposes another member's assignment, and nothing is stored server-side — **the link is the secret** (see [NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md)). The organizer is responsible for giving each link to the right person.
- **Performance:** generating all links is near-instant for typical group sizes.

## Open questions

- Should links be tamper-evident / signed so a giver can't decode another's link? (to be recorded as an ADR at implementation time — see [ADR backlog](../../adr/index.md))

## Traceability

- Requirement: [FR-005](../../requirements/functional/FR-005-generate-reveal-links.md); related [NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md)
- Glossary: [Reveal link](../../glossary/reveal-link.md), [Assignment](../../glossary/assignment.md), [Wishlist](../../glossary/wishlist.md), [Budget](../../glossary/budget.md), [Event details](../../glossary/event-details.md), [Member](../../glossary/member.md)
- Origin: [2026-07-24 brainstorming](../../brainstorming/2026-07-24-secret-santa-scope.md)
