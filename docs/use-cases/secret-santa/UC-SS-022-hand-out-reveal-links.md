---
id: UC-SS-022
title: Hand out reveal links without forgetting anyone
app: Secret-Santa
actors: [Organizer]
status: Approved
priority: Could
related: [FR-045, FEAT-015, TP-015, ADR-0028, NFR-002, UC-SS-005, UC-SS-006, UC-SS-018]
---

# UC-SS-022 — Hand out reveal links without forgetting anyone

## Summary / goal

After a draw, the Organizer has to get **each** participant their **own**
[reveal link](../../glossary/reveal-link.md) — one person at a time, through whatever chat that person
uses. Doing this by hand (tap Share, pick a chat, tap back, find the next person, repeat) is where a
participant gets **skipped**: with a large roster there is no record of who has already been sent their
link. The Organizer wants a guided pass over the whole draw that fires the share sheet for one person at
a time and **remembers who has been handed their link**, so nobody is forgotten and the pass can be
resumed later.

## Primary actor & stakeholders

- **Primary actor:** the Organizer, on the [Share](UC-SS-005-share-reveal-links.md) screen of a drawn
  event.
- **Stakeholders & interests:**
  - The Organizer — wants to hand every participant their link with the fewest taps, and to *see* who is
    still outstanding so no one is missed.
  - Each participant — wants to receive **only their own** link, never a message that also carries
    someone else's (which would spoil the surprise). Assignment privacy is the constraint the flow is
    built around ([NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md)).

## Preconditions

- The event has been **drawn**, so reveal links exist ([UC-SS-005](UC-SS-005-share-reveal-links.md)).
- The device can present a native share sheet — the phone, or a mobile browser with the Web Share API
  ([ADR-0028](../../adr/0028-browser-platform-adapters-block.md)). Where it cannot (most desktop
  browsers), the pass still runs but hands the link over by **Copy** instead (see *3a*).
- No account, sign-in, or connection is required — the links and the progress are on the device
  ([NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md)).

## Postconditions

- **Success:** every participant in the draw has been marked **handed out**; the Organizer can see
  "all done" and, on returning later, that nothing is outstanding.
- **Failure / partial:** some participants remain **outstanding**; their handed-out state is unchanged,
  so the next pass shows exactly who is left.

## Trigger

The Organizer taps **Hand out links** on the Share screen of a drawn event.

## Main success scenario

1. The Organizer opens the Share screen of a drawn event and taps **Hand out links**.
2. The app shows the guided hand-out over everyone in the draw, starting at the first person **not yet
   handed out**, with a running count — **"X of Y done · Z left"**.
3. For the current person, the Organizer taps **Share** — the native share sheet opens carrying **that
   one person's** reveal link, and the Organizer picks their chat / app.
4. Back in the app, the Organizer marks the person **✓ handed out**; the flow advances to the next
   person still outstanding and the count updates.
5. Steps 3–4 repeat until no one is left; the screen reads **all handed out**.
6. If the Organizer closes the app and returns later, the handed-out marks are **still there**, so the
   pass resumes at whoever is still outstanding.

## Extensions / alternate flows

- **3a. The platform has no share sheet** (typical desktop browser) → for each person the action is
  **Copy** instead of Share; the Organizer pastes the link into the chosen channel and marks the person
  handed out. The guided pass and its progress work identically.
- **4a. The Organizer wants to skip someone for now** → they advance without marking; that person stays
  **outstanding** and reappears next time. Nothing is ever sent on the person's behalf without the
  Organizer choosing a target in the share sheet.
- **4b. The Organizer marks someone by mistake** → they can toggle a person back to **outstanding** (the
  handed-out state is a reversible per-person flag, not an irreversible action).
- **6a. The event is re-drawn** → the reveal links change, so the previous "handed out" marks no longer
  refer to valid links; the hand-out progress for that event is **reset** so the Organizer hands out the
  new links afresh ([FR-045](../../requirements/functional/FR-045-guided-hand-out-of-reveal-links.md)).
- **6b. The roster changed but the draw was kept** (a member archived/added, [ADR-0052](../../adr/0052-member-archival-and-permanent-draw-history.md))
  → the same "roster changed" warning the Share screen already shows applies; the hand-out lists exactly
  the links the kept draw produced.

## Exceptions

- **The share sheet is dismissed without sending** → a normal outcome; the person simply is not marked
  handed out (unless the Organizer marks them anyway), and stays outstanding.

## Non-functional notes

- **Privacy (the central constraint):** the flow hands over **one link to one recipient at a time**. It
  **never** composes a single message containing more than one reveal link — that would let a recipient
  open everyone's link and defeat the surprise
  ([NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md)). "Share all" therefore
  means "guide me through sharing each, one by one", not "send them all together".
- **Where it earns its place:** the value is greatest for the **account-free** and
  [shared-by-link](../../glossary/shared-by-link-group.md) modes, where a link is the only channel to a
  participant. In account / linked-member groups the participant sees their own assignment in-app
  ([UC-SS-011](UC-SS-011-see-my-assignment.md)), so hand-out matters less there.
- **Offline/local-first:** the links are generated on the device and the handed-out state is stored on
  the device; the whole pass works with no account and no network
  ([NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md)).
- **Persistence:** the handed-out state is **device-local and not synced** — it is the Organizer's own
  distribution progress on this device, not shared group data (the same class as recently-opened groups
  or the culture choice).
- **Localization:** the hand-out action, the progress count and the per-person state are DE/EN/FR
  ([NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)); a count needs a
  singular form as well as a plural (the M2 lesson).

## Open questions

- **Should the handed-out state ever be shared between the Organizer's own devices?** For v1 it is
  device-local only (progress is inherently "what I did from *this* phone"). Revisit only if a real need
  to continue a hand-out on a second device appears.

## Traceability

- Requirements: [FR-045](../../requirements/functional/FR-045-guided-hand-out-of-reveal-links.md)
- Feature spec: [FEAT-015](../../features/FEAT-015-guided-hand-out-of-reveal-links.md) /
  [TP-015](../../features/TP-015-guided-hand-out-of-reveal-links.md)
- Related use cases: [UC-SS-005](UC-SS-005-share-reveal-links.md) (generate & share the links this pass
  hands out), [UC-SS-006](UC-SS-006-open-reveal-link.md) (what the recipient opens),
  [UC-SS-018](UC-SS-018-share-and-manage-as-admin.md) (the shared-by-link mode where hand-out matters
  most)
- Governing ADR: [ADR-0028](../../adr/0028-browser-platform-adapters-block.md) (the `IShareService`
  platform seam the pass reuses — no new ADR)
- Glossary: [Reveal link](../../glossary/reveal-link.md), [Assignment](../../glossary/assignment.md),
  [Draw](../../glossary/draw.md)
- Source: MNF-7 in the [backlog](../../roadmap/backlog.md#mobile-native-feel-pass--secret-santa-2026-09-02),
  from the [mobile brainstorm (2026-09-01)](../../brainstorming/2026-09-01-secret-santa-mobile.md)
