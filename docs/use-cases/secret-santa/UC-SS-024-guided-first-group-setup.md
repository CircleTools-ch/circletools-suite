---
id: UC-SS-024
title: Set up my first group with guidance
app: Secret Santa
actors: [Organizer]
status: Approved
priority: Should
related: [FR-001, FR-002, FR-004, FR-034, FEAT-019, ADR-0055]
---

# UC-SS-024 — Set up my first group with guidance

## Summary / goal

A first-time organizer who has just created a group wants to reach a **completed, shared draw** quickly,
without having to guess the order of steps or hunt for the right controls. The app shows a short, ordered
**getting-started checklist** on the group hub that names the next step and takes them to it, and gets out
of the way once the group is set up.

## Primary actor & stakeholders

- **Primary actor:** the **organizer** running a gift exchange for the first time.
- **Stakeholders & interests:** the family/friends who want their reveal links; the project, which wants
  first-run success (empty → drawn → shared) without support questions.

## Preconditions

- A group exists (the organizer has just created one). No account is required (local-first, NFR-001).

## Postconditions

- **Success:** the group has enough members, an event, and a completed draw; the checklist has removed
  itself, leaving the normal hub.
- **Failure:** the organizer leaves setup incomplete; the checklist remains on the hub next time (or is
  gone if they chose to skip it).

## Trigger

The organizer opens the hub of a group that is not yet fully set up.

## Main success scenario

1. On the group hub, the app shows a **"Getting started"** card listing the ordered steps: *name your
   group* (already done), *add members*, *add an event*, *draw names* — with a count of steps left.
2. The organizer taps **Add members** and is taken to the Members screen; they add the participants.
3. Back on the hub, "Add members" is ticked; they tap **Add an event** and name the event.
4. "Draw names" unlocks (an event now exists); they tap it, run the draw, and it succeeds.
5. With members, an event, and a draw all present, the checklist **disappears**; the organizer shares
   the reveal links (UC-SS-022).

## Extensions / alternate flows

- **1a. The organizer wants to explore, not be guided** → they tap **Skip setup**; the checklist hides and
  does not return for this group (a device-local choice).
- **2a. Only two members are added** → "Add members" stays open: two people cannot be drawn without giving
  to each other (the no-reciprocal rule), so the step completes only at **three** members, matching what a
  successful draw needs.
- **4a. "Draw names" is tapped before an event exists** → the step is disabled until an event is
  added, so the tap never lands on a draw with nothing to draw.

## Exceptions

- **Storage unavailable (private-mode browser):** the checklist still guides; the skip preference simply
  may not persist (it re-appears), which is harmless.

## Non-functional notes

- **Offline/local-first:** works with no account and no connection (NFR-001); reads only local group state.
- **Localization:** all checklist strings in DE/EN/FR (NFR-003).
- **Privacy:** shows only step-completion state (counts), never assignments (NFR-002).
- **Accessibility:** steps are labelled list actions with done/undone icons; the disabled "draw" step is
  conveyed, not just greyed.

## Open questions

- Whether to later replace the checklist with a fuller guided wizard (OB-8 alternative) — deferred; the
  checklist is the lightweight, non-modal first step and did not warrant a modal setup gate.

## Traceability

- Requirements: [FR-001](../../requirements/functional/FR-001-create-secret-santa-group.md),
  [FR-002](../../requirements/functional/FR-002-manage-members-and-wishlists.md),
  [FR-004](../../requirements/functional/FR-004-run-fair-draw.md),
  [FR-034](../../requirements/functional/FR-034-multiple-events-per-group.md)
- Feature spec: [FEAT-019](../../features/FEAT-019-getting-started-checklist.md)
- Related: [ADR-0055](../../adr/0055-event-navigation-mobile-section-hub.md) (the events-first hub this
  guides within), [UC-SS-022](UC-SS-022-hand-out-reveal-links.md) (the share step that follows)
