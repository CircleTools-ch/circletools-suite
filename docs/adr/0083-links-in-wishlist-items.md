---
status: Accepted
date: 2026-10-16
deciders: [Adrian]
tags: [secret-santa, wishlist, compatibility]
---

# ADR-0083 — Links in wishlist items are recognised, not entered in a special way

## Context and problem statement

A wishlist is a list of text items (`IReadOnlyList<string>`) — in the local store, the synced group body, the
shared-group copy, the per-giver reveal slice and the encoded reveal-link snapshot
([ADR-0014](0014-reveal-link-encoding-and-privacy.md), [ADR-0068](0068-live-reveal-links-for-backend-backed-groups.md)).
The [2026-10-16 two-account test](../brainstorming/2026-10-16-secret-santa-two-account-device-test.md) (I8)
asked for links the giver can tap (and, at first, an optional price with an "over budget" hint). Older app
versions, links already sent and server copies must keep working. How should a wish carry a link?

## Decision drivers

- **Nothing to learn:** people paste a shop link into a wish the way they would into a chat message.
- **Backward compatible everywhere:** no stored or transmitted format change; the server never reads the group
  body ([ADR-0016](0016-sync-and-conflict-resolution.md)).
- **Reveal-link size:** the snapshot is length-budgeted ([FR-005](../requirements/functional/FR-005-generate-reveal-links.md)).

## Considered options

1. **Structured items** (`{ text, link, price }`) in every format, with a version bump of the reveal payload.
2. **A typed convention** inside the line: `Headphones · CHF 80 · https://…` (built first, then dropped).
3. **Recognise links wherever they are** in the line; no price.

## Decision outcome

**Chosen: option 3 — recognise links wherever they are**, because it asks nothing of the person writing the
wish and changes no format.

- The first `http(s)://` address anywhere in a wish is its link (`WishlistItem.Parse`, pure, in Core). The
  rest of the line is its text.
- The giver (reveal page, *My assignments*) sees the text and a tappable **"Open link (shop.example)"** instead
  of the long address. A wish that is only a link shows just that.
- The editors stay plain text, one wish per line, with no hint about formats.
- Only `http` and `https` become links; they open outside the app with `rel="noopener noreferrer"`.

Option 2 was implemented and tried on 2026-10-16 and dropped on Adrian's review: the "·" separator was
confusing and too complicated, and nobody was expected to type prices. Prices are not recognised.

## Consequences

- **Positive:** no migration, no payload version, no server change; works in every head at once; existing
  wishes that already contain links display better immediately.
- **Negative:** no price or "over budget" hint.
- **New risks:** a malicious link in a wishlist — mitigated by allowing only http(s), opening it outside the
  app, and showing the site name before it is opened.

## Pros and cons of the options

### 1. Structured items
- 👍 Exact fields.
- 👎 Changes every stored and transmitted format; older clients and already-sent links need a compatibility
  layer; more to fill in.

### 2. Typed convention
- 👍 No format change.
- 👎 A separator people have to learn; confusing in practice.

### 3. Recognise links (chosen)
- 👍 Nothing to learn, nothing to migrate.
- 👎 Only links; prose stays prose.

## Links

- Related ADRs: [ADR-0014](0014-reveal-link-encoding-and-privacy.md), [ADR-0016](0016-sync-and-conflict-resolution.md),
  [ADR-0042](0042-events-as-first-class-entities.md), [ADR-0068](0068-live-reveal-links-for-backend-backed-groups.md)
- Requirements: [FR-002](../requirements/functional/FR-002-manage-members-and-wishlists.md),
  [FR-030](../requirements/functional/FR-030-edit-my-wishlist.md)
