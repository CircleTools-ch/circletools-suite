---
id: FR-048
title: A default place seeds new groups, set at first run and in Settings
type: functional
status: Implemented
priority: Should
source: UC-SS-017
verification: test
related: [FR-038, UC-SS-017, ADR-0071, ADR-0086, NFR-001, FEAT-021, FEAT-027]
---

# FR-048 — A default place seeds new groups, set at first run and in Settings

> **Rewritten 2026-10-25 for [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)** ([FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)): one way to be in a group. The previous version described the four-kind storage model (on this device, shared by link, my account, my account · shared) and is in the git history.

## Description

The user has a **default [place](../../glossary/place.md)** for new groups — *on this device* or *online* — kept on the device
([ADR-0071](../../adr/0071-default-storage-mode-and-first-run-onboarding.md)).

- It starts as **on this device**, so nothing reaches the server for anyone who never changes it
  ([NFR-001](../non-functional/NFR-001-local-first-offline.md)).
- The first group a device creates shows both places with their explanation; later groups show the default with a
  *Change* menu for that one group.
- *Settings* lets the user change the default at any time. *Online* explains what it means in the current state:
  signed in, the group is kept in the account; signed out, this device keeps its organizer link.

## Rationale

People who know they want to share should not have to switch every new group by hand, and people who never think
about it should stay local.

## Acceptance criteria

```gherkin
Given a new device
When the user creates their first group
Then both places are shown with their explanation and "on this device" is selected

Given the default place is online
When the user creates a group without changing it
Then the group is created online

Given the Settings page
When the user changes the default place
Then the next new group starts in that place
```

## Dependencies & related

- Related: [FR-038](FR-038-per-group-storage-mode.md), [UC-SS-017](../../use-cases/secret-santa/UC-SS-017-choose-and-switch-storage-mode.md)
- Governing ADRs: [ADR-0071](../../adr/0071-default-storage-mode-and-first-run-onboarding.md), [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)
- Implemented by: [FEAT-021](../../features/FEAT-021-storage-default-onboarding-and-list-indicator.md), [FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)
