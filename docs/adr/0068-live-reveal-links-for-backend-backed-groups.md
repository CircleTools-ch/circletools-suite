---
status: Accepted
date: 2026-09-19
deciders: [Adrian]
tags: [secret-santa, privacy, sharing, sync, reveal]
---

# ADR-0068 — Live reveal links for backend-backed groups (fresh wishlist & event details)

> **Amended by [ADR-0086](0086-one-way-to-be-in-a-group.md) (2026-10-22).** Reveal slices are no longer published; a reveal token names (group, event, giver) and the page is derived on read.

## Context and problem statement

A Secret-Santa [reveal link](../glossary/reveal-link.md) is a **self-contained offline snapshot**: the
giver's recipient name, the recipient's [Wishlist](../glossary/wishlist.md), the
[Budget](../glossary/budget.md) and the [Event details](../glossary/event-details.md) are all encoded
into the link's URL fragment and decoded fully client-side, with no backend
([ADR-0014](0014-reveal-link-encoding-and-privacy.md)). ADR-0014 accepts a direct consequence of this:

> **links cannot be revoked** … A link from a superseded draw keeps resolving and keeps showing the
> *old* recipient … Detecting staleness would require shared state, i.e. a backend, which this ADR
> exists to avoid.

Two things have changed since then. [ADR-0058](0058-account-free-link-shared-groups.md) added a
**backend-stored, link-shared** storage tier, and account mode already stores the whole group aggregate
on the backend ([ADR-0016](0016-sync-and-conflict-resolution.md),
[ADR-0033](0033-which-aggregates-sync.md)). For those two tiers the "shared state" ADR-0014 lacked now
**exists**.

The gap this creates in practice: a recipient updates their wishlist (or the organizer edits the event
date/location/budget) and it syncs to the backend — but a reveal link **already handed out** still shows
the *old* snapshot. The giver opens a stale wishlist and buys the wrong thing. For a **local-only**
group there is no backend, so the only recourse is to notify the organizer to re-share (implemented
separately: the "links changed since you handed them out" notice on the share screen,
[FR-045](../requirements/functional/FR-045-guided-hand-out-of-reveal-links.md)). For a **backend-backed** group we can do
better.

**Should the reveal page, for a backend-backed group, load the current wishlist and event details from
the backend when opened — while a local-only group keeps its pure offline snapshot, and every reveal
still works offline and still discloses only its own giver's assignment?**

## Decision drivers

- Recipients change wishlists *after* links are sent; givers should see the latest **without the
  organizer re-sending** — the collaborative promise of the backend tier ([ADR-0058](0058-account-free-link-shared-groups.md)).
- **Per-giver privacy is non-negotiable:** a reveal link must still disclose only *its own* giver's
  assignment, never the whole draw ([NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md),
  [ADR-0020](0020-server-side-assignment-privacy.md)).
- **Offline-first must not regress:** a reveal must still resolve with no network
  ([NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md)).
- **Local-only groups stay exactly as they are** ([ADR-0014](0014-reveal-link-encoding-and-privacy.md) unchanged there).
- Minimal new attack surface; any new token is protected at rest like the existing share tokens
  ([ADR-0061](0061-share-token-and-creator-key-at-rest-protection.md)).
- **The Platform backend stays schema-agnostic** ([ADR-0016](0016-sync-and-conflict-resolution.md),
  [ADR-0033](0033-which-aggregates-sync.md)): the shared-group `Body` is stored as **opaque JSON** the
  server never deserializes, so the server *cannot* resolve "giver G's recipient and wishlist" itself —
  whatever the reveal endpoint serves must be a document a **client** produced.

## Considered options

1. **Status quo — snapshot only.** Keep reveal links frozen; rely on the organizer re-sharing after a
   change (the local-mode "links changed" notice).
2. **Whole-group fetch by join token.** The reveal link carries the group's join token + the giver's
   member id; the reveal page fetches the entire group and resolves the assignment client-side.
3. **Server-resolved per-giver slice.** Mint a per-giver reveal token; the reveal page GETs a fresh
   slice that *the server* builds from the stored group by resolving the giver's assignment. *Rejected as
   infeasible:* the server never deserializes the opaque `Body` ([ADR-0016](0016-sync-and-conflict-resolution.md),
   [ADR-0033](0033-which-aggregates-sync.md)), so it cannot resolve a recipient/wishlist without learning
   the Secret-Santa schema — which the whole Platform boundary exists to prevent.
