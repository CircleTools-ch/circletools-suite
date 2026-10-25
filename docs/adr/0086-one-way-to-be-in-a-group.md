---
status: Accepted
date: 2026-10-22
deciders: [adrian-moll]
tags: [architecture, data, sync, sharing, privacy, secret-santa]
---

# ADR-0086 — One way to be in a group: two places, one group store, participants, derived views

## Context and problem statement

Between July and October the way a Secret-Santa group is stored and shared grew one amendment at a time. The
details are in the [analysis](../brainstorming/2026-10-22-one-way-to-be-in-a-group.md) and the
[database review](../brainstorming/2026-10-22-database-and-write-path-review.md). In short:

- **Three server stores hold group data:**
  - `sync.aggregates` for private *My account* groups (ADR-0016/0033);
  - `shared.*` for groups shared by link or owned and shared (ADR-0058/0081);
  - `linking.*`, a projection the organizer's device publishes for account-linked members (ADR-0019/0020/0042).
- **Two access models for a person with an account:**
  - a **linked member** reads rows the organizer's device published, and keeps their wishlist in a table nothing
    merges back;
  - a **participant** reads a server-trimmed view of the document, and keeps an entry the server merges in
    (ADR-0080/0084).
- **Four user-facing kinds:**
  - *On this device*, *Shared by link*, *My account* and *My account · shared*;
  - represented on the client by eight overlapping record fields;
  - repaired by ADR-0073, 0077 and 0078.
- **Published copies everywhere:** assignments, the event registry, wishlist snapshots and reveal slices are
  pushed by the organizer's device and are stale until it re-publishes.

These copies were made because the server was not meant to read the group document. That premise no longer
holds: the server reads the shared document to trim and merge it, and
[NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md) states that in account and shared mode
the operator can read the pairings. The copies now protect nothing. They are the source of R16, the unlink data
loss, two owners for one member, the stale reveal pages, and the authorization gaps S1/S2.

We are not live; production holds only test data.

**What model gives a group one place, every person one way in, and every piece of information one copy?**

## Decision drivers

- **One source of truth:** the group document, stored once.
- **One way to be in a group**, whatever route brought the person in.
- **Derive, don't copy:** every view a person gets is computed from the document on read.
- **Keep the promises:**
  - local-first, account-free solo use (NFR-001);
  - a person sees only their own pairing (NFR-002);
  - account-free taking part *and* account-free organizing (ADR-0058, kept by decision D1).
- **Bounded contexts** ([architecture guidelines](../guidelines/architecture-guidelines.md)): Secret-Santa's
  server logic lives in a Secret-Santa module, not in the platform.
- **Fewer states on the client:** a record says where the group lives and what this device may do, nothing else.

## Considered options

1. **Two places, one group store, participants, derived views** (this ADR).
2. **Bridge the two models**: one entry table and a merge hook in the sync store, linking projection kept (the
   rewritten ADR-0085).
3. **Keep the model and fix defects one by one** (R16, C2, C5, S1/S2, …).

## Decision outcome

**Chosen: option 1.**

### Two places for a group

- **On this device.** There is no server copy and nobody joins. Reveal links carry a snapshot. This is today's
  local group, unchanged.
- **Online.** There is exactly one server copy, in the Secret-Santa group store.
  - *Shared by link*, *My account* and *My account · shared* become one kind.
  - Whether others may join is a **setting** of an online group (its join link is on or off), not a tier.
- The transitions are *make online* (upload) and *keep only on this device* (server copy deleted; participants
  lose access, told so first).

### Access is a list of role assignments

Who may do what in an online group is stored as **role assignments**, not as an owner column on the group. A
role assignment says: this **principal** has this **role** in this group, optionally as this **member** of the
roster.

- **Principals:**
  - an **account**: the group is on all its devices;
  - a **device key**, with no account: the group is on that device only;
  - an **organizer link**: a token, today's admin link, kept for account-free organizing (decision D1). The group
    is on each device that holds the link.
- **Roles, today:**
  - **owner:** reads and writes the whole document, manages participants and links, deletes or hands over the
    group;
  - **participant:** reads the trimmed view, writes their own entry, leaves.
- **Roles, later:** one or more **admins** (co-organizers). The model is built so this is an addition, not a
  rework:
  - endpoints check **permissions**, never role names;
  - one central table maps roles to permissions (read document, write document, manage participants, manage
    links, manage roles, delete group, read own view, write own entry);
  - an admin is then a new row in that table, and further assignment rows;
  - the rule "exactly one owner" is enforced in the application, not by the schema;
  - the apps read a group's permissions from the server instead of deciding from a hard-coded role.
