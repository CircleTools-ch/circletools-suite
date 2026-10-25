---
status: Superseded by ADR-0086
date: 2026-10-22
deciders: [adrian-moll]
tags: [architecture, data, sync, sharing, linking, secret-santa]
---

# ADR-0085 — One place for what a member keeps themselves

> **Superseded by [ADR-0086](0086-one-way-to-be-in-a-group.md) before it was accepted.** This proposal kept both access
> models (linked member and participant) and bridged them; ADR-0086 replaces them with one.

## Context and problem statement

**The group document is the source of truth.** A Secret-Santa group is one document: members, events, draws and
wishlists. The organizer's devices hold it, and the server holds one copy:
- `sync.aggregates` for a private *My account* group ([ADR-0016](0016-sync-and-conflict-resolution.md),
  [ADR-0033](0033-which-aggregates-sync.md));
- `shared.groups` for a shared one ([ADR-0058](0058-account-free-link-shared-groups.md),
  [ADR-0081](0081-signed-in-sharing-stays-in-the-account.md));
- none for an *On this device* group.

Everything a giver reads is built from that document:
- the shared copy a joined participant reads;
- the reveal links, snapshot or live ([ADR-0014](0014-reveal-link-encoding-and-privacy.md),
  [ADR-0068](0068-live-reveal-links-for-backend-backed-groups.md));
- the linking projection the organizer's device publishes for account-linked givers (`linking.assignments`,
  including `RecipientWishlistSnapshot`).

**Some fields belong to the member, not the organizer.** A member can keep their own wishlist (per event) and
gift note. Today there are **two separate mechanisms** for that one idea:

| | Joined participant / reveal-link holder | Linked member |
|---|---|---|
| Decided in | [ADR-0080](0080-participant-access-to-shared-groups.md), [ADR-0084](0084-own-entry-from-the-reveal-link.md) | [FR-030](../requirements/functional/FR-030-edit-my-wishlist.md), [ADR-0019](0019-member-user-linking.md) |
| Edited on | the group page after joining, or the reveal page | *My wishlists* |
| Stored in | `shared.member_entries`, keyed by **member**: gift note + all events' wishlists | `linking.member_wishlists`, keyed by **account**: one event's wishlist per row |
| Reaches the group document? | **Yes.** The server merges it into the body on every write and every organizer push | **No.** The linking projection only flows device → server |
| Read by | everything built from the document | only `LinkingStore.ComposeAsync`, i.e. an account-linked giver's *My assignments* |

The second mechanism writes into a *copy*, so nothing built from the document ever sees it. The
[2026-10-21 re-check](../brainstorming/2026-10-20-secret-santa-release-device-test.md) found this as R16: the
organizer (member "Adri", linked to the same account) filled *My wishlists*, the group's *Wishlists* page showed
the row empty, and Ben, who joined by link and gives to Adri, saw "No wishlist yet". Every linked member is
affected the same way.

The [2026-10-22 database review](../brainstorming/2026-10-22-database-and-write-path-review.md) found the same
split behind several more defects:
- **The organizer can still edit a linked member's wishlist** on the group page, against FR-030.
  `ConnectedMembers.KeepsOwnEntry` covers joined and self-maintained members only.
- **There can be two live versions of one wishlist.** If the organizer edits it, givers who read the document see
  the organizer's text, while account-linked givers keep seeing the `member_wishlists` text.
- **Unlinking and account deletion destroy the content.** They delete the `member_wishlists` row, and the document
  never had it. FR-030 promises it returns to the organizer "with content intact".
- **One member can have two owners**, linked and person-claimed at the same time. Nothing refuses it.
- **The "My wishlist is empty" reminder** reads the document, so it is wrong for a linked organizer.
- **The `member_wishlists` row follows the account, not the member**, and shows group names frozen at invite time.

**Where does a member's own entry live, so that it reaches every reader of the group, whichever store holds the
group?**

## Decision drivers

- **Same kind of information, same place.** One table, one shape, one owner rule for "what a member keeps
  themselves", whichever way they edit it.
- **The group document stays the single source of truth.** A member's entry is merged into it; every reader
  keeps reading the document.
- **No relaying through a device.** Where the server holds the document, the server merges the entry; an
  organizer's device must not fetch data from the server only to push it back.
