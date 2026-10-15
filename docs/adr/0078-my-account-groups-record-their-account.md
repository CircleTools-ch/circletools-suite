---
status: Accepted
date: 2026-10-15
deciders: [Adrian]
tags: [privacy, sync, secret-santa, account, storage-mode]
---

# ADR-0078 — Every "My account" group records which account it belongs to

## Context and problem statement

On an iPad (0.1.113, 2026-10-15) a group showed the **My account** icon while **no one was signed in**.
ADR-0044 promises the opposite: signing out hides account groups from the offline view, and a group
claimed into one account is never shown to another account on the same device (2026-10-14 review).

The visibility filter (`VisibleGroupsService`) and the import prompt (`SignInReconciler`) decide "is this an
account group?" from `SyncedGroupRecord.AccountScope` alone. But since ADR-0058 and ADR-0073, a group can be
in **My account** mode (`Mode = Account`) **without** an `AccountScope`, in three ways:

| How the group became My account | `Mode` | `AccountScope` | Which account? |
|---|---|---|---|
| Claimed through the account store (`SyncService.ClaimAsync`) | Account | set | recorded |
| Marked My account, not uploaded yet (`SyncService.MarkForAccountAsync`, ADR-0073) | Account | `null` | **not recorded** |
| Admin of a shared-by-link group claimed into the account (`SharedSyncService.ClaimAdminToAccountAsync`), or such a group discovered on another device of the account (`MergeOwnedAsync`) (ADR-0058) | Account | `null` | **not recorded** |

The last two rows count as account-free. So they:

1. **stay visible while signed out**, and to **any other account** that signs in on the device, with the
   My account icon (from `Mode`). This is the privacy break the iPad showed;
2. are **offered in the sign-in import prompt** as if made while signed out, where *Discard* would
   delete them locally;
3. in the admin-claimed case, are **also picked up by the auto-claim** (`Mode == Account && AccountScope
   is null`, ADR-0073). This uploads a group that already lives in the shared-group store into the account
   sync store too, so it is held twice. Sharing & sync then picks the wrong transition for it, because that
   screen tells the two kinds apart by `AccountScope`.

**How does the device know which account a My-account group belongs to, and which kind it is, before it
has been claimed through the account store?**

## Decision drivers

- ADR-0044's promise: account data is hidden when signed out, and never shown to another account.
- ADR-0073: `Mode` is the authoritative intent, and the auto-claim acts on it, but only for the kind it can
  claim.
- Local-first, offline: marking a group My account must keep working without a server round-trip.
- No user loses a group. Records already on devices (without the new facts) must stay usable.
- ADR-0016: the sync facts stay in the LocalStore record, not in the domain memento.

## Considered options

1. **Treat `Mode == Account` as account-bound in the filters.** No new data.
2. **Set `AccountScope` in all three paths.**
3. **Record the account per kind: two additive fields on `SyncedGroupRecord`.**

## Decision outcome

**Chosen: option 3.** `SyncedGroupRecord` gains two additive, nullable fields (schema stays v3; an older
record loads with both `null`):

- **`PendingClaimAccount`**: the account an On-this-device group was marked My account for and is waiting
  to be [claimed](../glossary/claim.md) into. `MarkForAccountAsync` now takes the signed-in account and sets
  it. A successful claim clears it, and so does resetting the intent.
- **`AdminAccount`**: the account that claimed admin of a [shared-by-link group](../glossary/shared-by-link-group.md)
  (ADR-0058). `ClaimAdminToAccountAsync` and `MergeOwnedAsync` set it (both now take the account).
  Release-to-link clears it.

The account a group belongs to is `AccountScope ?? AdminAccount ?? PendingClaimAccount`, exposed as
`ClaimableGroup.Owner`. On top of that:

- **Account-free** means *no* owner **and** `Mode != Account`. This feeds the import prompt and the claim
  screen, which no longer offer My account groups.
- **Visibility.** Signed out shows only account-free groups. Signed in shows account-free groups plus the
  ones whose owner is this account. A legacy record (`Mode == Account` with no owner recorded) stays hidden
  while signed out and is shown to a signed-in account, because no owner can be known yet.
- **Auto-claim** takes only `Mode == Account` groups that have no `AccountScope` and no `AdminAccount`, and
  whose `PendingClaimAccount` is the signed-in account. A legacy record with no recorded account is
  claimed only when the same cycle's `MergeOwnedAsync` reached the server. Merge first **backfills**
  `AdminAccount` on every already-held group the account owns as a shared group, so a legacy admin-claimed
  group is never uploaded a second time. `ClaimAsync` itself also refuses a group that has an `AdminAccount`
  or a `PendingClaimAccount` for a different account.

