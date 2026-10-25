---
id: UC-SS-012
title: Keep my own wishlist and gift note
app: Secret-Santa
actors: [Participant]
status: Approved
priority: Should
related: [FR-030, FR-050, FR-037, FR-024, NFR-003, ADR-0086]
---

# UC-SS-012 — Keep my own wishlist and gift note

> **Rewritten 2026-10-25 for [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)** ([FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)). The previous version described the four-kind storage model and member linking; it is in the git history.

## Summary / goal

A person taking part writes what they wish for and what their giver should know, and their giver sees it straight away.

## Primary actor & stakeholders

- **Primary actor:** a participant.
- **Stakeholders & interests:** their giver — to read the latest; the organizer — to see it without retyping.

## Preconditions

- The person takes part in an online group.

## Postconditions

- **Success:** the person's entry is stored once and shown to their giver and the organizer.
- **Failure:** the entry stays on the device as pending and is sent later.

## Trigger

The person edits the wishlist or gift note on their participant page.

## Main success scenario

1. The person writes a wishlist for an event and their gift note.
2. The person saves.
3. The system sends the entry; the page says it is saved.
4. The giver's participant page and reveal link show it; the organizer sees it in the event's wishlists, marked as kept by the member.

## Extensions / alternate flows

- **3a.** Offline → the page says it is saved on this device; it is sent on the next sync.
- **1a.** The person has no participant but holds their reveal link → they write it there instead (FR-050).

## Exceptions

- **The entry exceeds the limits** → it is refused and the page says why.

## Non-functional notes

- **Offline/local-first:** saved on the device and sent later.
- **Localization:** —
- **Privacy:** only the person's giver and the organizer see it.
- **Performance:** —

## Open questions

- None.

## Traceability

- Requirements: [FR-030](../../requirements/functional/FR-030-edit-my-wishlist.md), [FR-050](../../requirements/functional/FR-050-edit-own-entry-from-reveal-link.md), [FR-037](../../requirements/functional/FR-037-member-gift-note.md)
- Feature spec: [FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)
- Glossary terms: [Member entry](../../glossary/member-entry.md), [Wishlist](../../glossary/wishlist.md), [Gift note](../../glossary/gift-note.md)