- **Keep FR-030's ownership rule.** While the member keeps the entry, the organizer sees it but does not edit it;
  when the member stops (unlink, release, leave), the content stays and the organizer can edit it again.
- **Sync stays correct.** The sync store keeps whole-body last-write-wins
  ([ADR-0016](0016-sync-and-conflict-resolution.md)); a merge must survive an organizer push.
- **NFR-002.** The server already reads both bodies for redaction and merging; this adds no access to the draw.
- **No re-entry.** Wishlists already saved, like "Kaffee", must arrive without typing them again.

## Considered options

1. **One member-entry store, merged by the server into whichever store holds the document.** Linked members,
   joined participants and reveal-link holders all write it. For *On this device* groups, which have no server
   copy, the organizer's device reads the entries and applies them locally.
2. **Unify only for shared groups.** A linked member's save in a shared group becomes a member entry. Private
   *My account* groups keep `member_wishlists` and `ComposeAsync`.
3. **The organizer's device pulls linked wishlists and pushes them back.** This was the earlier proposal of this
   ADR.
4. **Leave it and document** that *My wishlists* reaches account-linked givers only.

## Decision outcome

**Proposed: option 1**, because it is the only option that puts the same information in one place and reaches
every reader, without relaying data through a device the server already holds.

- **One table.** It stores a member's own entry: `(GroupId, MemberId)` → gift note (nullable), wishlists per event,
  `Version`, `UpdatedAt`, and who keeps it (linked account, participant claim, or reveal link). It replaces
  `shared.member_entries` and `linking.member_wishlists`.
  - It sits behind one interface (`IMemberEntryStore`) that both group stores use.
  - The schema and the final table name follow the glossary decision below.
- **Every route writes the same table:**
  - *My wishlists*, for a linked member. It is authorized by the `member_links` row in state *Linked* and
    resolved to the member id there.
  - `PUT /shared/{token}/me`, for a joined participant.
  - `PUT /shared/reveal/{token}/me`, for a reveal-link holder.
- **One owner per member.** A member is kept either by a linked account or by a person claim, never both.
  - A join-link claim of a linked member is refused.
  - Linking a claimed member waits until the claim is released.
- **The server merges entries into the document wherever it holds it:**
  - **Shared groups:** as today. The entry is applied on write and re-applied on every organizer push.
  - **Private *My account* groups:** the sync store gets the same rule.
    - Entries are re-applied on every push and claim of a `group` aggregate.
    - A member's write updates the stored body and gives it a new version and sequence number. The organizer's
      devices then pull it on their next `/sync`, and a push answers with the merged body in the same response.
    - The sync store stays generic: the merge is a body hook the Secret-Santa module registers for the
      `group` kind, the same code `SharedGroupBody.ApplyEntries` runs today. This amends ADR-0016/0033's "the
      platform never reads the body" to "the platform reads it only through hooks a module registers".
  - **The entry and the body change in one database transaction.** Both stores share the database, so they share
    the connection.
- ***On this device* groups.** They have no server copy, so they are the one case where a device fetches.
  - The organizer's device reads the entries of the groups it organizes (an organizer-only read, checked by
    `GroupOrganizerCheck`) and applies them to its local document with the same rule.
  - The next publish carries them on as usual.
- **Readers don't change.** Everything keeps reading the group document.
  - `ComposeAsync` reads the entry table instead of `member_wishlists`, so account-linked givers stay current
    between organizer publishes.
  - The snapshot in `assignments` and the reveal slices refresh on the organizer's next publish, as accepted in
    ADR-0084.
- **The organizer's view.** A member with an entry is shown read-only on the group pages (*"X keeps this up to
  date themselves"*), linked members included. The client learns this from the claims answer's `selfMaintained`
  list, extended to linked members.
- **When the member stops keeping it** (unlink, account deletion, release, leave):
  - The entry row is deleted, and the content already merged into the document stays. The fields become
    editable for the organizer again, with nothing lost.
  - For *On this device* groups, the `Unlinked` resolution carries the last entry, so the organizer's device gets
    it even if it never pulled.
- ***My wishlists*** reads and writes the entry table. Group and event names come from the published event
  registry (`group_events`), not from copies.
