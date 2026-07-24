---
date: 2026-07-24
facilitator: Adrian
participants: [Adrian]
topic: Secret-Santa MVP scope
status: Concluded
---

# Brainstorming — Secret-Santa MVP scope (2026-07-24)

## Objective / framing question

What is the smallest Secret-Santa app that is genuinely useful and delightful **offline, with no
account and no backend** — and that we can build a suite around?

## Divergent — raw ideas

Capture everything, no judging.

- Create a group; add members by name.
- Draw names fairly; each gives once, receives once.
- Exclusions (partners / same household can't draw each other).
- No reciprocal pairs (if A gives to B, B does not give to A).
- Don't repeat last year's draw for the same group.
- Each giver privately sees only their own recipient.
- Wishlists / gift ideas per member.
- Budget per group (spending limit everyone agrees on).
- Event details: date, location, notes.
- Re-draw until "shared/locked".
- **Pass-the-phone reveal:** hand the device around; each member taps to see their recipient.
- **Backend-stored reveal links:** server keeps the assignments; each member opens a link that
  fetches theirs.
- **Self-contained encoded links:** the whole payload (recipient + wishlist + budget + event) is
  encoded *into the link itself*; a page decodes it client-side. No server, no account.
- QR codes / messaging to hand out the reveal.
- Reminders before the event.
- Multiple groups (family + friends).
- Accounts + cross-device sync.

## Clustering / themes

- **Core draw:** group, members, exclusions, draw, private assignment.
- **Draw fairness rules:** honor exclusions, no reciprocal pairs, no repeat of the previous draw
  (needs a draw history).
- **Group context for the gift:** wishlists, budget, event details.
- **Reveal mechanism (the pivotal choice):** pass-the-phone vs backend-stored links vs
  self-contained encoded links.
- **Distribution:** copy / QR / messaging, out-of-band.
- **Needs backend / account:** stored links, cross-device sync, notifications.

## Convergent — selection

Score candidates. Impact/effort/fit on a 1–5 scale (5 = best).

| Idea | Impact | Effort (low=better) | Fit | Decision |
|------|:------:|:-------------------:|:---:|----------|
| Group + members | 5 | 5 | 5 | **Selected (MVP)** |
| Wishlists per member | 4 | 4 | 5 | **Selected (MVP)** |
| Budget per group | 4 | 5 | 5 | **Selected (MVP)** |
| Event details | 4 | 5 | 5 | **Selected (MVP)** |
| Exclusions | 5 | 3 | 5 | **Selected (MVP)** |
| Fair draw + no reciprocal + no repeat (history) | 5 | 3 | 5 | **Selected (MVP)** |
| Private assignment per giver | 5 | 4 | 5 | **Selected (MVP)** |
| Reveal: **self-contained encoded links** | 5 | 4 | 5 | **Selected (MVP)** |
| Reveal: pass-the-phone | 3 | 5 | 2 | Rejected (organizer can't share remotely; awkward) |
| Reveal: backend-stored links | 4 | 2 | 2 | Rejected for MVP (needs backend/account) |
| Accounts + cross-device sync | 4 | 1 | 3 | Parked (post-MVP) |
| Notifications / reminders | 3 | 2 | 3 | Parked (needs backend) |
| Multiple simultaneous organizers | 2 | 1 | 2 | Parked (post-MVP) |

## Decisions & selected ideas

- **MVP = fully offline, account-free core:** create a group → add members (with wishlists) → set
  a budget and event details → define exclusions → run a fair draw → hand each giver a private
  reveal link. No account, no backend.
- **Reveal mechanism = self-contained encoded links.** After the draw, the organizer generates
  **one reveal link per member**. The link **encodes the full payload** for that giver — the
  recipient's name, the recipient's wishlist, the group budget, and the event details — into the
  link itself (a URL-safe encoded fragment). A reveal page **decodes the link entirely
  client-side** and displays the assignment; nothing is stored on a server. The organizer
  distributes links out-of-band (copy / QR / messaging).
  - *Chosen over pass-the-phone* because the organizer can distribute remotely and each member
    keeps their own link.
  - *Chosen over backend-stored links* because it keeps the MVP fully offline and account-free —
    no server ever holds the assignments. The link **is** the secret.
- **Draw fairness rules the algorithm must enforce:** (a) honor exclusions (one-directional
  "giver X must not give to recipient Y"); (b) no reciprocal pairs (if A→B then not B→A); (c) no
  repeat of the previous draw of the same group (requires keeping a draw history). The draw must
  **fail gracefully** when no valid assignment exists (explain why, e.g. over-constrained).
- **Non-functional anchors:** local-first & fully offline; assignment privacy (the link is the
  secret; no server-side assignment storage; members never see others' assignments); multilingual
  DE/EN/FR with `de-CH` default.

## Rejected / parked (for the record)

- **Pass-the-phone reveal** — rejected: doesn't support remote distribution and is awkward in
  practice.
- **Backend-stored reveal links** — rejected for MVP: would require a backend/account and would
  place assignments on a server, breaking the privacy and offline goals.
- **Accounts & cross-device sync** — parked (post-MVP); not needed for the self-contained-link
  reveal.
- **Notifications / reminders** — parked; needs a backend.
- **Multiple simultaneous organizers** — parked (post-MVP).

## Follow-up actions

- [x] Refine group creation (name, budget, event details) → [UC-SS-001](../use-cases/secret-santa/UC-SS-001-create-group.md), [FR-001](../requirements/functional/FR-001-create-secret-santa-group.md)
- [x] Members & wishlists → [UC-SS-002](../use-cases/secret-santa/UC-SS-002-manage-members.md), [FR-002](../requirements/functional/FR-002-manage-members-and-wishlists.md)
- [x] Exclusions → [UC-SS-003](../use-cases/secret-santa/UC-SS-003-define-exclusions.md), [FR-003](../requirements/functional/FR-003-define-exclusions.md)
- [x] Fair draw with rules + history + graceful failure → [UC-SS-004](../use-cases/secret-santa/UC-SS-004-run-draw.md), [FR-004](../requirements/functional/FR-004-run-fair-draw.md)
- [x] Generate self-contained reveal links → [UC-SS-005](../use-cases/secret-santa/UC-SS-005-share-reveal-links.md), [FR-005](../requirements/functional/FR-005-generate-reveal-links.md)
- [x] Open a reveal link (decode client-side) → [UC-SS-006](../use-cases/secret-santa/UC-SS-006-open-reveal-link.md), [FR-006](../requirements/functional/FR-006-reveal-page-decode.md)
- [x] Non-functional: local-first & offline, privacy, multilingual → [NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md), [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md), [NFR-003](../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)
- [ ] Record reveal-link encoding & privacy/tamper decision as an ADR at implementation time — see [ADR backlog](../adr/index.md)
