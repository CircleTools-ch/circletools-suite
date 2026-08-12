---
id: FR-012
title: Manage profile and preferred language
type: functional
status: Implemented
priority: Must
source: UC-MAIN-005
verification: test
related: [UC-MAIN-005, NFR-003, ADR-0011, ADR-0016, ADR-0018]
---

# FR-012 — Manage profile and preferred language

## Description

A signed-in [User](../../glossary/user.md) must be able to view and edit their **display name** and
**preferred language** (DE / EN / FR, `de-CH` default).

- The display name may not be empty, and is **not required to be unique** — not within a
  [Circle](../../glossary/circle.md), not globally.
- The preferred language applies **immediately** to the running UI, without a reload or a flash of the
  wrong language, and is used for all subsequent transactional email.
- **While signed in, the account preference wins**; after sign-out the UI reverts to the
  device/browser preference.
- A language change made offline applies locally at once and is pushed on the next
  [Sync](../../glossary/sync.md); it must not be reported as saved server-side when it is not.
- The user's **email address is never exposed to other members** of shared circles.
- **Avatar is out of scope** for M1 (deferred to M2).

## Rationale

The display name is how a person is recognised by everyone sharing a circle with them, so it must be
theirs to choose — and a uniqueness constraint would force people to adopt names nobody calls them
(two "Anna"s in one family is ordinary). Putting the language on the account is what lets the
preference follow a user between devices and reach transactional email, which the device-level picker
cannot do. Hiding the email address preserves the enumeration resistance that
[ADR-0018](../../adr/0018-circle-and-sharing-model.md) builds into invitations — surfacing it in a
member list would hand out exactly what the invite flow refuses to confirm.

## Acceptance criteria

```gherkin
Given a signed-in user
When they open their profile
Then their current display name, email address and preferred language are shown

Given a signed-in user
When they change their display name to a non-empty value and save
Then the new name is stored
And it is shown everywhere they are visible to others

Given a signed-in user
When they attempt to save an empty display name
Then saving is blocked

Given two users in the same circle with identical display names
When either is saved
Then both are accepted, because display names are not unique

Given a signed-in user
When they change their preferred language
Then the running UI switches immediately without a reload
And subsequent transactional emails use the new language

Given a signed-in user who is offline
When they change their preferred language
Then the change applies locally at once
And it is pushed on the next sync
And it is not reported as saved server-side before that

Given a user signs out on a shared device
When the sign-in screen is shown
Then the UI language reverts to the device/browser preference

Given a member of a circle
When they view the other members
Then no other member's email address is shown
```

## Dependencies & related

- Derived from: [UC-MAIN-005](../../use-cases/main/UC-MAIN-005-manage-profile.md)
- Related: [FR-013](FR-013-change-email-address.md),
  [NFR-003](../non-functional/NFR-003-multilingual-de-en-fr.md),
  [NFR-005](../non-functional/NFR-005-account-enumeration-resistance.md)
- **Carried criterion** — changing the language *offline*, pushed on the next sync — is specified by
  [FR-023](FR-023-sync-across-devices.md) and
  [FR-024](FR-024-offline-editing-and-reconciliation.md), and closes when M3 is built
- Governing ADRs: [ADR-0011](../../adr/0011-localization-format-and-culture.md),
  [ADR-0016](../../adr/0016-sync-and-conflict-resolution.md),
  [ADR-0018](../../adr/0018-circle-and-sharing-model.md)
- Glossary: [User](../../glossary/user.md), [Circle](../../glossary/circle.md),
  [Sync](../../glossary/sync.md)
