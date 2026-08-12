---
id: UC-SS-009
title: Create a group from a circle
app: Secret-Santa
actors: [Organizer]
status: Draft
priority: Must
related: [FR-027, FR-019, FR-022, NFR-001, NFR-006, ADR-0018, ADR-0019, ADR-0033]
---

# UC-SS-009 — Create a group from a circle

## Summary / goal

A signed-in organizer creates a Secret-Santa [Group](../../glossary/group.md) **from a
[Circle](../../glossary/circle.md)** they belong to, so the people are brought in from the standing
set instead of being retyped. Each circle person with an account becomes a **linked**
[Member](../../glossary/member.md) from the start; managed members and any others are brought in as
ordinary named members. From that point the group behaves exactly like one created by hand.

## Primary actor & stakeholders

- **Primary actor:** an organizer who is a member of at least one circle.
- **Stakeholders & interests:** the circle's people, who become members of a group (and, if they have
  an account, are linked to it — [UC-SS-011](UC-SS-011-see-my-assignment.md)); the circle's owner and
  admins, whose [managed members](../../glossary/managed-member.md) are copied out and must keep
  working even if the circle later changes; the organizer, who wants the setup shortcut without
  surrendering control of the group.

## Preconditions

- The actor is **signed in** (a circle is an account concept — there is no account-free path here).
- The actor belongs to at least one circle that has people in it
  ([UC-MAIN-008](../main/UC-MAIN-008-create-circle.md)..[UC-MAIN-012](../main/UC-MAIN-012-manage-circle-people.md)).

## Postconditions

- **Success:** a new group exists, account-scoped and syncing
  ([UC-MAIN-015](../main/UC-MAIN-015-sync-across-devices.md)), populated with one member per chosen
  circle person — linked where that person has an account, unlinked otherwise. The group is a
  **snapshot**: it holds its own copy of the people and no longer depends on the circle.
- **Failure:** no group is created; the circle is unchanged.

## Trigger

The organizer chooses "create a group from a circle" and picks one of their circles.

## Main success scenario

1. Organizer chooses to create a group and selects **from a circle**; the system lists the circles
   they belong to.
2. Organizer picks a circle. The system shows its people — accounted members and managed members
   alike — with everyone selected by default (a circle is already "these are my people").
3. Organizer confirms the selection and names the group.
4. System creates the group and, **for each chosen person, copies them in as a member**:
   - a circle person **with an account** becomes a **linked** member (an optional user reference is
     set — [ADR-0019](../../adr/0019-member-user-linking.md));
   - a **managed member** and anyone else becomes an **unlinked** named member, exactly like a member
     typed in by hand — a managed member is never linked ([ADR-0018](../../adr/0018-circle-and-sharing-model.md)).
   Each copied member records the **circle person it came from**, so a later change to the circle can
   be reasoned about without re-reading the circle.
5. The group opens in the ordinary editor. From here it is an ordinary group: the organizer sets the
   [budget](../../glossary/budget.md) and [event details](../../glossary/event-details.md), adjusts
   members, defines [exclusions](../../glossary/exclusion.md)
   ([UC-SS-003](UC-SS-003-define-exclusions.md)), and runs the
   [draw](../../glossary/draw.md) ([UC-SS-004](UC-SS-004-run-draw.md)).

## Extensions / alternate flows

- **1a. The actor belongs to no circle** → the "from a circle" path is absent (or disabled with a
  one-line reason); creating a group by hand ([UC-SS-001](UC-SS-001-create-group.md)) is unaffected.
- **2a. The organizer deselects some people** → only the chosen people are copied. This is a normal
  case — not everyone in a family plays every year.
- **2b. The organizer adds someone the circle doesn't have** → they add ordinary named members after
  step 5, mixed freely with the copied ones ([UC-SS-002](UC-SS-002-manage-members.md)).
- **4a. Two circle people resolve to the same account** (should not happen inside one circle, but a
  circle can contain a managed member later promoted to an existing account) → the group refuses to
  create **two members linked to the same user**; the second is brought in unlinked, and the organizer
  is told ([ADR-0019](../../adr/0019-member-user-linking.md)).