4. **Client-published per-giver slice + snapshot fallback (hybrid).** A draw-capable client (the
   organizer's app, which has the draw and every wishlist) computes each giver's slice — recipient name,
   current wishlist, gift note, budget, event details — and **publishes it to the server as an opaque
   document** keyed by a per-giver **reveal token**, exactly as opaque to the server as the group body
   already is. The reveal link carries that token *alongside* the offline `d=` snapshot. When online, the
   reveal page GETs the opaque slice and decodes it client-side; offline, it falls back to the embedded
   snapshot. The organizer's client re-publishes changed slices when it reconciles the group (after a
   re-draw, an event edit, or a synced wishlist change). Local-only groups are unchanged — no token, no
   published slice, snapshot only.

## Decision outcome

**Chosen: option 4 — client-published per-giver slice with offline snapshot fallback**, because it is the
only option that delivers fresher data *and* preserves per-giver privacy *and* keeps offline-first *and*
respects the schema-agnostic Platform boundary: the server stores and serves an opaque per-giver
document (never the draw, never parsed), the embedded snapshot still resolves with no network, and a
local-only group behaves exactly as under ADR-0014.

**Freshness sub-decision (deferred to the feature spec).** Because only a device that can see the draw
can build a giver's slice, a recipient editing *their own* wishlist reaches the reveal link only once a
**draw-capable device next reconciles and re-publishes** — automatic, but not instant. Instant freshness
on a recipient's own edit would need a different keying (a *recipient-wishlist* token the recipient's own
client re-publishes, referenced by the giver's reveal link); that is a larger change with its own privacy
review and is left to the feature spec to choose. This ADR fixes the *mechanism and boundary*; the exact
freshness guarantee is a follow-up.

Item #3 of the original request — *show the event details (date, budget, location) when set* — is
**already delivered** by ADR-0014's payload and the reveal page; this ADR extends it so those details
are shown **live** for a backend-backed group rather than frozen at share time.

## Consequences

- **Positive:** an already-sent link reflects the latest **published** slice for backend-backed groups,
  so a wishlist or event edit no longer requires the organizer to re-send the link itself; the reveal
  still works offline from the snapshot; local-only groups are untouched; the server never learns the
  Secret-Santa schema.
- **Negative / accepted:** a new **anonymous, token-scoped** server endpoint plus a per-giver
  reveal-**slice** store and a migration; the organizer's client must publish (and re-publish on change)
  each giver's opaque slice; a recipient's own wishlist edit is fresh only after a draw-capable device
  re-publishes (the freshness sub-decision), not instantly; the reveal page (a public static route) now
  makes a network call for backend-backed links; the reveal token is a bearer capability — like the
  join/admin tokens
  ([ADR-0058](0058-account-free-link-shared-groups.md)) it *is* the secret, so possession grants that
  one giver's slice. Mitigations: the token is scoped to a single giver (never the draw), is
  server-side revocable, and is encrypted at rest ([ADR-0061](0061-share-token-and-creator-key-at-rest-protection.md)).
  Link length grows slightly (the token is added to the fragment).
- **New risk:** a leaked reveal token exposes that giver's *current* recipient and wishlist for as long
  as the group stays backend-backed, where a snapshot would have frozen at share time — marginally more
  exposure over time, bounded by per-giver scope and revocability. When a group leaves the backend tier
  (→ on-this-device), its reveal tokens die with the server copy, exactly as the admin/join tokens do on
  `MakeLocal` ([ADR-0058](0058-account-free-link-shared-groups.md)); the link then resolves from its
  snapshot only.