Option 1 would hide the groups when signed out, but it can't tell one account's groups from another's. It
also can't tell an admin-claimed group from one waiting to be claimed, so it doesn't fix point 3. Option 2
would break `AccountScope`'s meaning ("claimed through the account store"). The push, tombstone and
un-claim paths rely on that meaning, so an admin-claimed group would start being pushed and tombstoned
through the account sync store.

## Consequences

- **Positive:** a deleted My account group stays deleted, everywhere. A My account group is never visible
  signed out or to another account. The import prompt
  only offers genuinely account-free groups. An admin-claimed shared group is no longer uploaded into the
  account sync store a second time. Sharing & sync can tell the two My account kinds apart from a recorded
  fact instead of guessing from the tokens.
- **Negative:** a legacy record without a recorded account is shown to whichever account signs in next on
  that device, and is then claimed into or attributed to it. This is the same as today's behaviour, and
  each record goes through it only once.
- **Follow-ups:**
  1. Groups that the old auto-claim already uploaded twice (`AccountScope` set, and owned in the shared
     store) are left as they are. A one-off clean-up would need a server-side check of both stores.
  2. The import prompt still offers **shared-by-link** groups (account-free, but they already have an
     online home). Whether those should be offered, or protected from *Discard*, is a separate question.
- **New risks:** the auto-claim selection now depends on the merge step's success signal. If the merge
  fails, legacy records simply wait for the next cycle, which is the safe direction.

### Amendment (2026-10-15) — deleting a My account group reaches its online copy

Reported the same day on Android and iOS: a deleted "My account" group **disappeared and then came back**.
`SyncingGroupRepository.DeleteAsync` only told the server about groups with an `AccountScope` (a
tombstone). An admin-claimed shared group has none, so its online copy stayed. The next signed-in cycle's
`MergeOwnedAsync` then found it "owned on another device" and downloaded it again. A group the old auto-claim
had put in both stores came back the same way: the tombstone removed the account-store copy, the shared copy
was rediscovered. There was no way for an owner to delete such a copy: the shared-group API only deleted
by admin token, and claiming admin retires that token.

- **Server:** `DELETE /shared/{groupId}/owned` (bearer, owner only), the owner counterpart of the admin-link
  delete (`ISharedGroupStore.DeleteOwnedAsync`). It removes the group, its tokens, claims and reveal slices,
  so **the join link stops working too**, exactly like the admin-link delete. Deleting means gone (decided
  2026-10-15, over handing the group back to a link). Idempotent: an already-gone group is a success.
- **Client:** deleting a My account group that may be online (`Mode == Account` or `AccountScope` set, and
  not still waiting for its claim) queues a `PendingOwnedDelete` (group + owning account) in `SyncState`,
  next to tombstones and linking retractions. This keeps delete offline-safe. The signed-in cycle sends the
  queue (`SharedSyncService.SendOwnedDeletesAsync`) **before** owned-group discovery, and discovery skips
  any group still queued. A delete the server settles (deleted, already gone, or not owned by the caller) is
  dropped from the queue; an unreachable server keeps it queued.

## Pros and cons of the options

### 1. Treat `Mode == Account` as account-bound
- 👍 No data change.
- 👎 Can't tell which account a group belongs to, so a second account on the device still sees it.
- 👎 Doesn't fix the auto-claim uploading admin-claimed groups twice.

### 2. Set `AccountScope` everywhere
- 👍 One field, and every reader already understands it.
- 👎 Overloads a field the push, tombstone and un-claim paths treat as "lives in the account sync store",
  which would send admin-claimed groups through the wrong server path.

### 3. Record the account per kind (chosen)
- 👍 Each My account kind keeps its own meaning, and both the owner and the kind are explicit.
- 👍 Additive and backward compatible. Legacy records resolve on the next signed-in cycle.
- 👎 Two more fields to keep in step on every mode transition (mitigated by regression tests).

## Links

- Amends [ADR-0044](0044-sign-in-reconciliation-and-account-scoped-visibility.md) (visibility rule) and
  [ADR-0073](0073-storage-mode-is-authoritative.md) (auto-claim selection); builds on
  [ADR-0058](0058-account-free-link-shared-groups.md) (claim admin to an account) and
  [ADR-0016](0016-sync-and-conflict-resolution.md) (sync facts in the LocalStore record)
- arc42: 08 Cross-cutting concepts (sync, privacy)
- Requirements: [FR-038](../requirements/functional/FR-038-per-group-storage-mode.md),
  [FR-024](../requirements/functional/FR-024-offline-editing-and-reconciliation.md) (never sent to or shown for
  another account)
