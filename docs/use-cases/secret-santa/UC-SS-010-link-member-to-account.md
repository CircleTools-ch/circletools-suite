---
id: UC-SS-010
title: Link a member to an account
app: Secret-Santa
actors: [Organizer, Member]
status: Approved
priority: Must
related: [FR-028, NFR-002, NFR-005, ADR-0019, ADR-0020, ADR-0018]
---

# UC-SS-010 — Link a member to an account

## Summary / goal

An organizer connects an individual [Member](../../glossary/member.md) of a
[Group](../../glossary/group.md) to a suite [User](../../glossary/user.md) account, so that person can
see their own [Assignment](../../glossary/assignment.md) in the app
([UC-SS-011](UC-SS-011-see-my-assignment.md)) and own their
[Wishlist](../../glossary/wishlist.md) ([UC-SS-012](UC-SS-012-edit-my-wishlist.md)) instead of the
organizer maintaining it for them. This is the **ad-hoc** path, with no circle involved; the
[circle path](UC-SS-009-create-group-from-circle.md) establishes the same link implicitly.

## Primary actor & stakeholders

- **Primary actor:** the **Organizer**, who initiates a link.
- **Secondary actor:** the **Member** being linked — a real person with an account, whose
  **consent completes the link**. A link cannot be established unilaterally: attaching someone's
  account to a member is both an identity claim and a privacy change (they gain read access to an
  assignment), so the account holder must confirm it.
- **Stakeholders & interests:** the linked person, who must not be misattributed and must not be
  probed for account existence; other members, whose assignments must stay unreadable to the newly
  linked user.

## Preconditions

- A group exists with the member to be linked ([UC-SS-002](UC-SS-002-manage-members.md)).
- The organizer is signed in and the group is account-scoped (a link is only meaningful for a synced
  group — [ADR-0033](../../adr/0033-which-aggregates-sync.md)).

## Postconditions

- **Success:** the member holds an optional reference to the user
  ([ADR-0019](../../adr/0019-member-user-linking.md)); wishlist ownership has moved to that user; if
  the draw has run, that user can now read **only their own** assignment
  ([ADR-0020](../../adr/0020-server-side-assignment-privacy.md)).
- **Failure:** the member is unchanged and still unlinked; nothing about the group's account-free
  behaviour is affected.

## Trigger

The organizer opens a member and chooses **link to an account** — or unlinks one that was linked.

## Main success scenario

1. Organizer opens a member and chooses **link to an account**, entering the person's email address.
2. System sends a **link request** to that address and tells the organizer *"request sent"* — it does
   **not** reveal whether the address has an account
   ([NFR-005](../../requirements/non-functional/NFR-005-account-enumeration-resistance.md),
   [D5](../../brainstorming/2026-07-24-accounts-families-sync.md)). The member shows as
   **link pending** meanwhile.
3. The recipient, signed in, sees the request and **accepts** it. Only on acceptance does the member
   gain the user reference and become **linked**.
4. From then on that member is served differently: wishlist ownership is theirs
   ([UC-SS-012](UC-SS-012-edit-my-wishlist.md)), and — once the draw has run — they see their
   assignment in-app ([UC-SS-011](UC-SS-011-see-my-assignment.md)). The organizer keeps every
   organizer right; **the link confers none** ([ADR-0019](../../adr/0019-member-user-linking.md)).

## Extensions / alternate flows

- **1a. Linking a managed member** → not allowed directly. A
  [managed member](../../glossary/managed-member.md) has no account by definition; they must first be
  **promoted** to an account in the circle ([UC-MAIN-011](../main/UC-MAIN-011-manage-managed-members.md),
  [ADR-0018](../../adr/0018-circle-and-sharing-model.md)), which supplies the user reference and links
  them at that point.
- **1b. The member is already linked** → the "link" action is replaced by **unlink** (see below);
  re-linking to a different account requires unlinking first.
- **3a. The recipient declines or never responds** → the member stays unlinked; the request expires.
  The organizer sees only that it is no longer pending, never why — declining must not become an
  existence oracle either.