- **Migration.** Each `member_wishlists` row moves into the new table under its `MemberId` (from `member_links`),
  then `member_wishlists` is dropped. The next organizer push merges the moved rows, so "Kaffee" and "QA-Buch"
  arrive without re-entry.

## Consequences

- **Positive:** a member's own entry lives in one place and reaches every reader, whatever kind of group it is in.
  - R16 is fixed for every linked member.
  - FR-030's "organizer sees, doesn't edit, keeps it after unlink" finally holds.
  - Two owners for one member become impossible.
  - The reminder check becomes correct.
- **Positive:** a linked member's own gift note (FR-037, parked) needs no new storage; the column exists.
- **Negative:** the sync store now changes a stored body on the server. ADR-0016/0033 need an amendment, and the
  merge has to keep working under the whole-body last-write-wins rule (re-applied on every push, never a
  one-off patch).
- **Negative:** a migration that moves data between schemas, and a client change for *On this device* groups.
- **Follow-ups:**
  - amendments to ADR-0016/0033 (body hook), ADR-0019/FR-030 (where the own wishlist lives) and ADR-0080
    (one owner);
  - the glossary decision: *participant contribution* vs *own entry*, and the table name;
  - `IMemberEntryStore`, the migration and the sync-store hook, with integration tests (private group, shared
    group, unlink keeps content, claim of a linked member refused);
  - client: read-only rows for linked members, *My wishlists* on the new endpoint, the entry read for
    *On this device* groups;
  - a TP entry, then a Pixel and browser re-check of R16: organizer, joined participant, reveal link,
    account-linked giver.
- **New risks:**
  - The merge depends on the body's property names (`members`, `events`, `wishlists`, `giftNote`). The review
    asks for a contract test pinning them, and a schema version on the group document, before this lands.
  - An organizer edit made offline before a member linked can be replaced by the member's entry. That is the
    ownership rule.

## Pros and cons of the options

### Option 1 — one store, merged by the server wherever it holds the document
- 👍 Same information in one place; one owner rule; every reader and every group kind.
- 👍 No device relay for server-held groups; the device fetches only where there is no server copy.
- 👍 Existing rows move over in the migration, with no re-entry.
- 👎 The sync store learns to merge bodies (through a registered hook); there is a migration across schemas.

### Option 2 — unify only for shared groups
- 👍 Smaller: the shared store already merges.
- 👎 Private *My account* groups keep the split, so R16, the unlink data loss and the organizer edit stay there.
- 👎 Two models remain, which is exactly what made R16 hard to see.

### Option 3 — the organizer's device pulls and pushes back
- 👍 No server change to the sync store.
- 👎 Relays through a device data the server already has; reaching givers waits for an organizer device.
- 👎 Keeps two tables for one idea.

### Option 4 — leave it and document
- 👍 No work.
- 👎 A member's wishlist silently fails to reach most givers, against FR-030.

## Links

- Related ADRs: [ADR-0016](0016-sync-and-conflict-resolution.md) and [ADR-0033](0033-which-aggregates-sync.md)
  (sync model; amended by this),
  [ADR-0019](0019-member-user-linking.md) (member–user linking, wishlist ownership),
  [ADR-0042](0042-events-as-first-class-entities.md) (per-event wishlists),
  [ADR-0058](0058-account-free-link-shared-groups.md) and [ADR-0081](0081-signed-in-sharing-stays-in-the-account.md)
  (where a group is stored),
  [ADR-0080](0080-participant-access-to-shared-groups.md) and [ADR-0084](0084-own-entry-from-the-reveal-link.md)
  (participant entries),
  [ADR-0068](0068-live-reveal-links-for-backend-backed-groups.md) (live reveal slices)
- arc42 chapters: 05 Building-block view, 06 Runtime view, 08 Crosscutting concepts
- Requirements / use cases: [FR-030](../requirements/functional/FR-030-edit-my-wishlist.md),
  [FR-028](../requirements/functional/FR-028-link-member-to-account.md),
  [FR-037](../requirements/functional/FR-037-member-gift-note.md),
  [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md)
- Found in: [2026-10-20 release device test, part 3 (R16)](../brainstorming/2026-10-20-secret-santa-release-device-test.md),
  [2026-10-22 database and write-path review](../brainstorming/2026-10-22-database-and-write-path-review.md)