- **After the draw — the circle changes** → nothing happens to the group. The group froze its
  participant list at the draw (D2 — snapshot at draw time); a person added to or removed from the
  circle in January does not rewrite a December draw. This is the existing draw-invalidation contract:
  editing *the group's* members invalidates its draw, but editing *the circle* does not touch the
  group at all.
- **Before the draw — the circle changes** → still nothing automatic. The snapshot is taken at
  **create** time, not kept live; if the organizer wants a newly added circle person, they add them to
  the group by hand. (A live link was rejected in D2 because it breaks existing draws.)

## Exceptions

- **The circle is deleted after the group was created** → the group **opens unchanged**. Its members
  are its own copies; the recorded circle-person origin becomes a dangling reference that is read
  defensively and never dereferenced into the (gone) circle — it must not raise an error
  ([FR-022](../../requirements/functional/FR-022-delete-circle.md)'s carried Secret-Santa half).
- **A managed member is removed from the circle after the group was created** → the copied member in
  the group is **unaffected** — it is a snapshot, not a live view
  ([FR-019](../../requirements/functional/FR-019-manage-managed-members.md)'s carried half).
- **The create call cannot reach the server while signed in** → the group is created locally and
  syncs when back online ([UC-MAIN-016](../main/UC-MAIN-016-work-offline-and-reconcile.md)); it is
  account-scoped from birth, so nothing is lost.

## Non-functional notes

- **Offline/local-first:** creating a group from a circle needs the circle, which is
  **server-authoritative and does not sync** ([ADR-0033](../../adr/0033-which-aggregates-sync.md)) —
  so this path needs the circle to be readable, i.e. connectivity or a warm read. The **group** it
  produces is local-first from that point on, like every other group.
- **Localization:** DE/EN/FR, including the counted people strings — *"6 people in this circle"* has a
  singular in all three languages
  ([localization guidelines](../../guidelines/localization-guidelines.md)).
- **Privacy:** copying accounted people into a group establishes **links**, not new disclosures — the
  group members are names and (once drawn) assignments, governed by
  [NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md). Reading the circle to
  populate the picker is authorized per request against current membership
  ([NFR-006](../../requirements/non-functional/NFR-006-circle-authorization.md)); the organizer only
  sees people from a circle they belong to.
- **Performance:** a family-scale circle is a handful of people; the picker and the copy are instant.

## Open questions

- **May any circle member create a group from the circle, or only owners/admins?** Recommendation:
  **any member** — the group belongs to the organizer, not to the circle, and creating one from the
  circle only *reads* it. This needs no new circle role; it is an ordinary authorized read
  ([NFR-006](../../requirements/non-functional/NFR-006-circle-authorization.md)). To be confirmed in
  the M4 feature spec.
- **Does a linked member find out they were added to a group?** In M4 there is no notification channel
  (as in M2); they discover it when they next open the app and see the group/assignment. A notification
  is parked, consistent with the brainstorming decision to defer notifications.

## Traceability

- Requirement: [FR-027](../../requirements/functional/FR-027-create-group-from-circle.md); it also
  closes the carried Secret-Santa halves of
  [FR-019](../../requirements/functional/FR-019-manage-managed-members.md) and
  [FR-022](../../requirements/functional/FR-022-delete-circle.md)
- ADRs: [ADR-0018](../../adr/0018-circle-and-sharing-model.md) (snapshot at draw time, managed
  members), [ADR-0019](../../adr/0019-member-user-linking.md) (linking, duplicate-link guard),
  [ADR-0033](../../adr/0033-which-aggregates-sync.md) (circle does not sync)
- Related use cases: [UC-SS-010](UC-SS-010-link-member-to-account.md),
  [UC-SS-011](UC-SS-011-see-my-assignment.md), [UC-SS-001](UC-SS-001-create-group.md),
  [UC-MAIN-011](../main/UC-MAIN-011-manage-managed-members.md)
- Glossary: [Group](../../glossary/group.md), [Circle](../../glossary/circle.md),
  [Member](../../glossary/member.md), [Managed member](../../glossary/managed-member.md)
- Origin: [brainstorming — accounts, families & sync (v2)](../../brainstorming/2026-07-24-accounts-families-sync.md),
  decisions D2, D3
