---
id: TP-007
title: Test plan — Events (occasions) per group
context: SecretSanta
status: Reviewed
related: [FEAT-007, FR-034, UC-SS-013, ADR-0042]
---

# TP-007 — Test plan — Events (occasions) per group

Covers [FEAT-007](FEAT-007-events-per-group.md) /
[FR-034](../requirements/functional/FR-034-multiple-events-per-group.md). The load-bearing properties
are **event independence** (re-drawing one occasion must not disturb another), **shared-roster
invalidation** (a roster change voids every event's draw), and **in-place migration** (a pre-events
group loses nothing).

## Automated coverage

| Area | Where | What it asserts |
|------|-------|-----------------|
| Aggregate: default event | `GroupTests` | a new group has one event whose id == group id |
| Aggregate: event independence | `GroupTests.EventsAreIndependent_ReDrawingOneLeavesAnotherAlone` | re-drawing one event leaves another's draw untouched |
| Aggregate: shared-roster invalidation | `GroupTests.AddMember_VoidsEveryEventsDraw` | adding a member voids **every** event's draw |
| Aggregate: per-event wishlists | `GroupTests.Wishlists_ArePerEvent` | a member's wishlist differs per event |
| Aggregate: last event guard | `GroupTests.RemoveEvent_RejectsRemovingTheLastEvent` | the last event cannot be removed |
| Memento round-trip | `GroupTests.ToState_FromState_RoundTrips` | budget, details, draw, wishlists survive per event |
| Application handlers | `ApplicationHandlerTests` | event CRUD, event-scoped budget/details/wishlist/draw |
| Reveal per event | `HandlerTests` | reveal links built from one event's draw + that event's wishlists/budget |
| Sync (two devices) | `TwoClientSyncTests` | a claimed group's members, per-event wishlists, exclusions and draw all cross |
| Sync (offline) | `SyncOfflineTests` | create + populate + draw an event with no network |
| Linking per event | `AssignmentReadTests`, `LinkingWishlistAndCascadeTests` | a linked member's assignment and self-wishlist are per (group, event) |
| Event registry | `LinkingWishlistAndCascadeTests`, `LinkingCircleTests`, `MemberLinkTests` | a member enumerates a group's events (My-memberships) once the organizer publishes the registry |
| Linking migration | applied by every `Main.Service.Test` integration test | the `PerEventLinking` migration applies cleanly and the linking store works keyed by event |

All of the above are green (Core 64, LocalStore 57, UI 16, Main-service 232, Architecture 28).

## Manual / browser verification (required by `CLAUDE.md`)

Run the stack (`dotnet run --project src/Aspire/CircleTools.AppHost`, open `secretsanta-web`), in
**DE/EN/FR**, and walk:

1. Create a group → add members/exclusions **once** → create two events (name them).
2. Give each event its **own** wishlists, budget and date → draw each independently → confirm
   re-drawing one event does **not** change the other → open a **per-event** reveal link.
3. Add/remove a member and confirm **every** event's draw is voided (the warning shows).
4. Signed in with a linked member on a second device: confirm My-wishlists offers **one editor per
   (group, event)** and My-assignments shows **one card per drawn event**.
5. **Migration:** open a group created **before** this change and confirm its old draw, budget, event
   details and wishlists are intact inside a single default event.
6. Console clean; the unnamed default event shows a localized generic label in each language.
