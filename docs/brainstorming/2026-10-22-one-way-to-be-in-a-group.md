---
date: 2026-10-22
facilitator: Claude (AI assistant)
participants: [adrian-moll]
topic: One way to be part of a group — analysis and target model
status: Draft
---

# Brainstorming — One way to be part of a group (2026-10-22)

## Objective / framing question

The [database review](2026-10-22-database-and-write-path-review.md) traced R16 and most of its other findings to
one cause: **there are two ways for a person with an account to be in a group**, plus three storage tiers that
grew one amendment at a time. We are not live yet, so this is the moment to replace them properly.

**What is the simplest model in which a group lives in one place, every person reaches it one way, and every
piece of information is stored once?**

## Divergent — raw ideas

### How we got here

| When | Decision | What it added |
|---|---|---|
| 2026-07 | ADR-0016/0017/0033 | Account sync: the organizer's groups as opaque documents in `sync.aggregates` |
| 2026-07 | ADR-0019/0020, FR-028–030 | **Linking:** a member linked to an account; the organizer's device publishes per-giver rows, an event registry and a wishlist snapshot into `linking.*`; the member's own wishlist goes to `member_wishlists` |
| 2026-08 | ADR-0042 | Events; linking keyed by event |
| 2026-09 | ADR-0058, FR-038–043 | **A third tier:** groups shared by link, in `shared.*`, with an admin link, a join link and person claims |
| 2026-09 | ADR-0068 | Live reveal links: the organizer's device publishes per-giver slices into `shared.reveal_slices` |
| 2026-10 | ADR-0073, 0077, 0078 | Repairs where the tiers collided: mode authoritative, retract linking on delete, record which account a group belongs to |
| 2026-10 | ADR-0080, 0081, 0084 | **Participants:** trimmed server view, own entry merged into the document; owned shared groups; own entry from the reveal link |

Each step was reasonable on its own. Together they produced:
- three server stores holding group data (`sync`, `linking`, `shared`);
- two access models for people (linked member, participant);
- **eight** state fields on the client's group record (`Mode`, `AccountScope`, `Tokens`, `ClaimedMemberId`,
  `PendingClaimAccount`, `AdminAccount`, `JoinedAccount`, `PendingEntry`), with helper rules
  (`IsAdminClaimedShared`, `EffectiveMode`) to work out which of four kinds a record really is.

### Today: where a group lives, by user-facing kind

| Kind (UI) | Server copy | People see their assignment through | Own wishlist goes to | Reveal links |
|---|---|---|---|---|
| On this device | none | snapshot reveal link | — (organizer types it) | snapshot |
| Shared by link | `shared.groups` | join-link claim → trimmed view | `shared.member_entries` (merged) | live slices (published by organizer device) |
| My account | `sync.aggregates` | **linking** → `linking.assignments` (published by organizer device) | `linking.member_wishlists` (**not merged**) | snapshot only |
| My account · shared | `shared.groups` (owned) | join-link claim **or** linking | either, depending on the route | live slices |

### Today: what the server holds, and how much is a copy

| Data | Copies | Kept fresh by |
|---|---|---|
| Group document | one per group, in `sync` **or** `shared` | the organizer's devices |
| Draw / pairings | document; `linking.assignments`; `shared.reveal_slices` | organizer device re-publishing every cycle |
| Wishlists | document; `shared.member_entries`; `linking.member_wishlists`; `assignments.RecipientWishlistSnapshot`; reveal slices | merge (entries), never (member_wishlists), re-publish (snapshots) |
| Event names/dates | document; `linking.group_events`; `assignments`; `member_wishlists`; slices | re-publish; never for `member_wishlists` |
| "Who is this member" | document `userId`; `linking.member_links` + `resolutions`; `shared.member_claims` | resolutions drained by the organizer device |

The copies exist because, in July, the server was not supposed to read the group document. That is no longer
true: since ADR-0058/0080 the server reads the shared document to trim it per participant and merge entries,
and NFR-002 already states that in account and shared mode the operator can read the pairings. Every published
copy now protects nothing and costs a sync path, a staleness window and a class of bugs.

### The requirements underneath (what must keep working)

1. Organize alone, offline, with no account; hand out reveal links (FR-001–006, NFR-001).
2. Keep your groups on all your devices (FR-023/024).
3. Let other people in: they see their own pairing in the app and keep their own wishlist and gift note
   (FR-029, FR-030, FR-037, FR-041, FR-050).
4. Bring people in by join link, by email invitation, from a circle, or yourself (FR-027, FR-028, FR-032, FR-041).
5. Reveal links that stay current for groups the server holds (FR-005/006, ADR-0068/0082).
6. One place to see all my assignments and edit my wishlists across groups (FR-029/030).
7. Privacy: a person sees only their own pairing; a local group never touches a server (NFR-002).
8. Organizer powers: release a person, stop sharing, delete; on account deletion decide what happens to owned
   groups (FR-042, FR-043, ADR-0022).
9. Reminders, calendar, hand-out progress (FR-044–046) — device-side, unaffected by storage.

### Target model

**Two places for a group.**
- **On this device:** no server copy, no participants. Exactly today's local group. Nobody can join it; you hand
  out snapshot reveal links.
- **Online:** one server copy, in one group store. "My account", "Shared by link" and "My account · shared"
  become one thing. Whether other people can join is a setting of an online group (the join link on or off),
  not a different storage tier.

**One owner.** An online group has an organizer: the owning account (decision D1 below on whether an
account-free organizer link stays). The organizer's devices read and write the whole document.

**One way to be in a group: a participant.** A participant is one member of the roster, connected to one person:
an account, or, without an account, a device key. Every route creates the same participant row:

