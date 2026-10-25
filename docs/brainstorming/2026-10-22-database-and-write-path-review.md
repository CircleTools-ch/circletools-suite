---
date: 2026-10-22
facilitator: Claude (AI assistant)
participants: [adrian-moll]
topic: Database design and write-path review
status: Draft
---

# Brainstorming — Database design and write-path review (2026-10-22)

## Objective / framing question

R16 ([2026-10-20 device test](2026-10-20-secret-santa-release-device-test.md), part 3) showed that a linked
member's own wishlist is stored where none of the group's readers look. **Is the same kind of information
kept in one place across the database? Is the design easy to understand, safe to write to, and does it cover
the current use cases and likely future ones?**

Three read-only reviews were run: schema and data model, write paths, and use-case coverage. The findings
below were merged from them. The security findings (S1, S2) and the account-deletion gap (S3) were checked
against the code by hand; the rest are cited from the reviews with file references.

## Divergent — raw ideas

### Inventory

One PostgreSQL database with five EF contexts, each with its own schema:

| Schema | Tables | Role |
|---|---|---|
| `public` | Identity (`AspNet*`), `refresh_token_chains`, `refresh_tokens`, `outbox_messages` | Accounts, sessions, mail |
| `circles` | `circles`, `circle_memberships`, `invitations`, `invitation_declines`, `invitation_attempts` | Circle aggregate (relational, `xmin`) |
| `sync` | `aggregates` (jsonb body), `markers`, `devices`, `account_counters` | **Source of truth** for private *My account* groups |
| `shared` | `groups` (jsonb body), `tokens`, `member_claims`, `member_entries`, `reveal_slices` | **Source of truth** for shared groups; participants' own entries; live reveal pages |
| `linking` | `member_links`, `assignments`, `member_wishlists`, `group_events`, `resolutions` | Projection the organizer's device publishes for account-linked members, plus `member_wishlists` |

### Where a wishlist lives

| Copy | Written by | Merged into the group document? |
|---|---|---|
| Group document (`sync.aggregates.Body` or `shared.groups.Body`, and the device) | Organizer | It *is* the document |
| `shared.member_entries` | Joined participant, reveal-link holder (ADR-0080/0084) | **Yes**: on every write and every organizer push (`SharedGroupStore.cs:304-331`, `:672-705`) |
| `linking.member_wishlists` | Linked member (FR-030) | **No** (R16). Only read by `LinkingStore.ComposeAsync` (`:652-686`) |
| `linking.assignments.RecipientWishlistSnapshot` | Organizer's publish | A snapshot |
| `shared.reveal_slices.Body` | Organizer's publish | A snapshot |

There are two half-versions of the same idea, "a member keeps their own wishlist". They differ in key
(`MemberAccountId` vs `MemberId`), shape (one event per row vs all events plus gift note), and fate (merged
vs never merged). The account-keyed one follows the account, not the member, if it is relinked.

### Can the server merge into a private group's body?

- The body is plain `jsonb` (`SyncDbContext`). It is "opaque" by convention only (ADR-0016/0033); `SharedGroupBody`
  already parses the same format for shared groups.
- Sync is whole-body last-write-wins with client-chosen versions (`SyncStore.cs:316-335`). A one-off server patch
  would be discarded in full by the organizer's next push. It only works if entries are kept separately and
  **re-applied on every push and claim**, as the shared store does. If they are, the merged body comes back in
  the same `/sync` response.
- *On this device* groups have no server copy, so the server can't reach them. `GroupOrganizerCheck` has an
  explicit path for groups stored nowhere, so linked members in such groups appear to be possible.
- The write would span `linking` and `sync` (same database, different contexts).

### Findings

**Security (exploitable today)**

- **S1 — Linking accepts any group id without an organizer check.** `POST /member-links` and `/self`
  (`LinkingEndpoints.cs:88-173`) take the group id from the request. A self-link row makes the caller pass
  `RetractGroupAsync`'s "organizes" test (`LinkingStore.cs:282-290`), which then deletes **every** member's
  `member_wishlists` for that group. Group ids are visible to every join-link holder. `member_links` is also not
  unique per (group, member) for linked rows, so a forged link could make `ComposeAsync` pick a forged wishlist.
