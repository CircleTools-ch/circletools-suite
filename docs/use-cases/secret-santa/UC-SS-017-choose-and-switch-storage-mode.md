---
id: UC-SS-017
title: Choose a group's storage mode at creation and switch it later
app: Secret-Santa
actors: [Organizer]
status: Draft
priority: Should
related: [FR-038, FR-043, UC-SS-018, NFR-001, ADR-0058]
---

# UC-SS-017 — Choose a group's storage mode at creation and switch it later

## Summary / goal

An organizer decides **where a group lives** — one of three modes — and can change that decision at any
time without losing the group. The modes are **On this device** (the private, offline default), **Shared
by link** (stored on the server keyed by a [share token](../../glossary/share-token.md), reachable by an
[admin link](../../glossary/admin-link.md) and a [join link](../../glossary/join-link.md) with no
sign-up — a [Shared-by-link group](../../glossary/shared-by-link-group.md)), and **My account** (synced
to the signed-in account). The choice is offered **at group creation** and is a first-class, reversible
setting afterwards, each switch explained with a short plain-language before → after consequence.

## Primary actor & stakeholders

- **Primary actor:** Organizer (creating or configuring a group).
- **Stakeholders:** co-organizers and participants (a shared mode is what lets them see and act on the
  group at all); the operator (server storage and its abuse/retention surface begin only when a group
  leaves the device); anyone relying on the privacy default (a group must stay private unless the
  organizer deliberately escalates it — [NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md)).

## Preconditions

- The organizer is on the create-group screen, or on an existing group's **Sharing & sync** settings.
- For **My account**, the organizer can sign in (an account exists or can be created).

## Postconditions

- **Success:** the group is in exactly one mode; the mode is shown on the group hub (a chip + the
  Sharing & sync row); switching preserves the group's contents, its [Draw](../../glossary/draw.md) and
  any [person claims](../../glossary/person-claim.md).
- **Failure:** the mode is unchanged and the organizer is told why; nothing is half-migrated.

## Trigger

The organizer picks a mode when creating a group, or opens **Sharing & sync** and changes it.

## Main success scenario

1. Organizer creates a group and is shown the three modes, **On this device pre-selected**, each with a
   one-line explanation.
2. Organizer accepts the default (or picks another) and creates the group.
3. Later, the organizer opens the group's **Sharing & sync** settings and selects a different mode.
4. The app shows a **short consequence** (a before → after line, not legalese) and asks to confirm.
5. On confirm, the app performs the transition and updates the mode chip + Sharing & sync row.

## Extensions / alternate flows

- **1a. Organizer just taps Create** → the group stays **On this device**; nothing is uploaded and the
  privacy default holds (NFR-001).
- **3a. On this device → Shared by link** → *"This uploads the group to CircleTools so people with the
  link can see it. You can switch it back anytime."* On confirm the aggregate is uploaded, a share token
  is minted, and the organizer lands in the **Share group** view ([UC-SS-018](UC-SS-018-share-and-manage-as-admin.md)).
- **3b. On this device / Shared by link → My account (claim admin)** → if signed out, the account
  sign-in opens first; then the group is **claimed** into the account
  ([ADR-0017](../../adr/0017-anonymous-to-account-claim.md)). That account becomes the **sole admin**,
  the admin link retires, and the join link keeps working (detailed in
  [UC-SS-018](UC-SS-018-share-and-manage-as-admin.md)).
- **3c. My account → Shared by link (release-admin)** → so ownership is not a trap. The server copy is
  **re-keyed from the account to a fresh share token**, a **new admin link is minted**, and the account
  stops owning it; [Assignments](../../glossary/assignment.md) and existing person claims are preserved.
  Consequence line: *"This hands the group to anyone with the admin link and removes it from your
  account."*
- **3d. Shared by link / My account → On this device (downgrade)** → *"This removes the online copy and
  any shared links stop working. The group stays on this device."* On confirm the server copy is
  deleted (**not** a [tombstone](../../glossary/sync.md) — the local copy is kept), mirroring today's
  *unclaim*.
- **3e. Organizer cancels at the consequence prompt** → nothing changes.

## Exceptions

- **Backend unreachable during a switch to/among online modes** → the switch fails cleanly, the group
  stays in its current mode, and the organizer is told; account-free on-device use is unaffected (NFR-001).
- **Local storage unavailable** → the app reports the error and does not claim the mode changed.

## Non-functional notes

- **Offline/local-first:** **On this device** is the default and touches no server; escalation is always
  deliberate ([NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md)). The three
  modes are the same shared feature code on Web and the native heads.
- **Privacy:** moving to **Shared by link** means the group (including the draw) now lives on the server
  and its assignment secrecy becomes **trust-bounded**
  ([NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md)); the consequence prompt
  is where that is made plain before it happens.
- **Localization:** the mode labels, one-liners and consequence prompts are DE/EN/FR
  ([NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)).

## Open questions

- Should a group with **other joined people** warn more strongly before a downgrade to On this device
  (which strands them), steering toward release-admin instead? Leaning yes; confirm in the feature spec.

## Traceability

- Requirements: [FR-038](../../requirements/functional/FR-038-per-group-storage-mode.md)
  (mode selection & switching), [FR-043](../../requirements/functional/FR-043-release-ownership-and-deletion-disposition.md)
  (release-admin & deletion disposition)
- Related use case: [UC-SS-018](UC-SS-018-share-and-manage-as-admin.md) (managing the shared group)
- Governing ADR: [ADR-0058](../../adr/0058-account-free-link-shared-groups.md)
- Feature spec: [FEAT-013](../../features/FEAT-013-account-free-link-shared-groups.md)
- Glossary: [Shared-by-link group](../../glossary/shared-by-link-group.md),
  [Admin link](../../glossary/admin-link.md), [Join link](../../glossary/join-link.md),
  [Share token](../../glossary/share-token.md), [Claim](../../glossary/claim.md)