- **Follow-ups:** the feature spec [FEAT-020](../features/FEAT-020-live-reveal-links-for-backend-backed-groups.md); updates to
  [FR-005](../requirements/functional/FR-005-generate-reveal-links.md) /
  [FR-006](../requirements/functional/FR-006-reveal-page-decode.md) and
  [UC-SS-005](../use-cases/secret-santa/UC-SS-005-share-reveal-links.md) /
  [UC-SS-006](../use-cases/secret-santa/UC-SS-006-open-reveal-link.md); a **reveal token** glossary
  entry; the server endpoint + slice entity + migration; the organizer-side slice publishing (on draw
  and re-publishing on reconcile); the client reveal-fetch with snapshot fallback; token revocation on
  re-draw; **resolving the freshness sub-decision** (accept eventual re-publish, or add recipient-keyed
  wishlist tokens for instant freshness); and tests across online/offline and all three storage tiers.

## Amendment (2026-10-16) — one token per slice across the organizer's devices

Found while building short links ([ADR-0082](0082-short-reveal-links-for-online-groups.md)): each device that
can publish (an admin-link holder, or every device of the owning account) kept its **own** reveal tokens and
published them as the whole set. Two organizer devices therefore replaced each other's tokens on every sync,
and a link handed out from one device stopped resolving after the other published. Long links hid it — they
fall back to their snapshot — but the live view was lost, and a short link has no snapshot to fall back to.

- Each published slice now carries a **key** — its place in the draw, `event:giver`, opaque to the server.
- On publish the server **keeps the token it already stores for a key** and ignores the newly minted one;
  the answer lists the token in effect for every key. The publishing device adopts those tokens locally, so
  every organizer device hands out the same link. New keys take the sent token.
- Older clients send no key and get the previous behaviour (their tokens replace the set). A mix of old and
  new clients on one group keeps flipping until the old ones update, which the store rollout resolves.
- Server: `reveal_slices.Key` (migration `AddRevealSliceKey`); publish answers `200` with the tokens instead
  of `204`. A client tolerates a `204` from a server that predates it.

## Pros and cons of the options

### 1. Status quo — snapshot only
- 👍 No new backend surface; nothing to build; strongest offline guarantee.
- 👎 Already-sent links stay stale; the organizer must notice and re-send every changed link — the exact
  pain this request is about.

### 2. Whole-group fetch by join token
- 👍 Reuses the existing shared-group GET; no new endpoint.
- 👎 **Breaks per-giver privacy** — a reveal-link holder could fetch the whole group and read the entire
  draw. Disqualifying against [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md).

### 3. Server-resolved per-giver slice
- 👍 Simplest client; the reveal page just GETs its slice.
- 👎 **Infeasible** — the server would have to deserialize the opaque group body and learn the
  Secret-Santa schema, breaking the schema-agnostic Platform boundary ([ADR-0016](0016-sync-and-conflict-resolution.md), [ADR-0033](0033-which-aggregates-sync.md)).

### 4. Client-published per-giver slice + snapshot fallback (chosen)
- 👍 Fresher data, per-giver privacy preserved (each token → one giver's opaque slice, never the draw),
  offline fallback, local-only unchanged, server stays schema-agnostic.
- 👎 New endpoint + slice store + migration; the reveal page gains a network path; a small link-length
  increase; a recipient's own wishlist edit is fresh only after a draw-capable device re-publishes
  (freshness sub-decision, above), not instantly.

## Links

- Related ADRs: [ADR-0014](0014-reveal-link-encoding-and-privacy.md) (extends),
  [ADR-0058](0058-account-free-link-shared-groups.md), [ADR-0020](0020-server-side-assignment-privacy.md),
  [ADR-0016](0016-sync-and-conflict-resolution.md), [ADR-0033](0033-which-aggregates-sync.md),
  [ADR-0061](0061-share-token-and-creator-key-at-rest-protection.md)
- arc42 chapters: 05 Building-block view, 08 Crosscutting concepts
- Requirements / use cases: [NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md),
  [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md),
  [FR-005](../requirements/functional/FR-005-generate-reveal-links.md),
  [FR-006](../requirements/functional/FR-006-reveal-page-decode.md),
  [UC-SS-005](../use-cases/secret-santa/UC-SS-005-share-reveal-links.md),
  [UC-SS-006](../use-cases/secret-santa/UC-SS-006-open-reveal-link.md)
- Feature spec: [FEAT-020](../features/FEAT-020-live-reveal-links-for-backend-backed-groups.md)
- Glossary: [Reveal link](../glossary/reveal-link.md)