- **S2 — `/sync/groups/{id}/claim` accepts any id** (`SyncEndpoints.cs:67-86`), and `GroupOrganizerCheck` trusts
  "held by the caller's account" first (`GroupOrganizerCheck.cs:44-47`). Claiming another group's id makes the
  caller its organizer for publishing assignments and events, and locks the real organizer out (`HeldByOther`).
  The fallback for groups stored nowhere (`publishers.All(p => p == caller)`, `:68`) also grants organizer
  rights when there are no publishers yet.

**Privacy / data protection**

- **S3 — Account deletion leaves the account's `sync` data behind.** Only circle, linking and shared cleanups
  are registered (`Program.cs:93-102`). `sync.aggregates` (full group bodies with names and wishlists),
  `markers`, `devices` and `account_counters` stay. This contradicts ADR-0022. `LinkingStore.CleanUpAccountAsync`
  (`:625`) assumes the opposite.
- Processed `outbox_messages` (with email addresses) are never purged. Refresh-token rows are revoked, never
  deleted.
- FR-014 (data export) is marked *Implemented*, but the export has no groups, wishlists or assignments. The note
  at `AccountEndpoints.cs:895` ("not synced to the server yet") is out of date.

**Consistency**

- **C1 — R16**, and the two own-entry tables (above).
- **C2 — Unlinking and account deletion delete `member_wishlists`** (`LinkingStore.cs:314-316`, `:622`). FR-030
  promises the wishlist returns to the organizer "with content intact"; the comment at `:303-306` says it
  survives on the group document, which never received it.
- **C3 — Lost updates in the shared body.** Push, entry write and reveal-entry write read then write `Body`/`Version`
  without a row lock or concurrency token. Two concurrent writes can drop one member's merge.
- **C4 — The shared-group client pulls before pushing** (`SharedSyncService.cs:1005-1011`). A dirty organizer
  record is overwritten when the server version is higher. Participant writes also raise it, so offline organizer
  edits can be silently lost. `SyncService` pushes first.
- **C5 — One member, two owners.** A member can be linked (`member_wishlists`) and person-claimed
  (`member_entries`). `ClaimPersonAsync` (`SharedGroupStore.cs:489-554`) doesn't refuse linked or organizer members;
  the "taken" list is advisory.
- **C6 — Stale copied names.** `member_wishlists.GroupName` is copied from `member_links` (frozen when the invite
  is created) and shown on *My wishlists*. `EventName` there is sent by the client.
- **C7 — Orphans.** `member_wishlists` and `assignments` rows survive when an event is removed.
- **C8 — Linked-member "is linked" has two readers that disagree** until resolutions are drained: the body's
  `userId` (`TakenMembersAsync`) vs `member_links` (`ComposeAsync`).
- **C9 — "My wishlist is empty" reminder** reads the group document. A linked organizer who filled *My wishlists*
  is told it is empty (follows from R16).

**Understandability**

- `docs/diagrams/database-schema.md` is from 2026-09-15 and misses `reveal_slices`, `member_entries` and newer
  columns.
- Columns named `Token`/`CreatorKey` hold hashes, while `ParticipantKeyHash` is named correctly.
- Glossary gaps or drift:
  - *Organizer*, *Reveal slice* and *Link request* have no entry.
  - *Participant contribution* (glossary) is `member_entries` / "own entry" in code.
  - *Person claim* is `member_claims`, and "claim" also means anonymous-to-account.
  - Account ids are `*AccountId` in most places and `UserId` in a few.
- `SharedGroupDbContext` and the sync docs call the body opaque, but `SharedGroupBody` hard-codes its property
  names for privacy redaction. Renaming a `GroupState` property could leak draws or drop entries, and no test
  pins those names.
- The `"profile"` sync kind is documented but unused; `preferences` is used but undocumented.
- Versioning and timestamps differ per table without a stated rule.
- `member_links` and `resolutions` ids use `Guid.NewGuid()` against ADR-0008 (v7).
- Missing index on `sync.aggregates(AggregateId)`, queried by every linking publish.

**What is fine and should stay**

- One schema per context, cross-context references by value with no foreign keys.
- jsonb for documents and small string lists.
- Hash-plus-cipher token storage.
- `numeric(10,2)` money.
- Cascades inside the circle aggregate.
- Partial unique indexes.
- Circles copying members with only an origin reference.

### Future-readiness (cheap now, expensive later)

