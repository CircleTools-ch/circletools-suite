---
status: Accepted
date: 2026-10-16
deciders: [Adrian]
tags: [secret-santa, reveal, sharing, privacy]
---

# ADR-0082 — Short reveal links for online groups

## Context and problem statement

A reveal link carries the whole assignment as an encoded snapshot in its fragment (`#for=…&v=…&d=…`,
[ADR-0014](0014-reveal-link-encoding-and-privacy.md)), so it works offline and needs no server. For a group
that lives online — shared by link, or an account-owned shared group — the link additionally carries a
per-giver reveal token (`&rt=…`) that fetches the live slice ([ADR-0068](0068-live-reveal-links-for-backend-backed-groups.md)).
The [2026-10-16 two-account test](../brainstorming/2026-10-16-secret-santa-two-account-device-test.md) (Q32b)
found these links long and alarming in a chat: several hundred characters of opaque text, a preview that
says nothing, and some messengers truncating or wrapping them. Can an online group's links be short without
weakening privacy or the reveal itself?

## Decision drivers

- **Per-giver privacy** ([NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md)): a link still
  discloses only its own giver's assignment; no link reveals anything before it is opened.
- **Nothing secret in server logs or link previews:** the token must not reach the static host's access logs
  or a messenger's preview crawler.
- **Local-only groups unchanged:** with no server copy, their links must keep the embedded snapshot.
- **No dead links:** a short link must only be handed out once its slice is actually on the server.
- **Small change:** reuse the reveal token and slice store of ADR-0068; no new server endpoint.

## Considered options

1. **Keep the long links** and only explain them better in the share message.
2. **Path-based short links** (`/r/<code>`): a short code in the URL path, resolved by the server.
3. **Token-only fragment links** (`/r#for=<name>&rt=<token>`): drop the `d=` snapshot for groups whose live
   slice is published; the reveal page fetches the slice by token.

## Decision outcome

**Chosen: option 3 — token-only fragment links**, because the reveal token already names exactly one
giver's slice, the fragment keeps it out of every request line and preview crawler, and the reveal page
already knows how to fetch it.

- **When:** the share screen hands out the short form only for a group that publishes reveal slices (shared
  by link with the management link, or an account-owned shared group) **and** whose slices were just
  published successfully. Otherwise — offline, a publish failure, a local-only or plain *My account* group —
  it hands out the long link as before (with `rt=` where a token exists).
- **Format:** `https://<app>/r#for=<name>&rt=<token>` — about 90 characters. `for=` is the giver's own name
  (the person the link is sent to), not a secret.
- **Opening:** with no `d=`, the reveal page fetches the slice by token. The slice it got is kept on that
  device, so reopening the link later works offline too. If the slice cannot be fetched and none is kept,
  the page says the link needs a connection once (not "invalid link"); if the server answers that it no
  longer knows the token (the online copy expired or was deleted), it says the link no longer works and to
  ask the organizer for a new one.
- **Opening a second link while one is shown** (the app's deep-link handler): the navigation carries a
  changing `?open=` query, because a change of the `#fragment` alone is an in-page scroll in Blazor and would
  not re-render the reveal page. This also fixes the same case for long links.
- **Preview:** a messenger fetching `/r` gets the generic app page — title and description of the app, never
  a name or assignment.
- **Stability:** reveal tokens are stable per (event, giver) across re-draws (ADR-0068), so a short link
  already sent shows the new pairing after a re-draw, as live links already do.

## Consequences

- **Positive:** links fit in a chat line and read as a link; the share message (Q32a) carries the context.
  Long links remain for every case that needs them, so nothing that worked stops working.
- **Negative:** a short link needs the server the first time it is opened on a device; if the group's online
  copy expires or is deleted, a short link that was never opened cannot be revealed (a long one still can).
- **Follow-ups:** the reveal page caches fetched slices per token on the device (best-effort).
- **New risks:** a leaked short link reveals one assignment — exactly as a leaked long link does today.

## Pros and cons of the options

### 1. Keep the long links
- 👍 No change; always works offline.
- 👎 The finding stands: long, opaque, sometimes truncated.

### 2. Path-based short links
- 👍 Shortest possible URL; the server could render a per-link preview.
- 👎 The code reaches the static host in the request line (logs) and every preview crawler; needs a new
  resolver endpoint and changes to the App Link handling.

### 3. Token-only fragment links (chosen)
- 👍 Short; the token never leaves the device in a request line; no new endpoint.
- 👎 Needs the network on first open; only for groups with published slices.

## Links

- Related ADRs: [ADR-0014](0014-reveal-link-encoding-and-privacy.md), [ADR-0068](0068-live-reveal-links-for-backend-backed-groups.md),
  [ADR-0058](0058-account-free-link-shared-groups.md), [ADR-0081](0081-signed-in-sharing-stays-in-the-account.md)
- Requirements / use cases: [FR-005](../requirements/functional/FR-005-generate-reveal-links.md),
  [FR-006](../requirements/functional/FR-006-reveal-page-decode.md), [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md)
