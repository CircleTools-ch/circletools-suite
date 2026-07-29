---
id: UC-MAIN-006
title: Delete the account and export personal data
app: Main
actors: [User]
status: Approved
priority: Must
related: [NFR-001, NFR-002, ADR-0017, ADR-0018, ADR-0019, ADR-0020, ADR-0022]
---

# UC-MAIN-006 — Delete the account and export personal data

## Summary / goal

A [User](../../glossary/user.md) obtains a machine-readable copy of the personal data the suite holds
about them, and can delete their account entirely. Once the suite stores personal data server-side,
this stops being a nice-to-have: it is a **legal obligation** (revDSG / GDPR) and was selected in the
v2 scope on exactly those grounds.

## Primary actor & stakeholders

- **Primary actor:** User, signed in.
- **Stakeholders:** the operator (legal duty, and must not destroy *other* users' data while honouring
  one user's deletion); co-members of shared circles (their groups must survive someone leaving).

## Preconditions

- The user is signed in and has **re-authenticated recently** (deletion is not something a borrowed
  session should be able to do).

## Postconditions

- **Success (export):** the user holds an archive of their profile, circle memberships, and the
  Secret-Santa data attributable to them.
- **Success (deletion):** the account, credentials, passkeys, profile and sessions are gone;
  server-side data attributable solely to the user is deleted; **local data on the user's devices is
  untouched** and the apps keep working account-free.
- **Failure:** nothing is deleted and the user is told why.

## Trigger

The user opens **Profile → Data & account** and chooses **Export my data** or **Delete my account**.

## Main success scenario — export

1. User selects **Export my data**.
2. System assembles the export: profile, circle memberships, groups the user owns, their own
   [Wishlist](../../glossary/wishlist.md) entries, and the
   [Assignments](../../glossary/assignment.md) the user is entitled to see.
3. System delivers the archive in a **machine-readable** format (JSON), either as a direct download or
   via a link sent to the account's address.
4. User receives the archive.

## Main success scenario — deletion

1. User selects **Delete my account**.
2. System explains **precisely what will and will not be deleted** — in particular that local device
   data stays, and that groups other people own will keep the members the user was linked to.
3. User confirms by re-authenticating (password or passkey).
4. System deletes the account, its credentials and passkeys, and revokes every session and refresh
   token.
5. System removes the `UserId` link from any Secret-Santa [Members](../../glossary/member.md) that
   pointed at this account, leaving the member itself in place
   ([ADR-0019](../../adr/0019-member-user-linking.md)).
6. System confirms deletion by email to the (now removed) address.

## Extensions / alternate flows

- **Deletion 2a. The user is the sole owner of a circle** → they must either **transfer ownership** to
  another admin or accept that the circle is deleted with them. This interaction is defined by the
  circle roles in [ADR-0018](../../adr/0018-circle-and-sharing-model.md) and its UI lands in **M2** —
  but M1 must not ship a deletion that silently orphans a circle.
- **Deletion 5a. Completed draws referencing the user** → a finished
  [Draw](../../glossary/draw.md) is history and is **not** rewritten. The organizer's group keeps the
  member record (a name in someone else's list); only the account link disappears
  ([ADR-0018](../../adr/0018-circle-and-sharing-model.md), snapshot-at-draw).
- **Deletion 3a. User cancels at the confirmation** → nothing happens.
- **Export 3a. The archive is large** → generated asynchronously and delivered by link; the user is
  told it is being prepared rather than left staring at a spinner.
- **After deletion, the user re-signs-up with the same email** → an entirely new account with no
  connection to the old one. Nothing is restored.

## Exceptions

- **Deletion partially fails** → the account must not be left half-deleted; the operation is
  reported as failed and retryable, and the user is not told their data is gone when it is not.
- **Backend unreachable** → neither flow is available; local data and the account-free experience are
  unaffected ([NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md)).

## Non-functional notes

- **Offline/local-first:** deleting the account is explicitly **not** a device wipe. Per
  [ADR-0017](../../adr/0017-anonymous-to-account-claim.md), local data survives signing out, and the
  same must hold for deletion — otherwise an "additive" account turns out to be destructive at the
  exit.
- **Localization:** the confirmation screens and emails in DE/EN/FR; the export's *content* is data,
  not translated prose.
- **Privacy:** the export must include the assignments visible to the user but must **not** become a
  way to read assignments they could not otherwise see —
  [NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md) and
  [ADR-0020](../../adr/0020-server-side-assignment-privacy.md) apply to the export endpoint exactly as
  they do to the UI. **This is the most likely place to accidentally leak a draw.**
- **Performance:** export may be asynchronous; deletion should be prompt and its effect on sessions
  immediate.

## Decided — see [ADR-0022](../../adr/0022-account-deletion-and-data-export.md) (2026-07-27)

- **Deletion is immediate.** No grace period, no soft-delete state, no undo server-side. This is
  defensible precisely because [ADR-0017](../../adr/0017-anonymous-to-account-claim.md) keeps the
  local device copy: a mistaken deletion costs sync and circle memberships, not data.
- **The export mirrors in-app visibility** — it reuses the
  [ADR-0020](../../adr/0020-server-side-assignment-privacy.md) authorization boundary rather than
  defining a second one, which is what stops it becoming a second place for the assignment-privacy
  rule to be got wrong.
- **Managed members survive** — a [Managed member](../../glossary/managed-member.md) is circle data,
  not the creating user's property. The detail lands in **M2**, where managed members are built.
- **Export links are short-lived and single-use**, because hard deletion means an emailed archive can
  otherwise outlive the account it describes.

## Open questions

- **NFR-004 (data protection & retention) does not exist yet** and is the **first task of the M1
  requirements step**: it must cover this use case, ADR-0022, the 30-day purge of unverified pending
  accounts ([UC-MAIN-001](UC-MAIN-001-create-account.md)), and Swiss data residency once hosting is
  decided. Deliberately not written here — requirements are the next phase, not this one.
- The export's concrete **JSON shape** → M1 feature spec.

## Traceability

- Requirements: [FR-014](../../requirements/functional/FR-014-export-personal-data.md) (export),
  [FR-015](../../requirements/functional/FR-015-delete-account.md) (deletion); constrained by
  [NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md),
  [NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md),
  [NFR-004](../../requirements/non-functional/NFR-004-data-protection-and-retention.md)
- ADRs: **[ADR-0022](../../adr/0022-account-deletion-and-data-export.md)** (the decision for this use
  case), [ADR-0017](../../adr/0017-anonymous-to-account-claim.md),
  [ADR-0018](../../adr/0018-circle-and-sharing-model.md),
  [ADR-0019](../../adr/0019-member-user-linking.md),
  [ADR-0020](../../adr/0020-server-side-assignment-privacy.md)
- Glossary: [User](../../glossary/user.md), [Member](../../glossary/member.md),
  [Managed member](../../glossary/managed-member.md), [Assignment](../../glossary/assignment.md),
  [Wishlist](../../glossary/wishlist.md), [Draw](../../glossary/draw.md)
- Origin: [brainstorming — accounts, families & sync (v2)](../../brainstorming/2026-07-24-accounts-families-sync.md), theme A / slice M1
