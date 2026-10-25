---
status: Accepted
date: 2026-10-17
deciders: [Adrian]
tags: [architecture, privacy, security, sharing, secret-santa]
---

# ADR-0084 — The reveal link lets its holder keep their own wishlist and gift note (live-link groups)

> **Amended by [ADR-0086](0086-one-way-to-be-in-a-group.md) (2026-10-22).** The reveal-link entry is a member entry like a participant's, only for a member without a participant.

## Context and problem statement

The reveal page ends with *"You will receive a gift too: tell the organizer what you would like, so your
own Secret Santa has ideas."* For most people who open a reveal link — no account, no join — that is the
only way their wishes reach their own Secret Santa: by message to the organizer, who types them in.
Joined participants already maintain their own entry (gift note + per-event wishlist) themselves
([ADR-0080](0080-participant-access-to-shared-groups.md)), and backend-backed groups already
serve each giver's reveal slice live by its reveal token ([ADR-0068](0068-live-reveal-links-for-backend-backed-groups.md)).

May the holder of a reveal link edit **their own** wishlist and gift note from the reveal page, and if so,
what does the reveal token then authorize, and who owns that entry afterwards?

## Decision drivers

- **The link is the secret** (ADR-0014/0058/0068): a reveal token already proves "I am this giver for this
  event". Anything it unlocks must stay within that one person.
- **NFR-002 — the server never learns the draw.** It may know which member a token belongs to (the giver),
  never whom they give to.
- **Reuse ADR-0080's entry model**: one participant entry per member, merged into the stored body and
  re-applied on every organizer push, so a stale organizer copy cannot undo it.
- **Account-free and abuse-capped** (NFR-009): anonymous writes are rate-limited and size-limited.
- **No silent overwrites** between the person and the organizer.

## Considered options

1. **Write own entry by reveal token, live-link groups only; the person owns the entry** — the reveal token
   gains one write: this member's gift note and this event's wishlist.
2. **Same, but last write wins** between person and organizer.
3. **Also give private "My account" groups live reveal links** so every synced group gets the feature.
4. **Keep the reveal link read-only** and only fix the copy.

## Decision outcome

**Chosen: option 1**, because it closes the loop for the largest group of people (link holders without an
account) with the token they already hold, reuses ADR-0080's entry storage and merge rules unchanged, and
keeps ownership unambiguous.

- **Scope.** Only groups whose reveal links are live: *Shared by link* and *My account · shared* (they have
  a reveal-slice store). On-this-device groups and private *My account* groups keep snapshot links and show
  no editor; their reveal page keeps the "tell the organizer" hint, which is accurate there.
- **What the token authorizes.** `GET /api/v1/shared/reveal/{revealToken}/me` reads, and
  `PUT /api/v1/shared/reveal/{revealToken}/me` replaces, the token's member's **gift note** (group-wide) and
  **wishlist for the token's event**. Nothing else: no other member, no other event's wishlist, no claim.
  Both are anonymous, rate-limited and entry-size-capped like `PUT /{token}/me`.
- **Which member.** The organizer's client already publishes each slice under a stable key; it now also
  sends the slice's **event id and giver member id** with it. The server stores both beside the slice. They
  name only the giver — the recipient stays inside the opaque body (NFR-002 holds).
- **Storage.** The write upserts the member's `SharedMemberEntry` (merging this event's wishlist into any
  other events already there), applies it to the stored body, and bumps the group version — exactly what an
  ADR-0080 participant write does. Re-pushes by an organizer re-apply it.
- **Ownership.** Once a member has an entry, the organizer's copy of that member's wishlist and gift note is
  read-only (*"X keeps this up to date themselves"*), as for a joined participant. The claims answer gains a
  `selfMaintained` list (additive) so the organizer's device knows. Releasing the member — the existing
  admin/owner release — deletes the entry and hands the fields back.
- **Not for linked members.** A member linked to an account keeps their wishes through *My wishlists*
  (FR-030); the reveal token gets no editor for them (the read answers 404) to avoid two sources.
- **Freshness.** The giver's slice is opaque and computed by the organizer's client, so the giver sees an
  edit once an organizer device next syncs and republishes (the eventual freshness ADR-0068 accepted). The
  server does not patch slices: that would require knowing the pairing.

## Consequences

- **Positive:** people without an account can keep their own wishes current; the organizer no longer
  re-types them; one entry model for joined participants and link holders.
- **Negative:** a forwarded or leaked reveal link lets its holder change that person's wishlist and gift
  note (it already let them read the assignment); an edit reaches the giver only after an organizer device
  syncs.
- **Follow-ups:** FR-050, FEAT-026, TP-026; nullable `EventId`/`MemberId` columns on reveal slices
  (migration); client publishes them; reveal-page editor (DE/EN/FR); organizer read-only for self-maintained
  members.
- **New risks:** slices published by an older client carry no member/event and show no editor until the
  organizer's next publish (any reconcile).

## Pros and cons of the options

### Option 1 — write by reveal token, person owns
- 👍 Uses the token the person already has; no account, no join.
- 👍 ADR-0080's storage, merge and re-apply rules unchanged; no silent overwrite.
- 👎 Organizer can't correct the wishlist without releasing the member.

### Option 2 — last write wins
- 👍 Organizer keeps full control.
- 👎 An organizer edit on a stale copy silently replaces what the person wrote — the bug class the
  2026-10-17 browser pass just fixed on the wishlists page.

### Option 3 — also private "My account" groups
- 👍 Every synced group gets the feature.
- 👎 Those groups would need an online copy in the shared store — a storage-model change (ADR-0078/0081).

### Option 4 — read-only link
- 👍 No new write surface.
- 👎 Keeps the dead end the 2026-10-17 pass flagged.

## Links

- Related ADRs: [ADR-0014](0014-reveal-link-encoding-and-privacy.md), [ADR-0058](0058-account-free-link-shared-groups.md),
  [ADR-0061](0061-share-token-and-creator-key-at-rest-protection.md), [ADR-0068](0068-live-reveal-links-for-backend-backed-groups.md),
  [ADR-0080](0080-participant-access-to-shared-groups.md), [ADR-0082](0082-short-reveal-links-for-online-groups.md)
- Requirements / use cases: [FR-050](../requirements/functional/FR-050-edit-own-entry-from-reveal-link.md),
  [UC-SS-006](../use-cases/secret-santa/UC-SS-006-open-reveal-link.md), [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md)
- Feature: [FEAT-026](../features/FEAT-026-own-entry-from-the-reveal-link.md)
- Source: backlog BTP-11 ([2026-10-17 browser pass](../roadmap/backlog.md))
