---
status: Accepted
date: 2026-09-15
deciders: [adrian-moll]
tags: [branding, visual-identity, icons, accessibility]
---

# ADR-0059 — Brand marks: a ring enclosing a central glyph

## Context and problem statement

The suite brand marks (decided 2026-08-13, recorded in
[naming.md → Visual identity](../process/naming.md)) were built as a **ring formed of five glyphs**:
five white people arranged in a circle for CircleTools, five white gifts for Secret-Santa. The figures
*were* the circle — there was no separate border.

At large sizes the five-glyph ring is fine, but at **favicon / small-launcher size (16–24 px)** the
five glyphs merge into an indistinct blob: neither "people" nor "gifts" nor even "a circle" survives.
The mark fails exactly where an app icon is used most. Should we keep tuning the five-glyph ring, or
change the construction?

## Decision drivers

- **Legibility at 16–24 px** is the hard constraint — the mark must read as a favicon and a home-screen
  icon, not only at 512 px.
- **Keep the "circle" idea** — it is the brand's core concept ([`Circle`](../glossary/circle.md)) and
  the reason for the name.
- **Keep the settled palette** — navy `#03173d`, burgundy `#7a1f2b`, white glyphs (no rebrand).
- **One consistent system across apps**, so a new app inherits it (per the per-app rule).
- **Monochrome-safe** and full-bleed opaque icons for stores, per
  [ADR-0024](0024-ui-styling-and-theming.md).

## Considered options

1. **Keep the five-glyph ring**, tune glyph count / spacing.
2. **A single circular border enclosing one central glyph** — the ring is drawn as a real border; a
   single bold glyph sits inside and carries the mark.
3. **Drop the circle**, use only a centred glyph on the tile.

## Decision outcome

**Chosen: Option 2 — a ring enclosing a central glyph.** It keeps the circle as an explicit, always-
legible shape while letting one bold glyph (not five small ones) survive at favicon size.

Per-app instances:

- **CircleTools:** an overlapping **trio of white people** inside a **deep navy-blue `#0a2c6b`** ring on
  the navy `#03173d` tile.
- **Secret-Santa:** the **white wrapped present** (box + cross ribbon + bow; glyph unchanged from the
  2026-08-14 refinement) inside a **maroon `#4a0f18`** ring on the burgundy `#7a1f2b` tile.

Both rings are **recessed, tone-on-tone frames** (a shade darker than the ground). White and accent-
coloured rings (CircleTools blue `#3c6fd1`; a range of reds/golds for Secret-Santa) were explored and
rejected in favour of the calmer, more premium tone-on-tone look, applied consistently to both apps.
The central glyph carries all required contrast; the ring is decorative.

## Consequences

- **Positive:** the mark reads at favicon size (glyph-led); the circle concept is now explicit;
  consistent, extensible per-app system (swap glyph + ring colour); palette unchanged.
- **Negative:** the ring is intentionally low-contrast, so it nearly disappears below ~20 px — accepted,
  because the glyph, not the ring, is load-bearing at that size.
- **Follow-ups:** all mark assets regenerated — shared `app-main.svg` / `app-secretsanta.svg`, each web
  app's `favicon.svg` + rasterised `favicon.png` (64) / `icon-192.png` / `icon-512.png`, the DocFX
  `logo.svg` / `favicon.svg`, and the Secret-Santa MAUI `appiconfg.svg` + `Splash/splash.svg`
  (`appicon.svg` burgundy background unchanged; resizetizer regenerates store sizes at build). The
  `naming.md` Visual-identity spec is updated with a 2026-09-15 redesign note.
- **New risks:** none material; a purely visual change with no code/behaviour impact.

## Pros and cons of the options

### Option 1 — keep the five-glyph ring
- 👍 No asset churn; already shipped.
- 👎 Fails the primary constraint (illegible at favicon size); the actual problem being solved.

### Option 2 — ring + central glyph
- 👍 Legible small; keeps the circle; one extensible system; palette unchanged.
- 👎 Recessed ring fades at the very smallest sizes (acceptable — glyph carries the read).

### Option 3 — centred glyph, no circle
- 👍 Maximum small-size legibility.
- 👎 Throws away the "circle" that motivates the whole brand.

## Links

- Related ADRs: [ADR-0024](0024-ui-styling-and-theming.md) (styling/theming, superseded, holds the
  original brand-mark amendment), [ADR-0057](0057-app-store-naming-and-localization.md) (per-app
  identity).
- Spec: [naming.md → Visual identity](../process/naming.md).
- Glossary: [`Circle`](../glossary/circle.md).
