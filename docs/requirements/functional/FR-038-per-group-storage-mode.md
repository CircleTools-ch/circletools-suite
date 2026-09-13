---
id: FR-038
title: A group has one of three storage modes, chosen at creation and switchable
type: functional
status: Draft
priority: Must
source: UC-SS-017
verification: test
related: [UC-SS-017, FR-039, FR-040, FR-041, FR-043, NFR-001, ADR-0058, ADR-0015, ADR-0017]
---

# FR-038 — A group has one of three storage modes, chosen at creation and switchable

## Description

Every [Group](../../glossary/group.md) is in **exactly one** of three storage modes, and the mode is a
first-class, per-group choice ([ADR-0058](../../adr/0058-account-free-link-shared-groups.md)):

- **On this device** *(default)* — the group lives only in local storage / a local file
  ([ADR-0015](../../adr/0015-mvp-client-offline-storage.md)); nothing crosses to the server; sharing is
  via per-person [reveal links](../../glossary/reveal-link.md) only.
- **Shared by link** — the group lives on the server keyed by a [share token](../../glossary/share-token.md)
  with **no account**, reachable by an [admin link](../../glossary/admin-link.md) and a
  [join link](../../glossary/join-link.md) (see [FR-039](FR-039-shared-by-link-storage-and-sync.md)).
- **My account** — the group is owned by the signed-in [User](../../glossary/user.md) and synced across
  their devices (today's account sync).

Rules:

- **The choice is offered at group creation, with *On this device* pre-selected.** Anyone who simply
  creates a group stays local, so [NFR-001](../non-functional/NFR-001-local-first-offline.md) holds for
  anyone who does nothing; a user who already wants an account or a shared link can pick it up front.
- **The mode is changeable anytime** from the group's **Sharing & sync** settings.
- **Every switch is preceded by a short, plain-language consequence** — a one-line before → after, never
  legalese — and the switch is confirmed before it happens.
- **All transitions are supported and reversible:**
  - *On this device → Shared by link* — uploads the aggregate, mints a share token and links.
  - *On this device / Shared by link → My account* — signs in and [claims](../../glossary/claim.md) the
    group into the account (retires the admin link; the join link keeps working —
    [FR-040](FR-040-admin-link-capability.md)).
  - *My account → Shared by link* (**release-admin**, [FR-043](FR-043-release-ownership-and-deletion-disposition.md))
    — re-keys the server copy from the account to a fresh share token and mints an admin link; the
    account stops owning it.
  - *Shared by link / My account → On this device* (**downgrade**) — the server copy is **removed** and
    shared links stop working; the **local copy is kept**. This mirrors today's *unclaim* — a delete of
    the server copy, **not** a [tombstone](../../glossary/tombstone.md) that would delete it everywhere.

## Rationale

The jump from private-offline to shared-collaborative used to also be a jump to *sign up*. Three
explicit per-group modes let a group grow from local, to link-shared without an account, to
account-synced — matching how families actually use it (one group local, another shared) — while
keeping the account-free, local-first default intact. Making every switch reversible and prefaced with
a plain consequence keeps it low-anxiety, so ownership is never a trap
([ADR-0058](../../adr/0058-account-free-link-shared-groups.md)).

## Acceptance criteria

```gherkin
Given a user creates a new group and does not change the storage option
When the group is created
Then it is in "On this device" mode and nothing is sent to the server

Given the create-group screen
When it is shown
Then all three modes are offered with a one-line explanation, and "On this device" is pre-selected

Given a group in "On this device" mode
When the user switches it to "Shared by link" and confirms the consequence prompt
Then the aggregate is uploaded, a share token and links are created, and the mode is "Shared by link"

Given a group in "My account" mode
When the owner chooses "release to shared by link" and confirms
Then the server copy is re-keyed to a fresh share token, an admin link is minted, and the account no longer owns it

Given a group in "Shared by link" or "My account" mode
When the user downgrades it to "On this device" and confirms
Then the server copy is removed and its shared links stop working, and the local copy remains on the device

Given any mode switch
When the user initiates it
Then a short plain-language before → after consequence is shown and the switch happens only on confirmation
```

## Dependencies & related

- Derived from: [UC-SS-017](../../use-cases/secret-santa/UC-SS-017-choose-and-switch-storage-mode.md)
- Related: [FR-039](FR-039-shared-by-link-storage-and-sync.md) (shared-by-link storage),
  [FR-040](FR-040-admin-link-capability.md) (admin link lifecycle),
  [FR-041](FR-041-join-link-and-person-claim.md) (join link),
  [FR-043](FR-043-release-ownership-and-deletion-disposition.md) (release-admin & deletion disposition),
  [NFR-001](../non-functional/NFR-001-local-first-offline.md) (default preserved)
- Governing ADR: [ADR-0058](../../adr/0058-account-free-link-shared-groups.md); reuses
  [ADR-0015](../../adr/0015-mvp-client-offline-storage.md) (local store) and
  [ADR-0017](../../adr/0017-anonymous-to-account-claim.md) (claim shape)
- Implemented by: [FEAT-013](../../features/FEAT-013-account-free-link-shared-groups.md)
- Glossary: [Group](../../glossary/group.md), [Shared-by-link group](../../glossary/shared-by-link-group.md),
  [Share token](../../glossary/share-token.md), [Admin link](../../glossary/admin-link.md),
  [Join link](../../glossary/join-link.md), [Claim](../../glossary/claim.md)
