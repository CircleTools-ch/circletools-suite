---
status: Accepted
date: 2026-09-01
deciders: [Adrian]
tags: [architecture, domain, secret-santa, ddd, history]
---

# ADR-0052 — Member archival and permanent draw history

> **Amended 2026-10-31 (owner decision, legal-page review LR-5).** "Remove" no longer keeps a member's
> data for history. It always deletes the member's exclusions, per-event wishlists and gift note. A member
> who appears in **no** draw (nor a carried-forward baseline) is then **deleted outright**; only a member a
> draw names is archived, and then just the name is kept so that draw still reads. Reason: the deletion
> page promises that removing a person deletes them, and "remove them if they ask you to" is the only
> lever an organizer has for someone else's data. A past occasion therefore no longer shows a removed
> member's wishlist.

## Context and problem statement

A Secret-Santa [Group](../glossary/group.md) owns a **shared** roster of
[Members](../glossary/member.md) reused across its [Events](../glossary/event.md) (ADR-0042). Two
things about how the roster and its [Draws](../glossary/draw.md) interact turned out to work against
the product's own promise — that a group is a *lasting* record of exchanges across years:

- **Removing a member destroyed history.** `Group.RemoveMember` deleted the member from the roster
  *and* cleared every event's draw. A past occasion that had already been drawn lost both the person
  and the result: the reveal/assignment name resolves through `Group.FindMember`, which no longer
  found the removed member, and the draw itself was gone. You could not look back at "who did Grandma
  get in 2025" once Grandma left the group.
- **Adding a member also destroyed history.** The same "a draw belongs to an exact member set"
  invariant cleared **every** event's draw on *any* roster change — so adding a cousin for the 2026
  exchange silently wiped the completed 2025 draw.

The organizer's mental model is the opposite: **the roster evolves, but past occasions are a record.**
Deleting someone should drop them from *future* occasions while leaving the ones already drawn intact;
renaming someone should reach the past occasions too (it is the same person); and adding someone for
next year must not disturb last year.

So: **how do we let the roster change over time without ever destroying a completed draw, in a shared
single-aggregate model that still syncs as one opaque document (ADR-0033)?**

## Decision drivers

- **Past draws are permanent history.** A completed draw is a real result the organizer may want to
  see for years. No roster edit may delete one.
- **A rename is the same identity.** Renaming a member must show everywhere the member appears,
  including past draws — so members must keep a stable identity that name resolution reads.
- **Removal is "no longer participating", not "never existed".** A removed member must stay resolvable
  for the draws that already include them, but must not appear in the roster editor or take part in any
  new draw.
- **Stay one sync aggregate, migrate in place.** The change must be additive to the group document
  (ADR-0016/0033 store it opaquely) so nothing on the server changes and every existing group loads
  unchanged.
- **A stale draw stays viewable, but is flagged when shared.** Keeping the draw is pointless if its
  links can no longer be reached, so the reveal links must keep generating for a past occasion (the
  archived member's name resolves). What changed is not *whether* links are produced but that the UI
  must **warn** the organizer, wherever a stale draw is shown or shared, that it no longer matches the
  current roster and a re-draw is one click away.

## Considered options

1. **Archive members + permanent draws (chosen).** A member gains an `IsArchived` flag. "Remove"
   archives: the member is kept in the aggregate (so `FindMember` still resolves their name and a
   rename still reaches them) but is excluded from the active roster (`Group.Members`), from draw
   generation, and from uniqueness checks. A roster change **flags** each event's current draw as no
   longer matching the roster (`DrawInvalidatedByMembershipChange`) but **keeps** it; a re-draw clears
   the flag. Reveal links keep generating for the kept draw — the UI warns they are stale rather than
   blocking them, so a past occasion's links stay reachable.
2. **Per-event participant snapshots.** Each event freezes its own participant list at creation/draw,
   so a group-level removal only affects future events. More faithful to "an undrawn existing event
   keeps its members", but a much larger change: a new per-event membership structure, its migration,
   a sync-document schema change, and UI to manage participants per event. Rejected as disproportionate
   to the stated need — the requirement is about *past drawn* occasions, which option 1 already keeps.
3. **Soft-delete without the stale flag.** Archive members but leave draws silently as-is on a roster
   change. Rejected: an organizer who added a member and shared the old links would hand out a set that
   omits the newcomer, with no signal that a re-draw is needed.

## Decision

**Option 1.** `Member.IsArchived`; `Group.RemoveMember` archives instead of deleting and drops the
member's exclusions but keeps their per-event wishlists; `Group.Members` returns the **active** roster
while `Group.FindMember` searches all members (active + archived); draw generation, name/account
uniqueness, and the two-member minimum all read the active roster. `Event.MarkRosterChanged` replaces
the old `InvalidateDraws` — it sets the stale flag without clearing the draw, and `AppendDraw` clears
it. `GetRevealLinks` still returns the kept draw's links — the archived member's name resolves — while
the UI warns they are stale (the pre-existing null guard still protects a genuinely broken legacy
draw). The change is additive to the memento (`MemberState.IsArchived`, defaulting to `false`), so
pre-archival snapshots load as all-active and the server's opaque document store is untouched.

The UI follows: the members editor shows only active members and its remove confirmation says the
person stays in occasions already drawn; the draw and share screens show a completed-but-stale draw
with a "re-draw to include everyone" notice (share still lists the links, so history stays reachable);
the group and event badges show "re-draw needed" ahead of "drawn" when a draw is stale.

## Consequences

- 👍 No roster edit ever destroys a completed draw; a removed member is still visible in the occasions
  they were part of, and a rename reaches those occasions.
- 👍 Additive and opaque to sync — no server change, every existing group loads unchanged, and an
  archived member round-trips through the memento.
- 👍 A re-added name or re-linked account is allowed, because uniqueness is enforced among active
  members only.
- 👎/👍 The shared-roster model means a roster change flags **every** event's draw stale in the domain.
  To keep that from being noise, the **read model suppresses the flag for a past occasion** (its date is
  behind us — it is history, not something to re-draw), surfacing "re-draw needed" only for undated or
  upcoming occasions; the query handlers take a `TimeProvider` for this. A finer scoping (per-event
  participants, option 2) is still deferred until there is evidence it matters.
- 👎 Archived members accumulate in the group document forever. Acceptable at the expected scale (a
  family roster); a compaction pass is a later option if it ever isn't.
- 👎 A member archived while linked to an account keeps their account link in history; their
  server-side assignment projection for a past event is left in place (a linked person can still see
  what they once got). No new server behaviour, but worth knowing.

## Links

- Amends: [ADR-0042](0042-events-as-first-class-entities.md) (events; the roster-change invalidation
  it defined is now a keep-and-flag, not a clear).
- Related: [ADR-0016](0016-sync-and-conflict-resolution.md) / [ADR-0033](0033-which-aggregates-sync.md)
  (opaque single-aggregate sync — unchanged), [ADR-0014](0014-reveal-link-encoding-and-privacy.md)
  (reveal links — kept but flagged stale), [ADR-0019](0019-member-user-linking.md),
  [ADR-0020](0020-server-side-assignment-privacy.md).
- Glossary: [Member](../glossary/member.md), [Draw](../glossary/draw.md), [Event](../glossary/event.md),
  [Group](../glossary/group.md).
