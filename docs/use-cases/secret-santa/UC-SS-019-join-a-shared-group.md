---
id: UC-SS-019
title: Take part through the join link
app: Secret-Santa
actors: [Participant]
status: Approved
priority: Should
related: [FR-041, FR-039, NFR-002, UC-SS-018, UC-SS-020, ADR-0086]
---

# UC-SS-019 — Take part through the join link

> **Rewritten 2026-10-25 for [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)** ([FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)). The previous version described the four-kind storage model and member linking; it is in the git history.

## Summary / goal

A person opens a group's join link, says who they are, and from then on sees their own pairing and keeps their own wishlist — with an account or on this device only.

## Primary actor & stakeholders

- **Primary actor:** a person in the group.
- **Stakeholders & interests:** the organizer — that people pick their own member; the others — that nobody takes their spot.

## Preconditions

- The group is online and its join link is on.

## Postconditions

- **Success:** the person is a participant of their member and their participant page is open.
- **Failure:** nothing changes.

## Trigger

The person opens the join link.

## Main success scenario

1. The system shows the group's name and the members whose spot is free (taken ones marked).
2. The person picks themselves, or enters a new name.
3. The person chooses *just this device* or *with my account*.
4. The system makes them a participant of that member and opens their participant page.

## Extensions / alternate flows

- **2a.** Their member is taken → they ask the organizer to free the spot (UC-SS-020).
- **3a.** *With my account* while signed out → the system offers to sign in first and returns here.
- **1a.** The link was turned off or replaced → the page says it no longer works.

## Exceptions

- **Offline** → the page can't load; the person tries again online.

## Non-functional notes

- **Offline/local-first:** the participant page then works offline with the last view.
- **Localization:** the page in the person's language.
- **Privacy:** the join page lists names but no pairings (NFR-002).
- **Performance:** —

## Open questions

- None.

## Traceability

- Requirements: [FR-041](../../requirements/functional/FR-041-join-link-and-person-claim.md), [FR-039](../../requirements/functional/FR-039-shared-by-link-storage-and-sync.md), [NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md)
- Feature spec: [FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)
- Glossary terms: [Join link](../../glossary/join-link.md), [Participant](../../glossary/participant.md), [Device key](../../glossary/device-key.md)
