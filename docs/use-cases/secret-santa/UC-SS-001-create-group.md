---
id: UC-SS-001
title: Create a Secret-Santa group
app: Secret-Santa
actors: [Organizer]
status: Draft
priority: Must
related: [FR-001]
---

# UC-SS-001 — Create a Secret-Santa group

## Summary / goal

An organizer creates a new [Group](../../glossary/group.md) so they can add
[Members](../../glossary/member.md), define [Exclusions](../../glossary/exclusion.md), and later
run a [Draw](../../glossary/draw.md) — all **offline, without an account**.

## Primary actor & stakeholders

- **Primary actor:** Organizer (the person setting up the exchange).
- **Stakeholders:** Members (want a fair draw), the organizer (wants it quick and private).

## Preconditions

- The app is installed (or opened on the web). No account is required.

## Postconditions

- **Success:** a new group exists locally with a name and (optionally) the first members.
- **Failure:** nothing is persisted; the organizer sees why.

## Trigger

The organizer chooses "Create group" from the Secret-Santa app.

## Main success scenario

1. Organizer opens the Secret-Santa app.
2. Organizer selects **Create group**.
3. Organizer enters a **group name**.
4. Organizer optionally adds initial members (name each).
5. System validates the input and **saves the group to local storage**.
6. System shows the new group's screen, ready for more members/exclusions.

## Extensions / alternate flows

- **3a.** Name is empty → system blocks saving and prompts for a name.
- **4a.** Organizer adds no members now → group is created empty; members can be added later.
- **4b.** Duplicate member name → system warns but allows it (names may legitimately repeat).

## Exceptions

- **Local storage unavailable** → system reports the error and does not claim success.

## Non-functional notes

- **Offline/local-first:** fully works with no account and no network (see [FR-001](../../requirements/functional/FR-001-create-secret-santa-group.md)).
- **Localization:** all labels available in DE/EN/FR.
- **Privacy:** data stays on device unless the user later signs in to sync.
- **Performance:** creating a group is instant (local write).

## Open questions

- Max members? Group avatar/theme now or later? (defer to brainstorming/requirements)

## Traceability

- Requirement: [FR-001](../../requirements/functional/FR-001-create-secret-santa-group.md)
- Glossary: [Group](../../glossary/group.md), [Member](../../glossary/member.md)
- Origin: [2026-07-24 brainstorming](../../brainstorming/2026-07-24-secret-santa-scope.md)
