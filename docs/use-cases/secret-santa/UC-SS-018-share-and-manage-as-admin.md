---
id: UC-SS-018
title: Share and manage a shared-by-link group as an admin
app: Secret-Santa
actors: [Organizer, Admin]
status: Draft
priority: Should
related: [FR-040, FR-043, FR-039, NFR-009, UC-SS-017, UC-SS-019, ADR-0058, ADR-0017]
---

# UC-SS-018 — Share and manage a shared-by-link group as an admin

## Summary / goal

In a [Shared-by-link group](../../glossary/shared-by-link-group.md), an admin distributes the two links
and manages the group's sharing. The [admin link](../../glossary/admin-link.md) grants the organizer
capability (members, exclusions, budget, run/redo the [Draw](../../glossary/draw.md), manage sharing);
the [join link](../../glossary/join-link.md) lets people join as participants
([UC-SS-019](UC-SS-019-join-a-shared-group.md)). The admin can **disable and regenerate** the admin
link, and can **claim admin to an account** to become the group's sole owner.

## Primary actor & stakeholders

- **Primary actor:** Admin (any holder of the admin link, or the owning account).
- **Stakeholders:** co-organizers (may share the admin capability); participants (receive the join
  link); the operator (regeneration and claim bound the exposure of leaked links).

## Preconditions

- The group is in **Shared by link** mode (a share token exists) — reached via
  [UC-SS-017](UC-SS-017-choose-and-switch-storage-mode.md).
- The actor holds the admin capability (opened the admin link, or owns the account before a downgrade).

## Postconditions

- **Success:** links are shared; a regenerated admin link invalidates the previous one; a claimed-admin
  account is the sole admin and the old admin link no longer resolves.
- **Failure:** sharing state is unchanged and the actor is told why.

## Trigger

The admin opens **Sharing & sync** for the group.

## Main success scenario

1. Admin opens **Sharing & sync** and sees the **admin link** and the **join link**, each with
   **Share**, **Copy**, and **Show QR** (the same seam the reveal links use).
2. Admin **shares the join link** with participants (native share sheet where available, else Copy/QR).
3. Admin, if co-organizing, **shares the admin link** with another organizer — the admin capability is
   **shared**, so several people can hold it at once.

## Extensions / alternate flows

- **1a. Admin link may have leaked** → the admin chooses **Disable & create a new one**; the previous
  [share token](../../glossary/share-token.md) for the admin link stops resolving and a fresh admin link
  is minted (the join link is unaffected).
- **1b. Admin wants sole ownership** → **Claim admin to my account**: the admin signs in (or creates an
  account), the group is claimed ([ADR-0017](../../adr/0017-anonymous-to-account-claim.md)), that
  account becomes the **sole admin**, and the group moves to **My account** mode. From then the admin
  link **does not work** — a holder opening it sees *"&lt;name&gt; has claimed the admin role for this
  group"* — while the **join link keeps working** and participants are unaffected.
- **1c. A holder opens a retired admin link** → they are shown who claimed admin and offered the join
  link instead ([UC-SS-019](UC-SS-019-join-a-shared-group.md)); they are not granted any admin ability.
- **1d. Admin hands the group back** → **release-admin** returns an account-owned group to shared-by-link
  and mints a fresh admin link ([UC-SS-017](UC-SS-017-choose-and-switch-storage-mode.md), 3c).
- **2a. Desktop web (no share sheet)** → Copy and Show QR only, as elsewhere in the app.

## Exceptions

- **Backend unreachable** → sharing actions that need the server (regenerate, claim admin) fail cleanly
  and are retryable; showing/copying an already-issued link still works.
- **Claim-admin race** → if two people try to claim admin at once, exactly one succeeds and becomes sole
  admin; the other is told admin is already claimed.

## Non-functional notes

- **Privacy / security:** the link **is** the credential — holding the admin link grants the admin
  capability, the same "the link is the secret" model as a [reveal link](../../glossary/reveal-link.md),
  scaled to the whole group ([ADR-0058](../../adr/0058-account-free-link-shared-groups.md)). Regeneration
  is the escape when a link reaches the wrong hands.
- **Abuse / retention:** minting and regenerating tokens sits behind the anti-abuse and retention limits
  ([NFR-009](../../requirements/non-functional/NFR-009-account-free-write-abuse-resistance.md)).
- **Localization:** link labels, the QR dialog, the disable/claim actions and the retired-link message
  are DE/EN/FR ([NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)).

## Open questions

- Should the admin link and join link support **read/edit grades** beyond the current two capabilities?
  Parked in the brainstorming; v1 ships exactly the admin link + join link.

## Traceability

- Requirements: [FR-040](../../requirements/functional/FR-040-admin-link-capability.md)
  (admin link capability & lifecycle), [FR-039](../../requirements/functional/FR-039-shared-by-link-storage-and-sync.md)
  (shared-by-link storage), [FR-043](../../requirements/functional/FR-043-release-ownership-and-deletion-disposition.md)
  (release-admin)
- Related use cases: [UC-SS-017](UC-SS-017-choose-and-switch-storage-mode.md),
  [UC-SS-019](UC-SS-019-join-a-shared-group.md)
- Governing ADR: [ADR-0058](../../adr/0058-account-free-link-shared-groups.md),
  [ADR-0017](../../adr/0017-anonymous-to-account-claim.md) (claim shape)
- Feature spec: [FEAT-013](../../features/FEAT-013-account-free-link-shared-groups.md)
- Glossary: [Admin link](../../glossary/admin-link.md), [Share token](../../glossary/share-token.md),
  [Claim](../../glossary/claim.md), [Reveal link](../../glossary/reveal-link.md),
  [Shared-by-link group](../../glossary/shared-by-link-group.md)
