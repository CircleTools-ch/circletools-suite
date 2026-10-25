---
id: UC-SS-017
title: Choose where a group is kept, and change it later
app: Secret-Santa
actors: [Organizer]
status: Approved
priority: Should
related: [FR-038, FR-043, FR-048, UC-SS-018, NFR-001, ADR-0086]
---

# UC-SS-017 — Choose where a group is kept, and change it later

> **Rewritten 2026-10-25 for [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)** ([FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)). The previous version described the four-kind storage model and member linking; it is in the git history.

## Summary / goal

The organizer keeps a group on this device or puts it online, and can change that later without losing anything.

## Primary actor & stakeholders

- **Primary actor:** the organizer.
- **Stakeholders & interests:** the people in the group — to keep access while the group stays online.

## Preconditions

- None for creating; for changing, the organizer has the group on this device or owns it online.

## Postconditions

- **Success:** the group is in the chosen place.
- **Failure:** the group stays where it was.

## Trigger

The organizer creates a group, or opens *Sharing & sync* for an existing one.

## Main success scenario

1. When creating, the system shows the default place (both places with their explanation on a device's first group).
2. The organizer keeps the default or picks the other place and creates the group.
3. Later, in *Sharing & sync*, the organizer picks the other place.
4. The system says in one line what that does and asks to confirm.
5. On *online*: the system uploads the group; the signed-in account owns it, or, signed out, an organizer link this device keeps.
6. On *on this device*: the system deletes the online copy (its links stop working) and keeps the device's copy.

## Extensions / alternate flows

- **5a.** Signed out → the group is owned by an organizer link (UC-SS-018); signing in later offers to keep it in the account.
- **5b.** Offline → the change is refused; the group stays on this device.
- **6a.** The device is not the owner → it can only leave the group or stop organizing it on this device.

## Exceptions

- **Upload refused (too large)** → the group stays on this device and the organizer is told.

## Non-functional notes

- **Offline/local-first:** on this device is the default; going online needs a connection.
- **Localization:** the explanations in the user's language.
- **Privacy:** an online group's pairings are on the server (NFR-002).
- **Performance:** —

## Open questions

- None.

## Traceability

- Requirements: [FR-038](../../requirements/functional/FR-038-per-group-storage-mode.md), [FR-043](../../requirements/functional/FR-043-release-ownership-and-deletion-disposition.md), [FR-048](../../requirements/functional/FR-048-default-storage-mode-for-new-groups.md)
- Feature spec: [FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)
- Glossary terms: [Place](../../glossary/place.md), [Online group](../../glossary/online-group.md), [Owner](../../glossary/owner.md)
