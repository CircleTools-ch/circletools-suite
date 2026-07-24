# Backlog

Idea stubs and work items, newest-first. Promote items into
[brainstorming](../brainstorming/index.md) → [use cases](../use-cases/index.md) as they mature.

## Ideas

### IDEA-001 — Secret-Santa app (first app) · _status: MVP defined_
- **Problem:** Organizing a Secret-Santa (Wichteln) by hand is fiddly — drawing names fairly,
  honoring "who can't draw whom" (e.g. partners), and doing it across a group on different phones.
- **Target users:** families and friend-groups, Swiss-first.
- **Value:** a delightful, private, offline-capable way to run a gift exchange with no account or backend.
- **MVP scope (defined):** create & configure a group (name, budget, event details) → add members
  with wishlists → define exclusions → run a fair draw (honors exclusions, no reciprocal pairs, no
  repeat of the previous draw via draw history, graceful failure) → generate one **self-contained
  encoded reveal link** per member (recipient + wishlist + budget + event) → each member opens their
  link and the page decodes it fully client-side. Fully offline & account-free; privacy (the link is
  the secret); multilingual DE/EN/FR (`de-CH` default). Out of scope: accounts, sync, server-stored
  assignments, notifications, multiple simultaneous organizers.
- **Traceability:** [brainstorming](../brainstorming/2026-07-24-secret-santa-scope.md) →
  [use cases UC-SS-001..006](../use-cases/index.md) → [requirements FR-001..006, NFR-001..003](../requirements/index.md).
- **Post-MVP (v2 — after the offline MVP):** add **central login** in the AppSuite **Main** shell,
  **App↔backend sync**, and the ability to **link a Secret-Santa `Member` to another user's AppSuite
  account** — so members receive their assignment on their own device, keep their own wishlist, and
  identity is shared across apps. This drives the deferred sync / account-claim / groups-sharing
  decisions (see the [ADR backlog](../adr/index.md)).
- **Next:** architecture ADRs (incl. reveal-link encoding & privacy/tamper) + feature specs.

### IDEA-000 — AppSuite shell (Main app-type) · _status: planned_
- **Problem:** apps need shared branding, one login, shared config, and a dashboard.
- **Role:** hosts **central login / accounts**, shared config, and the **sync backbone** that later
  connects the Secret-Santa app (and future apps) to the backend — including linking in-app "persons"
  (e.g. Secret-Santa `Member`s) to AppSuite user accounts.
- **Next:** design when the post-MVP accounts+sync phase begins (after the Secret-Santa offline MVP).

## Later / parked
- Additional apps (to be brainstormed).
- Public sharing / invitations model.
