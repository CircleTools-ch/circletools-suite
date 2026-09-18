# Google Play store listing — Secret Santa

Everything needed for the **Secret Santa** (`ch.circletools.secretsanta`) Google Play listing, in one
place: the copy in all three product languages and the visual artifacts. Paste the text into Play
Console → *Main store listing* (+ *Manage translations*), and upload the assets from `assets/` and
`screenshots/`.

This complements the submission runbook: [`docs/deployment/mobile-apps.md`](../../docs/deployment/mobile-apps.md).

## Contents

| File | Play Console field | Spec |
|------|--------------------|------|
| `listing-en.md` | Default listing (English) | name ≤30, short ≤80, full ≤4000 |
| `listing-de.md` | German translation (**Wichteln**) | same |
| `listing-fr.md` | French translation (**Père Noël secret**) | same |
| `assets/app-icon-512.png` | App icon | 512×512 PNG, ≤1 MB, **no alpha** |
| `assets/feature-graphic-1024x500{,-de,-fr}.png` | Feature graphic (per language) | 1024×500 PNG/JPEG, ≤15 MB |
| `screenshots/phone/{en,de,fr}/` | Phone screenshots | 2–8, ~20:9 portrait |
| `screenshots/tablet/en/` | 7-inch **and** 10-inch tablet | 2–8, **1080×1920 (9:16)** — one set covers both slots |

### Screenshot inventory

Captured from the running web app (identical shared UI), snackbars stripped.

**Phone** — 430×932 portrait (~20:9):
- **en/** — 01 home · 02 group · 03 event · 04 members · 05 draw · 06 share · 07 reveal
- **de/** — 01 home · 02 group · 03 event · 04 members · 05 draw · 06 share · 07 reveal (app title localizes to *Wichteln*)
- **fr/** — 01 home · 02 group · 03 event · 04 members · 05 draw · 06 share · 07 reveal (app title localizes to *Père Noël secret*)

**Tablet** — 1080×1920 (exactly 9:16), English:
- **tablet/en/** — 01 group · 02 event · 03 members · 04 share · 05 reveal (tablet layout = persistent nav sidebar + content)

The **reveal** shot is the payoff — a recipient opening their private link sees who they drew (e.g. Anna →
Clara) and that person's wishlist; the **share** shot shows the organizer's per-person Share / Copy / QR
links. These two are the strongest for the top of the listing.

> **Tablet slots:** the `tablet/en/` set is **1080×1920**, which satisfies **both** the 7-inch slot
> (each side 320–3,840 px) and the 10-inch slot (1,080–7,680 px) — upload the same five to both. Play uses
> the default-language (English) graphics for locales without their own, so these cover DE/FR too; localized
> DE/FR tablet shots can be generated the same way if wanted. A portrait-column app leaves whitespace on a
> tall tablet canvas — normal and compliant.
>
> **Phone ratio & resolution:** phone shots are **430×932 (~20:9)** — a normal tall-phone ratio Play accepts
> in practice, though taller than the strict 9:16 the field text cites; if the uploader ever objects,
> regenerate at **540×960 (exact 9:16)**. They're also **below 1080 px/side**, so valid to publish but not
> **promotion/featuring** eligible — swap in on-device Pixel captures (1080×2400) for featuring.

The `.svg` files in `assets/` are the editable sources; the `.png`s beside them are what you upload
(rendered from the app's real brand marks in a headless browser at exact pixel size).

## Listing-level facts (not per-language)

- **Category:** Lifestyle · **Free** · **No ads**
- **Privacy policy:** `https://secretsanta.circletools.ch/privacy`
- **Content rating:** Everyone / PEGI 3 / USK all ages (interactive element: *Users Interact*)
- **Contact:** info@circletools.ch

## Notes

- Google applies its own rounded-corner mask to the icon, so `app-icon-512.png` is a **full square**
  with no pre-rounded corners and no transparency (opaque burgundy `#7a1f2b`).
- **Feature graphic is localized** — upload the matching file under each language's graphics:
  `feature-graphic-1024x500.png` (EN, *Secret Santa*), `-de.png` (*Wichteln*), `-fr.png`
  (*Père Noël secret*). The brand mark **floats on the gradient with no tile** (the ring + gift only),
  so it no longer blends into the burgundy background. The standalone **app icon keeps its burgundy
  square** (Google requires a full opaque icon).
- Screenshots are captured from the standalone web app, which renders the **identical** shared UI as
  the MAUI head. They're publishable as-is; see the resolution caveat above for promotion/featuring.
- German uses **Wichteln** (what German users search) rather than a literal translation; French uses
  **Père Noël secret**.
