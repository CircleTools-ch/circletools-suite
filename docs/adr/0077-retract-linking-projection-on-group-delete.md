---
status: Superseded by ADR-0086
date: 2026-10-14
deciders: [Adrian]
tags: [privacy, sync, linking, secret-santa, account]
---

# ADR-0077 — Deleting a group retracts its server-side linking projection; device-only groups do not self-link

> **Superseded by [ADR-0086](0086-one-way-to-be-in-a-group.md) (2026-10-22).** There is no linking projection any more; deleting an online group deletes its one server copy.

## Context and problem statement

The 2026-10-14 on-device review ([brainstorming](../brainstorming/2026-10-14-secret-santa-android-ux-review.md),
finding B3) found that a deleted group **stays on "My assignments" and "My wishlists"**.

- Signed in, `Groups.CreateNamedAsync` adds the organizer as a member and **self-links** their account
  (FR-032) for *every* new group — including **On this device** ones.
- Once any member is linked, the sync cycle's `AssignmentPublisher` publishes the group's linking projection
  (ADR-0042): the event registry (group + event names, dates) and, after a draw, each linked giver's
  assignment row (recipient name, wishlist, gift note, budget).
- `DeleteGroupCommand` only deletes the local record (plus a sync tombstone for account-claimed groups). The
  linking store has **no retract path**, so the projection is orphaned forever and keeps showing up for every
  linked member.

This breaks two promises. A deleted group must be gone. And **ADR-0073** promises that "On this device"
stays on the device, with the create sheet saying "Privat – wird nirgends geteilt". Yet the automatic
self-link put that group's names and the organizer's own assignment on the server.

**How should a delete reach the server-side projection, and should a device-only group publish at all?**

## Decision drivers

- **Delete means delete** (privacy, NFR-004): no personal data may outlive the group on the server.
- **Modes mean what they say** (ADR-0073): "On this device" must not silently leave the device.
- **Local-first / offline** (NFR-001): deleting must work offline; the server catches up later.
- **Least privilege** (NFR-002): only the organizer can retract their group's projection.
- Small, testable change on a sensitive path.

## Considered options

1. **Retract only via the account-sync tombstone.** The server's sync endpoint clears the projection when it
   applies a group tombstone. This only covers account-claimed groups. Device-only and shared-by-link groups
   with linked members have no tombstone, so they would stay orphaned.
2. **Call the server synchronously on delete.** This fails offline, and the delete would then either block or
   orphan the rows silently.
3. **(Chosen) A dedicated, idempotent retract endpoint, driven by a client-side queue, plus no automatic
   self-link for device-only groups.**

## Decision outcome

**Chosen: option 3.**

- **Server:** `DELETE /api/v1/groups/{id}/linking` (`ILinkingStore.RetractGroupAsync`). For a group the caller
  **organizes**, it deletes the event registry, the assignment rows, the member-links, the pending
  resolutions and the members' self-wishlists, all in one transaction. A caller who organizes nothing there
  changes nothing and still gets `204`, so the endpoint is no existence oracle. Calling it twice is safe.
- **Client:** `SyncingGroupRepository.DeleteAsync` queues the group ID in `SyncState.Retractions` whenever
  the deleted group had a linked member, whatever its storage mode. `SyncCoordinator` sends the queue on the
  next signed-in cycle and keeps any ID whose call failed. This works like tombstones: the delete works
  offline and the server catches up. The field is additive, so older state documents load with an empty
  queue.
- **Create:** an **On this device** group still gets the signed-in organizer as a member, but **no automatic
  self-link**. The organizer can still link explicitly from Members (a deliberate act of sharing).
  "Shared by link" and "My account" groups keep the self-link, because they live on the server anyway.

## Consequences

- **Positive:** a deleted group disappears from every linked member's My assignments / My wishlists. A
  device-only group created while signed in no longer reaches the server, which makes the "Privat" label
  true.
- **Behaviour change:** the organizer of a new device-only group no longer sees that group on their own
  My assignments / My wishlists. As organizer they already see everything inside the group.
- **Accepted gap:** groups deleted *before* this change are in no queue, so their orphaned rows remain until
  they are cleaned up manually (the test account has two: "Testq" and "UI-Test Familie").
- **Accepted gap:** a member linked explicitly in a device-only group still publishes, by design: the
  organizer chose to share with that account. Retraction on delete covers that case.

## Pros and cons of the options

### 1. Tombstone-only
- 👍 No new endpoint.
- 👎 Misses every account-free group with links, which is exactly the observed case.

### 2. Synchronous call on delete
- 👍 Simple.
- 👎 Breaks offline delete, or orphans the rows silently when offline.

### 3. Retract endpoint + queue + no automatic self-link (chosen)
- 👍 Covers every storage mode; works offline; idempotent; organizer-scoped.
- 👎 One more endpoint and one more queued item in the sync state.

## Links

- Refines [ADR-0042](0042-events-as-first-class-entities.md) (the projection now has a delete path) and
  [ADR-0073](0073-storage-mode-is-authoritative.md) (device-only now also means no automatic self-link).
- Privacy basis: [ADR-0020](0020-server-side-assignment-privacy.md).
- Source: [2026-10-14 Android UX review](../brainstorming/2026-10-14-secret-santa-android-ux-review.md), B3.
