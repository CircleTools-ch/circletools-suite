---
id: UC-MAIN-005
title: Manage profile and preferred language
app: Main
actors: [User]
status: Approved
priority: Must
related: [NFR-003, ADR-0009, ADR-0016, ADR-0018]
---

# UC-MAIN-005 — Manage profile and preferred language

## Summary / goal

A signed-in [User](../../glossary/user.md) edits how they appear to other people in their
[Circles](../../glossary/circle.md) and which of DE/EN/FR the suite uses for them — in the UI **and**
in the emails it sends.

## Primary actor & stakeholders

- **Primary actor:** User, signed in.
- **Stakeholders:** other members of shared circles (they see the display name, so it must be
  recognisable); the operator (the preferred language drives transactional email).

## Preconditions

- The user is signed in.

## Postconditions

- **Success:** the updated display name and preferred language are stored and applied everywhere the
  user is visible.
- **Failure:** nothing changes and the user is told why.

## Trigger

The user opens **Profile** in the Main shell.

## Main success scenario

1. User opens **Profile**.
2. System shows the current **display name**, **email**, and **preferred language**.
3. User edits the display name and/or picks a preferred language (DE / EN / FR).
4. System validates and saves.
5. System applies the language **immediately** to the running UI and uses it for subsequent emails.

## Extensions / alternate flows

- **3a. Display name emptied** → blocked; a user must be identifiable to the people they share
  circles with.
- **3b. Changing the email address** → the new address must be **verified** before it replaces the old
  one, and the old address is notified of the change. Until verification, the old address remains the
  account's address. This is a security-relevant flow and will likely need **its own FR** alongside
  password reset.
- **5a. Language changed while offline** → the choice applies locally at once and is pushed on the
  next [sync](../../adr/0016-sync-and-conflict-resolution.md); the UI must not pretend the server
  already knows.
- **Signed-out / account-free users** still pick a display language — that is the existing sidebar
  language picker and is purely local. The profile setting is the *account's* preference and the two
  must not fight: the account preference wins once signed in.

## Exceptions

- **Backend unreachable while saving** → the edit is queued or clearly rejected; it is never reported
  as saved when it was not.

## Non-functional notes

- **Offline/local-first:** profile is account data, so editing it is an online concern, with the
  language choice as the local-first exception noted in **5a**.
- **Localization:** this use case *is* the localization control point for account holders — the
  preference must reach transactional email, not just the UI
  ([NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)).
- **Privacy:** the display name is visible to everyone sharing a circle with the user; the email
  address is **not** exposed to other members — showing it would undo the enumeration resistance
  [ADR-0018](../../adr/0018-circle-and-sharing-model.md) builds into invitations.
- **Performance:** a language switch must re-render without a reload or a flash of the wrong language.

## Decided (2026-07-27)

- **Avatar is out of M1**, decided with **M2** — it is not in the selected v2 scope, and it drags in
  image upload, storage, resizing and moderation for no M1 benefit. Circles are what make faces
  useful.
- **Language on a shared device:** the **account preference wins while signed in**; on sign-out the UI
  reverts to the device/browser preference. A signed-in user's own choice should follow them between
  devices — that is the point of putting it on the account — and a signed-out tablet should not be
  stuck in the last user's language.
- **Display names are not unique** — not within a circle, not globally. Two "Anna"s in one family is
  ordinary, and a uniqueness constraint would force people to adopt names nobody calls them.
  Disambiguation is a UI concern (e.g. show a distinguishing detail when two names collide in the same
  list), not a domain constraint.
- **Changing the email address gets its own FR** in the M1 requirements — it is a
  security-relevant flow (verify-before-swap, notify the old address) rather than an ordinary profile
  field edit. It stays specified here as extension **3b**.

## Open questions

_None outstanding — remaining M1 detail lands in the feature spec._

## Traceability

- Requirements: [FR-012](../../requirements/functional/FR-012-manage-profile.md) (profile & language),
  [FR-013](../../requirements/functional/FR-013-change-email-address.md) (email change, extension 3b);
  constrained by [NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md),
  [NFR-005](../../requirements/non-functional/NFR-005-account-enumeration-resistance.md)
- ADRs: [ADR-0009](../../adr/0009-authentication-and-passkeys.md),
  [ADR-0016](../../adr/0016-sync-and-conflict-resolution.md),
  [ADR-0018](../../adr/0018-circle-and-sharing-model.md)
- Glossary: [User](../../glossary/user.md), [Circle](../../glossary/circle.md)
- Origin: [brainstorming — accounts, families & sync (v2)](../../brainstorming/2026-07-24-accounts-families-sync.md), theme A / slice M1