- **4a. The same user is already linked to another member in this group** → rejected at the
  application layer: **one user, one member per group** — a person cannot give a gift to themselves
  twice ([ADR-0019](../../adr/0019-member-user-linking.md)). The domain cannot enforce this alone (it
  holds only an identifier), so the check lives in the application layer.
- **Unlink.** The organizer unlinks a member → it reverts to unlinked behaviour: wishlist ownership
  returns to the organizer, the user can no longer read that assignment in-app, and the member and
  their wishlist content are **not** deleted. Reveal links become the way that person is served again
  ([UC-SS-005](UC-SS-005-share-reveal-links.md)).

## Exceptions

- **The linked user deletes their account** ([UC-MAIN-006](../main/UC-MAIN-006-delete-account-and-export-data.md))
  → the member must **degrade gracefully to unlinked** rather than break the group. Account deletion
  therefore has to **unlink** every member that referenced it — a cross-context cascade the M4 feature
  spec must specify ([ADR-0019](../../adr/0019-member-user-linking.md) consequences). The member, its
  name and its wishlist survive; only the reference is cleared.
- **The link request write fails while marking the member pending** → the request is not recorded and
  is reported ([ADR-0029](../../adr/0029-storage-write-failure-contract.md)); the member stays
  unlinked.

## Non-functional notes

- **Offline/local-first:** unlinked members are the account-free product in full
  ([NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md)); a **mixed** group —
  some members linked, most not — is the normal case and needs no special handling. Linking is
  additive.
- **Localization:** DE/EN/FR for the link/unlink flows and the *"request sent"* / *"link pending"*
  states.
- **Privacy:** linking grants a specific person **read access to one assignment** once the draw runs,
  enforced server-side ([NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md),
  [ADR-0020](../../adr/0020-server-side-assignment-privacy.md)); and the link request must never leak
  whether an address has an account
  ([NFR-005](../../requirements/non-functional/NFR-005-account-enumeration-resistance.md)).
- **Performance:** linking and unlinking are single small writes; no draw is recomputed.

## Open questions

- **Does the ad-hoc link request reuse the circle
  [Invitation](../../glossary/invitation.md) mechanism or get its own?** Both need "send to an email,
  resolve on the recipient's side, reveal nothing to the sender", so the enumeration-resistant
  machinery is identical. Recommendation: **reuse the invitation infrastructure** (rate limits,
  expiry, revoke) rather than build a parallel one, but scoped to a member-link rather than a circle
  join. To be settled in the M4 feature spec — it may become a small ADR if the shapes diverge.
- **Should a link ever be possible without the member's consent** (e.g. the organizer *is* that
  person, linking themselves)? Recommendation: **self-link is auto-accepted** — the signed-in
  organizer linking their own account needs no second confirmation — while any other account always
  requires acceptance. Confirm in the feature spec.

## Traceability

- Requirement: [FR-028](../../requirements/functional/FR-028-link-member-to-account.md); constrained
  by [NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md),
  [NFR-005](../../requirements/non-functional/NFR-005-account-enumeration-resistance.md)
- ADRs: [ADR-0019](../../adr/0019-member-user-linking.md) (optional `UserId`, unlink, duplicate-link
  guard, deletion cascade), [ADR-0020](../../adr/0020-server-side-assignment-privacy.md)
  (what the link grants), [ADR-0018](../../adr/0018-circle-and-sharing-model.md) (managed-member
  promotion)
- Related use cases: [UC-SS-009](UC-SS-009-create-group-from-circle.md),
  [UC-SS-011](UC-SS-011-see-my-assignment.md), [UC-SS-012](UC-SS-012-edit-my-wishlist.md),
  [UC-MAIN-011](../main/UC-MAIN-011-manage-managed-members.md),
  [UC-MAIN-006](../main/UC-MAIN-006-delete-account-and-export-data.md)
- Glossary: [Member](../../glossary/member.md), [User](../../glossary/user.md),
  [Wishlist](../../glossary/wishlist.md), [Assignment](../../glossary/assignment.md)
- Origin: [brainstorming — accounts, families & sync (v2)](../../brainstorming/2026-07-24-accounts-families-sync.md),
  decision D3
