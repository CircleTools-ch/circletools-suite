---
id: FR-045
title: Guided hand-out of reveal links
type: functional
status: Implemented
priority: Could
source: UC-SS-022
verification: test
related: [UC-SS-022, FEAT-015, TP-015, ADR-0028, NFR-001, NFR-002, NFR-003]
---

# FR-045 — Guided hand-out of reveal links

## Description

On the **Share** screen of a **drawn** event ([UC-SS-005](../../use-cases/secret-santa/UC-SS-005-share-reveal-links.md)),
the app offers a **Hand out links** action that guides the Organizer through giving each participant their
**own** [reveal link](../../glossary/reveal-link.md), one person at a time, and **tracks who has already
been handed their link**.

- The guided pass lists everyone in the current draw and, for the person in focus, presents the same
  per-person delivery the Share screen already offers — **Share** through the native share sheet where the
  platform has one ([ADR-0028](../../adr/0028-browser-platform-adapters-block.md)), otherwise **Copy**.
- After delivering a person's link the Organizer marks them **handed out**; the pass shows a running
  **"X of Y done · Z left"** and starts at the first person still outstanding.
- The handed-out state is **persisted per event, device-local, and not synced**, so the Organizer can
  close the app and return later and still see exactly who is left. It is reversible per person, and is
  **reset when the event is re-drawn** (the links change, so old marks no longer refer to valid links).

The action **must never** compose a message, share payload, or copied text containing **more than one**
reveal link: each hand-out carries exactly one recipient's link
([NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md)). The whole flow runs on the
device with no account or connection
([NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md)).

## Rationale

The per-person Share / Copy / QR actions already exist, but handing out a large roster by hand is where a
participant gets **skipped** — there is no record of who has been sent their link. A guided pass with a
persisted "handed out" tally turns "did I already send Grandma hers?" into a visible **Z left**, which is
the whole point of the feature (MNF-7, from the
[mobile brainstorm](../../brainstorming/2026-09-01-secret-santa-mobile.md)). Keeping it strictly
one-link-per-recipient is what lets it be a convenience without becoming a spoiler leak — the naive
"share them all at once" is explicitly forbidden by the acceptance criteria below.

## Acceptance criteria

```gherkin
Given a drawn event with several participants
When I open the Share screen
Then a "Hand out links" action is offered

Given the guided hand-out is open and no one has been handed out yet
Then it shows "0 of N done · N left" and focuses the first participant

Given the platform has a native share sheet
When I share the focused person's link
Then the share payload contains that one person's reveal link and no other

Given I mark the focused person as handed out
Then the count updates and the pass advances to the next person still outstanding

Given I have marked some people handed out
When I close and reopen the app and return to the hand-out
Then the same people are still marked handed out and only the outstanding people remain

Given a person is marked handed out
When I toggle that person back
Then they are outstanding again and re-appear in the count

Given some participants have been handed out
When the event is re-drawn
Then the hand-out progress for that event is reset to none handed out

Given the platform has no share sheet (e.g. a desktop browser)
When I run the hand-out
Then each person is delivered by Copy instead, and progress is tracked the same way

Given the guided hand-out at any point
When any hand-out payload is produced
Then it never contains more than one reveal link
```

## Dependencies & related

- Derived from: [UC-SS-022](../../use-cases/secret-santa/UC-SS-022-hand-out-reveal-links.md)
- Related: [NFR-002](../non-functional/NFR-002-assignment-privacy.md) (one link per recipient — the
  central constraint), [NFR-001](../non-functional/NFR-001-local-first-offline.md) (offline, on-device
  progress), [NFR-003](../non-functional/NFR-003-multilingual-de-en-fr.md) (DE/EN/FR, with a singular
  count form)
- Governing ADR: [ADR-0028](../../adr/0028-browser-platform-adapters-block.md) — reuses the
  `IShareService` share seam (no new ADR)
- Implemented by: [FEAT-015](../../features/FEAT-015-guided-hand-out-of-reveal-links.md)
- Builds on: [FR-005](FR-005-generate-reveal-links.md) (the reveal links being handed out)
- Glossary: [Reveal link](../../glossary/reveal-link.md), [Assignment](../../glossary/assignment.md),
  [Draw](../../glossary/draw.md)