| Route | How the member is chosen | Confirmation |
|---|---|---|
| Join link | the person picks themselves (or adds themselves) | first claim locks (as today) |
| Email invitation | the organizer picks the member and an address | the invited account accepts |
| From a circle | the organizer picks circle people | verified by the shared circle, no acceptance |
| Yourself | the organizer picks their own member | none |

A participant sees the trimmed view (their own pairing per event, their recipient's wishlist and gift note,
budget and event details) and keeps their own entry. One member has at most one participant. One account is at
most one member per group.

**One place for a member's own entry.** `member_entries (group, member)`: gift note and per-event wishlists,
written by the participant or by a reveal-link holder for that member (ADR-0084). The server merges it into the
document on every write and every organizer push (one implementation, today's `SharedGroupBody.ApplyEntries`).
While an entry exists the organizer sees those fields read-only; when the person leaves or is released, the row
goes and the merged content stays.

**No published copies; the server derives every view from the document.**
- A participant's view, "my groups / my assignments" across groups, and the reveal page are all computed on read
  from the document plus entries.
- `linking.assignments`, `linking.group_events`, `linking.member_wishlists`, `linking.member_links`,
  `linking.resolutions` and the reveal slice *bodies* go.
- A reveal token stays (it is the secret in the short link), as a row naming (group, event, giver). The page it
  opens is computed fresh, so the "stale until the organizer republishes" window of ADR-0068/0084 disappears.

**A typed group model on the server.** The server reads the document through a typed, versioned Secret-Santa
read model (members, events, draws, wishlists), not through hand-picked JSON property names. That fixes the
"opaque but not really" contract (G5) and is where the document's schema version lives (F2).

**The sync store keeps only per-account documents** (UI preferences). Groups leave it.

### What this removes

| Removed | Replaced by |
|---|---|
| Linking store (5 tables, ~15 endpoints), `AssignmentPublisher`, `LinkResolutionApplier`, resolutions drain/ack | participants + derived reads |
| `sync.aggregates` kind `group`, claim/unclaim routes, tombstones for groups | the one group store |
| Reveal slice publishing (`RevealSlicePublisher`) and slice bodies | reveal token rows + derived reveal read |
| `member_wishlists`, organizer-editable linked members, unlink data loss (R16, C1, C2, C9) | one entry table |
| Two owners for one member (C5), forged links (S1), organizer by claim (S2) | one participant per member; one owner per group |
| Record fields `AccountScope`, `PendingClaimAccount`, `AdminAccount`, `JoinedAccount`, helpers `IsAdminClaimedShared`, `EffectiveMode` | `Place` (device / online) + `Role` (organizer / participant) |
| Four user-facing kinds and their transitions (FR-038 transitions, ADR-0073/0078/0081 repairs) | *On this device* ↔ *Online*, join link on/off |

### Open decisions

- **D1 — Organizing without an account.** Today an account-free organizer holds an admin link (ADR-0058, FR-040,
  FR-043 hand-over).
  - **Keep it:** the owner is an account *or* an organizer token.
  - **Drop it:** organizing an online group needs an account; participants still need none (join link + device
    key). That removes the admin token, claim-admin, release-to-link, rotate-admin, the creator key and the
    account-free co-organizer case.
- **D2 — Existing server data.** Nothing on production is real user data. The new store can start empty (test
  data deleted) instead of migrating three stores into one.
- **D3 — "My assignments" / "My wishlists".** Keep them as two pages over the new overview read, or fold them into
  the group list (a joined group opens your assignment and your wishlist directly).

## Clustering / themes

- **Storage:** two places; one group store; sync store for per-account documents only.
- **People:** one participant model, four routes in; one owner.
- **Data:** document is the only source; entries merged; every view derived; typed, versioned read model.
- **Client:** one record shape, one online sync service, simpler pages.

## Convergent — selection

| Idea | Impact | Effort (low=better) | Fit | Decision |
|------|:------:|:-------------------:|:---:|----------|
| One group store, two places (device / online) | 5 | 3 | 5 | Selected (pending ADR) |
| One participant model, four routes | 5 | 3 | 5 | Selected (pending ADR) |
| Derived reads instead of published copies | 5 | 3 | 5 | Selected (pending ADR) |
| Typed, versioned server read model | 4 | 2 | 5 | Selected (pending ADR) |
| Unify only the own-entry table (old ADR-0085) | 2 | 3 | 2 | Rejected: keeps two models |

## Decisions & selected ideas

Target model above, recorded as [ADR-0086](../adr/0086-one-way-to-be-in-a-group.md) (Proposed). Decided by
adrian-moll on 2026-10-22:

- **D1:** keep account-free organizing. The owner of an online group is an account or an organizer link.
- **D2:** start empty. No migration; test data on production is dropped on deploy.
- **D3:** fold *My assignments* and *My wishlists* into the group list.

## Rejected / parked (for the record)

- **Bridging the two models** (ADR-0085 as rewritten on 2026-10-22): one entry table plus a merge hook in the sync
  store. Rejected: it keeps the linking projection and both access models.
- **Per-person cryptographic secrecy** for online groups: stays parked (NFR-002 future hardening); the target
  model does not make it harder.

## Follow-up actions

- [x] Decide D1–D3 (2026-10-22).
- [x] Accept ADR-0086 (2026-10-22).
- [ ] FEAT-027 (spec + implementation plan) and TP-027.
- [ ] Update FR-023/024, FR-027–030, FR-032, FR-037–043, FR-048, FR-050, NFR-002, glossary (*participant*,
      *organizer*, *online group*), arc42 §5/§8, `docs/diagrams/database-schema.md`.
- [ ] Implement in phases (server store → client store and sync → pages → E2E and Pixel check).
