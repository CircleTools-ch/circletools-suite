---
date: 2026-09-01
facilitator: Adrian
participants: [Adrian]
topic: Secret-Santa mobile (Android) features
status: Draft         # Draft | Concluded
---

# Brainstorming — Secret-Santa mobile (Android) features (2026-09-01)

## Objective / framing question

The MAUI Android head is at web parity and now installs on a real phone (USB deploy, see
[device testing](../deployment/device-testing.md)). Beyond parity, **what does the Secret-Santa app
gain by being a phone app — capabilities a browser tab can't offer — and which are worth building?**

Context: the app is local-first and account-free by default; the organizer's core loop is
create → members → exclusions → draw → **share each person their reveal link**. On a phone, sharing
and reminding are where a native app earns its place.

## Divergent — raw ideas

Capture everything, no judging.

- **Native share sheet per reveal link** — one tap into WhatsApp / Messages / e-mail (the platform
  picker), instead of copy-then-paste.
- **QR code per reveal link** — show a code the recipient scans in the room; no messaging app at all.
- **Local notifications** — remind the organizer to draw before the event date; nudge a linked member
  whose wishlist is still empty; remind everyone the event is tomorrow.
- **Add the event to the phone calendar** — one tap from the event's date/location/description.
- **Home-screen app shortcuts** (long-press the launcher icon) → "New group", "My assignments".
- **Deep links** — a reveal link opens the installed app directly instead of the browser.
- **Biometric app lock** — face/fingerprint on open, because a phone left unlocked would expose who
  drew whom (assignments are the one secret the app keeps).
- **Haptic feedback** on the reveal tap — a small buzz as the giftee is shown.
- **Add members from phone contacts** — a contact picker instead of typing names.
- **Share all links for an event at once** — hand the whole set out in one action.
- **Material You / dynamic colour** — tint the neutral chrome from the system palette on Android 12+.
- **Offline-first, stated plainly** — a visible "works without account / offline" reassurance on the
  phone, where users expect a login wall.

## Clustering / themes

- **Distribution (get the link to the right person):** native share sheet, QR code, share-all,
  deep links.
- **Timeliness (act at the right moment):** local notifications, add-to-calendar.
- **Trust & privacy on a personal device:** biometric app lock, offline-first reassurance.
- **Convenience & delight:** contact picker, home-screen shortcuts, haptics, Material You.

## Convergent — selection

Score candidates. Impact/effort/fit on a 1–5 scale (5 = best).

| Idea | Impact | Effort (low=better) | Fit | Decision |
|------|:------:|:-------------------:|:---:|----------|
| Native share sheet per link | 5 | 4 | 5 | **Selected — built this pass (ADR-0028 seam)** |
| QR code per reveal link | 4 | 4 | 5 | Selected — next |
| Local notifications (draw/date/empty-wishlist) | 4 | 2 | 4 | Parked — needs a scheduling design |
| Add event to phone calendar | 3 | 4 | 4 | Selected — next |
| Biometric app lock | 3 | 3 | 4 | Parked — privacy nicety, opt-in |
| Home-screen shortcuts | 2 | 4 | 3 | Parked |
| Deep links (reveal opens the app) | 3 | 2 | 3 | Parked — ties into the deferred production origin |
| Add members from contacts | 3 | 2 | 3 | Parked — privacy review first |
| Share all links at once | 3 | 4 | 4 | Selected — next (small follow-on to the share seam) |
| Haptic feedback on reveal | 2 | 5 | 3 | Parked — cheap delight, bundle later |
| Material You dynamic colour | 2 | 3 | 2 | Rejected — conflicts with ADR-0040 neutral chrome |

## Decisions & selected ideas

- **Built now:** the **native share sheet per reveal link** via a new `IShareService` platform seam
  (ADR-0028), lit up on the phone and mobile browsers; Copy stays for every platform.
- **Selected for a next pass** (small, high-fit, no new architecture): **QR code per link**,
  **add-to-calendar**, **share all links at once**. Each becomes a backlog item; take through the
  process if pursued.

## Rejected / parked (for the record)

- **Local notifications** — high value but needs a real scheduling/permission design (and, for
  "remind everyone", a reachable origin); park until after the reveal-origin/hosting decision.
- **Deep links** and **Material You** — deep links depend on the deferred production origin; Material
  You conflicts with the deliberate neutral chrome ([ADR-0040](../adr/0040-ui-component-library-mudblazor.md)).
- **Biometric lock, contacts picker, shortcuts, haptics** — parked as later polish; contacts and lock
  warrant a privacy review ([NFR-004](../requirements/non-functional/NFR-004-data-protection-and-retention.md)) first.

## Follow-up actions

- [x] Native share sheet — built (ADR-0028 seam; Share page). — Adrian, 2026-09-01
- [ ] QR code per reveal link — spec as a feature/backlog item. — Adrian
- [ ] Add-to-calendar for an event — backlog item. — Adrian
- [ ] Share all links at once — backlog item (follow-on to the share seam). — Adrian
- [ ] Revisit local notifications after the reveal-origin/hosting decision. — Adrian

## Promoted (2026-09-02)

A mobile-first "native feel" review (on-device/on-screen) took these forward. The **governing decision** —
one neutral Material design across web + both device heads, native feel earned through motion, touch
ergonomics and OS integration rather than per-platform chrome — is [ADR-0054](../adr/0054-mobile-native-feel-strategy.md).
The distribution/convenience/delight ideas above are now tracked as **MNF-1…10** in the
[backlog](../roadmap/backlog.md#mobile-native-feel-pass--secret-santa-2026-09-02); the near-term subset
(touch ergonomics, navigation clean-up, haptics, pull-to-refresh, action sheets, FAB) is specified as
[FEAT-008](../features/FEAT-008-mobile-native-feel-polish.md). Material You stays rejected
([ADR-0054](../adr/0054-mobile-native-feel-strategy.md) declines it, per
[ADR-0040](../adr/0040-ui-component-library-mudblazor.md)).