- **F1 — More apps on circletools.ch.** `sync.aggregates.Kind = "group"` and `shared.groups` (no kind column)
  assume Secret-Santa. Namespacing now (`secretsanta.group`, with `group` kept as a legacy alias) is one migration.
- **F2 — Old store clients overwriting newer bodies.** `GroupState` has no schema version and drops unknown
  fields, and pushes are last-write-wins. An older Android/iOS build would erase data a newer one added. Needed
  before any richer wishlist item (price, reserved).
- **F3 — One own-entry model** also serves future linked-member gift notes (FR-037, parked) and cross-member nudges.
- **F4 — Schrottwichteln (CR-4)** would need `RecipientMemberId` in the `assignments` and `reveal_slices` keys,
  only if pursued.

## Clustering / themes

- **A — Authorization of server writes:** S1, S2.
- **B — Data protection:** S3, outbox purge, FR-014 export.
- **C — One place for a member's own data:** R16/C1, C2, C5, C8, C9, F3.
- **D — Concurrency in the shared store:** C3, C4.
- **E — Format and naming hygiene:** opaque-body contract, F1, F2, schema doc, glossary, column names.

## Convergent — selection

| Theme | Impact | Effort (low=better) | Fit | Decision |
|------|:------:|:-------------------:|:---:|----------|
| A — Authorization (S1, S2) | 5 | 2 | 5 | Proposed first; needs a decision |
| B — Account deletion purges `sync` (S3) | 5 | 2 | 5 | Proposed; ADR-0022 already requires it |
| C — One own-entry model (R16) | 4 | 4 | 5 | Proposed; replaces ADR-0085's current option 1 |
| D — Shared-store concurrency (C3, C4) | 3 | 3 | 4 | Proposed |
| E — Body versioning (F2) and kinds (F1) | 3 | 2 | 4 | Proposed |
| E — Docs, glossary, naming | 2 | 1 | 4 | Proposed |

## Decisions & selected ideas

- **S1/S2 fixed first (2026-10-22, agreed with adrian-moll).** Done in code:
  - `GroupOrganizerCheck` asks the shared store first, so an id that store holds is decided by its owner or admin
    token only.
  - A *My account* id that several accounts hold belongs only to the account that already wrote its linking rows.
  - Member links now count as writing the group's linking rows, for the "first come" rule of groups the server
    doesn't hold.
  - `POST /member-links`, `/member-links/self` and `/member-links/circle` require that check. The members page sends
    the admin token for shared-by-link groups.
  - Three integration tests: a stranger can't link and so can't retract other members' wishlists; a participant
    can't link in a shared group; claiming someone else's id doesn't make you organizer.
- **R16 direction:** one member-entry table, merged by the server ([ADR-0085](../adr/0085-one-place-for-a-members-own-entry.md),
  rewritten, still Proposed).

## Rejected / parked (for the record)

- Rejected: the organizer's device pulling linked members' wishlists and pushing them back, as the main mechanism
  for server-held groups. It relays through a device data the server already holds. It may still be needed for
  *On this device* groups, which have no server copy.

## Follow-up actions

- [x] S1/S2: organizer check on the member-link routes; `GroupOrganizerCheck` tightened (2026-10-22). Deploy, then
      re-check on production: create a group with "link my account", invite a member, link in a shared group.
- [ ] S1/S2 follow-ups, not done:
  - refuse `/sync/.../claim` for an id another store or account already holds;
  - scope the linking deletes in `PublishAssignmentsAsync`/`PublishGroupEventsAsync` to the organizer;
  - a unique index on `member_links (GroupId, MemberId)` for linked rows;
  - check production for link rows created by someone other than the group's organizer.
- [ ] S3: `SyncAccountCleanup` for account deletion (+ refresh tokens, outbox purge); FR-014 status back to Partial.
- [x] R16: [ADR-0085](../adr/0085-one-place-for-a-members-own-entry.md) rewritten around one own-entry model
      (theme C); waiting for the decision.
- [ ] C3/C4: row lock in the shared store; push before pull for dirty shared records.
- [ ] F2: `GroupState` schema version and unknown-field round-trip; contract test for the names `SharedGroupBody` reads.
- [ ] Docs: update `docs/diagrams/database-schema.md` with a "which copy wins" table; glossary entries.