- **Rules:**
  - A principal has at most one role assignment per group.
  - A member has at most one principal.
  - An account is at most one member per group.
  - The organizer who is also in the roster has **one** row: role owner, plus their member.
  - Having a member adds **read own view** to any role (2026-10-22, found in the FEAT-027 walkthrough), so that
    organizer sees their own assignment like any participant. Their own entry stays part of the document they
    edit, so the member does not add **write own entry**.
- **Ownership transfers:**
  - An account can take over a link-owned group, which retires the link (today's claim-admin).
  - The owner can hand the group back to a link (today's release-to-link).
- **Concurrent editors.** Several owner devices, and later several admins, write the same document.
  - Every push carries the version it was based on, and the group records who wrote last.
  - The rule stays newest-write-wins (ADR-0016) for now. Because the base version is on the wire, the server can
    later refuse a stale push and let the client merge, without a protocol change.

### One way in: participants

A **participant** is a role assignment with the participant role and a member: it connects one member of the
roster to one person, through an account or a device key.

Every route in creates the same participant row:

| Route | Member chosen by | Confirmation |
|---|---|---|
| Join link | the person (an existing member, or adds themselves) | the first claim locks it |
| Email invitation | the organizer | the invited account accepts |
| From a circle | the organizer | verified against the shared circle |
| Organizer's own member | the organizer | none |

The owner frees a participant by releasing them; a participant can leave.

A participant reads the **trimmed view**: their own pairing per event; their recipient's wishlist and gift note;
budget and event details. They write only **their own entry**.

### One place for a member's own entry

The table `member_entries (group, member)` holds a gift note and per-event wishlists. Two kinds of writer:
- the member's participant;
- for a member without a participant, the holder of their reveal link (ADR-0084).

The server merges an entry into the document on every entry write and on every organizer push. While an entry
exists, the organizer sees those fields read-only. When the participant leaves or is released, the row goes and
the merged content stays in the document.

### Derived views, no published copies

The server computes every view on read, from the document plus entries:
- the participant view;
- the account's overview of its groups (owned and taking part, with its assignment per event);
- the reveal page.

**Reveal tokens** stay. Each is the secret in a short link (ADR-0082), and becomes a row naming (group, event,
giver). The page it opens is computed fresh, so it is never stale.

Gone: the linking store; the published reveal-slice bodies; `AssignmentPublisher`, `RevealSlicePublisher` and
`LinkResolutionApplier`; resolutions; claim/unclaim of groups into the sync store.

### A Secret-Santa server module with a typed model

- **New module.** `CircleTools.SecretSanta.Service.*` (core, repository, endpoints), hosted by the Main service
  (modular monolith, ADR-0003). It has its own schema: groups, tokens, role assignments, member entries, reveal tokens.
- **Typed document.** The server reads the document through `SecretSanta.Core`'s `GroupState`, the same
  contract the apps write, which has no I/O dependencies. It no longer reads hand-picked JSON property names.
- **Schema version.** The document carries one. A push from a writer older than the stored document is refused,
  so an old app cannot silently drop newer fields.
- **The platform keeps:**
  - accounts and circles;
  - the outbox;
  - the sync store, for per-account documents only (Secret-Santa UI preferences, ADR-0041). Groups leave it.

### The apps

- **The group record says three things:**
  - its **place** (device / online);
  - this device's **access:** the principal (account, organizer link or device key), the role, the member if
    any, and the permissions the server reported;
  - the principal's secret, if it has one (link token or device key).

  Pages decide what to show from permissions, never from the role name, so an admin role later needs no page
  rewrite.

  The fields `AccountScope`, `PendingClaimAccount`, `AdminAccount`, `JoinedAccount`, `ClaimedMemberId` and
  `PendingEntry`, and the helpers built on them, go.
- **One online sync service.**
  - Organizer: push and pull the document.
  - Participant: pull the trimmed view and push the own entry.
  - The device's dirty edits are pushed before pulling (C4).
- **Sign-in shows the account's groups.**
  - Local groups stay local until the user makes them online.
  - The sign-in import of ADR-0044 becomes an offer to make chosen local groups online.
- **One list, every role.** Groups you take part in appear in the group list beside the ones you organize. Opening
  one shows your assignment and your own wishlist and gift note. The separate *My assignments* and *My wishlists*
  pages go (decision D3).

### Privacy

[NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md) collapses from three columns to two:

| | On this device | Online |
|---|---|---|
| Server knowledge of the draw | none | the operator can read it (as today for account and shared groups) |
| Enforced by | absence | authorization: full reads only with the read-document permission (owner today), trimmed reads for participants, token-scoped reveal reads |

The rule that a non-organizer response never carries more than one pairing (ADR-0020) carries over to the
participant view and the reveal read.

### Data

There is no migration (decision D2). The new store starts empty. The `linking` schema, the group rows of
`sync.aggregates` and the `shared` schema are dropped on deploy, and test groups are recreated.

## Consequences

- **Positive:**
  - One copy of every piece of group data; R16, C1, C2, C5, C8 and C9 go away by construction.
  - S1/S2 can't recur, because there is no linking store and no group claim into the sync store.
  - Reveal pages and assignments are always current.
  - Two places instead of four kinds, for users and for code.
  - Roughly the linking store, two publishers, the resolution machinery, claim/unclaim and most of the client
    record's state logic are deleted.
- **Negative:**
  - A large refactor touching the server, both apps, many tests and the E2E suite.
  - The server now depends on the Secret-Santa document contract (deliberately, through `SecretSanta.Core`).
  - Production test data is lost.
- **Supersedes / amends** (marked in each ADR on 2026-10-22):
  - **Supersedes:**
    - [ADR-0017](0017-anonymous-to-account-claim.md) (claim → put online / take over; marked 2026-10-25);
    - [ADR-0019](0019-member-user-linking.md) (linking → participants);
    - [ADR-0020](0020-server-side-assignment-privacy.md)'s storage of published assignments (its
      authorization rules carry over);
    - [ADR-0077](0077-retract-linking-projection-on-group-delete.md);
    - [ADR-0078](0078-my-account-groups-record-their-account.md);
    - [ADR-0085](0085-one-place-for-a-members-own-entry.md).
  - **Amends:**
    - [ADR-0016](0016-sync-and-conflict-resolution.md), [ADR-0033](0033-which-aggregates-sync.md) and
      [ADR-0041](0041-sync-secret-santa-ui-preferences.md): groups leave the sync store;
    - [ADR-0042](0042-events-as-first-class-entities.md): no linking store keyed by event;
    - [ADR-0044](0044-sign-in-reconciliation-and-account-scoped-visibility.md): sign-in offers to make groups
      online;
    - [ADR-0058](0058-account-free-link-shared-groups.md), [ADR-0073](0073-storage-mode-is-authoritative.md),
      [ADR-0081](0081-signed-in-sharing-stays-in-the-account.md) and
      [ADR-0071](0071-default-storage-mode-and-first-run-onboarding.md): two places, join link as a setting;
    - [ADR-0068](0068-live-reveal-links-for-backend-backed-groups.md),
      [ADR-0082](0082-short-reveal-links-for-online-groups.md) and
      [ADR-0084](0084-own-entry-from-the-reveal-link.md): reveal tokens, derived reveal reads;
    - [ADR-0080](0080-participant-access-to-shared-groups.md): every route makes a participant.
- **Follow-ups:**
  - FEAT-027 with an implementation plan, and TP-027.
  - Requirement updates: FR-023/024, FR-027–030, FR-032, FR-037–043, FR-048, FR-050, NFR-002.
  - Glossary: *participant*, *organizer*, *owner*, *role*, *online group*, *organizer link*, *member entry*.
  - arc42 §5/§8 and the database schema diagram.
- **New risks:**
  - The refactor touches every sharing flow. Mitigations: phase it so each phase builds and passes its tests,
    and finish with the E2E suite and a Pixel/browser walkthrough of every route in.

## Pros and cons of the options

### Option 1 — two places, one store, participants, derived views
- 👍 Removes the cause instead of the symptoms; much less code and state.
- 👍 Every view is current; every person comes in one way.
- 👎 Large, cross-cutting change; only sensible before going live (which is now).

### Option 2 — bridge the two models
- 👍 Smaller than option 1.
- 👎 Keeps three stores, two access models, the published copies and their staleness; adds a merge hook to the
  sync store.

### Option 3 — fix defects one by one
- 👍 Small steps.
- 👎 Each fix adds another amendment to a model already too complex to reason about; the review found a dozen
  more of the same kind.

## Links

- Analysis: [One way to be part of a group](../brainstorming/2026-10-22-one-way-to-be-in-a-group.md);
  [database and write-path review](../brainstorming/2026-10-22-database-and-write-path-review.md)
- Related ADRs: see *Supersedes / amends* above; [ADR-0003](0003-modular-monolith-with-aspire.md) (modular monolith)
- arc42 chapters: 05 Building-block view, 06 Runtime view, 08 Crosscutting concepts
- Requirements: [NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md),
  [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md),
  [FR-028](../requirements/functional/FR-028-link-member-to-account.md),
  [FR-030](../requirements/functional/FR-030-edit-my-wishlist.md),
  [FR-038](../requirements/functional/FR-038-per-group-storage-mode.md),
  [FR-041](../requirements/functional/FR-041-join-link-and-person-claim.md)
