---
id: UC-SS-018
title: Organize an online group without an account
app: Secret-Santa
actors: [Organizer, Co-organizer]
status: Approved
priority: Should
related: [FR-040, FR-043, FR-039, NFR-009, UC-SS-017, UC-SS-019, ADR-0086]
---

# UC-SS-018 — Organize an online group without an account

> **Rewritten 2026-10-25 for [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)** ([FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)). The previous version described the four-kind storage model and member linking; it is in the git history.

## Summary / goal

An organizer without an account runs an online group from the device that holds its organizer link, shares that link with whoever helps, and lets people take part through the join link.

## Primary actor & stakeholders

- **Primary actor:** an organizer without an account.
- **Stakeholders & interests:** co-organizers — to edit from their own devices; participants — to take part.

## Preconditions

- The group is online and owned by its organizer link; this device holds the link.

## Postconditions

- **Success:** the group is organized from every device holding the link; people take part through the join link.
- **Failure:** the group stays as it was.

## Trigger

The organizer opens *Sharing & sync*.

## Main success scenario

1. The system shows the management link (copy, QR code, share) and the join link switch.
2. The organizer shares the management link with a co-organizer.
3. The co-organizer opens it and lands in the group as an organizer; edits on either device reach the other.
4. The organizer turns the join link on and shares it with the people in the group (UC-SS-019).

## Extensions / alternate flows

- **2a.** The management link leaked → the organizer replaces it; the old one stops working.
- **4a.** The organizer replaces the join link → the old one stops working; people who already take part keep access.
- **1a.** Signed in → the system offers to keep the group in the account (the management link then stops working), or, for an account-owned group, to hand it over to a fresh management link.

## Exceptions

- **The management link no longer works** → the device keeps its copy as a group on this device.

## Non-functional notes

- **Offline/local-first:** edits are kept and sent when back online.
- **Localization:** —
- **Privacy:** anyone holding the management link reads the whole group.
- **Performance:** —

## Open questions

- None.

## Traceability

- Requirements: [FR-040](../../requirements/functional/FR-040-admin-link-capability.md), [FR-043](../../requirements/functional/FR-043-release-ownership-and-deletion-disposition.md), [FR-039](../../requirements/functional/FR-039-shared-by-link-storage-and-sync.md), [NFR-009](../../requirements/non-functional/NFR-009-account-free-write-abuse-resistance.md)
- Feature spec: [FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)
- Glossary terms: [Organizer link](../../glossary/organizer-link.md), [Owner](../../glossary/owner.md), [Join link](../../glossary/join-link.md)
