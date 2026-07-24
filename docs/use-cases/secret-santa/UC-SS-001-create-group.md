---
id: UC-SS-001
title: Create and configure a Secret-Santa group
app: Secret-Santa
actors: [Organizer]
status: Draft
priority: Must
related: [FR-001, NFR-001, NFR-003]
---

# UC-SS-001 — Create and configure a Secret-Santa group

## Summary / goal

An organizer creates a new [Group](../../glossary/group.md) and configures its
[Budget](../../glossary/budget.md) and [Event details](../../glossary/event-details.md) so they can
later add [Members](../../glossary/member.md), define [Exclusions](../../glossary/exclusion.md), and
run a [Draw](../../glossary/draw.md) — all **offline, without an account**.

## Primary actor & stakeholders

- **Primary actor:** Organizer (the person setting up the exchange).
- **Stakeholders:** Members (want a fair exchange and a clear budget/date), the organizer (wants it
  quick and private).

## Preconditions

- The app is installed (or opened on the web). No account is required.

## Postconditions

- **Success:** a new group exists locally with a name and, optionally, a budget and event details.
- **Failure:** nothing is persisted; the organizer sees why.

## Trigger

The organizer chooses "Create group" from the Secret-Santa app.

## Main success scenario

1. Organizer opens the Secret-Santa app.
2. Organizer selects **Create group**.
3. Organizer enters a **group name**.
4. Organizer optionally sets a **budget** (amount + currency) for the group.
5. Organizer optionally sets **event details** (date, location, notes).
6. System validates the input and **saves the group to local storage**.
7. System shows the new group's screen, ready for members and exclusions.

## Extensions / alternate flows

- **3a.** Name is empty → system blocks saving and prompts for a name.
- **4a.** Budget amount is non-numeric or negative → system blocks saving and prompts for a valid amount.
- **5a.** Event date is in the past → system warns but allows it (a group may be recorded after the fact).
- **4b./5b.** Organizer skips budget and/or event details → group is created without them; both can be edited later.

## Exceptions

- **Local storage unavailable** → system reports the error and does not claim success.

## Non-functional notes

- **Offline/local-first:** fully works with no account and no network (see [FR-001](../../requirements/functional/FR-001-create-secret-santa-group.md), [NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md)).
- **Localization:** all labels available in DE/EN/FR; budget currency and date formatting follow the active culture (`de-CH` default) — see [NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md).
- **Privacy:** data stays on device; no account or backend is involved.
- **Performance:** creating a group is instant (local write).

## Open questions

- Max members? Group avatar/theme now or later? Multiple currencies per suite? (defer)

## Traceability

- Requirement: [FR-001](../../requirements/functional/FR-001-create-secret-santa-group.md); related [NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md), [NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)
- Glossary: [Group](../../glossary/group.md), [Member](../../glossary/member.md), [Budget](../../glossary/budget.md), [Event details](../../glossary/event-details.md)
- Origin: [2026-07-24 brainstorming](../../brainstorming/2026-07-24-secret-santa-scope.md)
