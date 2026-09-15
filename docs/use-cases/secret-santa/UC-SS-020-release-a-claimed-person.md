---
id: UC-SS-020
title: Free (release) a claimed person so they can rejoin
app: Secret-Santa
actors: [Admin]
status: Approved
priority: Could
related: [FR-042, FR-041, UC-SS-019, ADR-0058]
---

# UC-SS-020 — Free (release) a claimed person so they can rejoin

## Summary / goal

In a [Shared-by-link group](../../glossary/shared-by-link-group.md), a
[Member](../../glossary/member.md) that has been claimed is **locked** and cannot be claimed again
([person claim](../../glossary/person-claim.md), first-claim-locks). An admin can **free** a claimed
member — the **single recovery path** for a new or lost device, or a mis-claim — after which the
[join link](../../glossary/join-link.md) can claim that member afresh.

## Primary actor & stakeholders

- **Primary actor:** Admin (admin-link holder or the owning account).
- **Stakeholders:** the person who lost access (got a new phone, or claimed the wrong member) and needs
  back in; other participants (a release must not disturb their own claims or assignments).

## Preconditions

- The group is in **Shared by link** (or **My account**) mode and has at least one claimed member.
- The actor holds the admin capability.

## Postconditions

- **Success:** the chosen member is **unclaimed** (no account or device holds it); it becomes claimable
  again via the join link. Other members' claims and the [Draw](../../glossary/draw.md) are untouched.
- **Failure:** nothing changes and the admin is told why.

## Trigger

The admin opens the **Members** screen and chooses to free a claimed member.

## Main success scenario

1. Admin opens **Members**; each member shows its claim state — **claimed (account)**, **claimed (this
   device)**, or **open** (not yet claimed).
2. Admin selects a claimed member and chooses **Free this person**.
3. The app explains the effect — *"They'll lose access until they rejoin with the join link"* — and asks
   to confirm.
4. On confirm, the member is unclaimed; its state returns to **open**.
5. The freed person opens the join link again and claims that member ([UC-SS-019](UC-SS-019-join-a-shared-group.md)).

## Extensions / alternate flows

- **2a. The member is an account claim** → freeing removes the account's binding to *this member*; it
  does **not** delete the person's account or touch their other groups.
- **2b. The member is not claimed** → there is nothing to free; the option is not offered.
- **3a. Admin cancels** → the member stays claimed.
- **5a. Someone else claims the freed member first** → first claim wins again; the intended person must
  ask the admin to free it once more (a trust-context edge, accepted).

## Exceptions

- **Backend unreachable** → the release cannot complete; the admin is told to retry and no half-release
  is left behind.
- **Actor lost admin (e.g. link regenerated)** → the action is unavailable; only a current admin can
  free a member.

## Non-functional notes

- **Privacy:** releasing does not expose any assignment; it only clears a binding. The freed member's
  assignment is unchanged and is shown again only to whoever next claims that member
  ([NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md)).
- **Localization:** the claim-state labels, the **Free this person** action and its confirmation are
  DE/EN/FR ([NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)).

## Open questions

- Should freeing be logged/visible to the member who held it (so a device-only person understands why
  they were signed out)? Minor; decide in the feature spec.

## Traceability

- Requirements: [FR-042](../../requirements/functional/FR-042-release-a-claimed-person.md)
  (admin release), [FR-041](../../requirements/functional/FR-041-join-link-and-person-claim.md)
  (person claim & locking)
- Related use case: [UC-SS-019](UC-SS-019-join-a-shared-group.md)
- Governing ADR: [ADR-0058](../../adr/0058-account-free-link-shared-groups.md)
- Feature spec: [FEAT-013](../../features/FEAT-013-account-free-link-shared-groups.md)
- Glossary: [Person claim](../../glossary/person-claim.md), [Member](../../glossary/member.md),
  [Join link](../../glossary/join-link.md), [Shared-by-link group](../../glossary/shared-by-link-group.md)
