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
| `closed-test/testers-community/` | Closed-test reports (feedback, ASO, production-access draft) and what we do with them | — |
| `assets/app-icon-512.png` | App icon | 512×512 PNG, ≤1 MB, **no alpha** |
| `assets/feature-graphic-1024x500{,-de,-fr}.png` | Feature graphic (per language) | 1024×500 PNG/JPEG, ≤15 MB |
| `screenshots/phone/{en,de,fr}/` | Phone screenshots | 2–8, ~20:9 portrait |
| `screenshots/tablet/{en,de,fr}/` | 7-inch **and** 10-inch tablet | 2–8, **1440×2560 (9:16)** — one set covers both slots |

### Screenshot inventory

**Phone** — captured **on a Pixel 9 Pro from the Play-installed build** (2026-10-28, `0.1.129`) at 1280×2856,
light mode, Android demo-mode status bar, then cropped to **1280×2560 (exactly 2:1)** — status bar and
gesture bar removed, RGB, no alpha; ≥ 1080 px on both sides, so promotion-eligible. Same seven shots in
**de/**, **en/**, **fr/**:

01 reveal · 02 share · 03 draw · 04 own-wishlist (the link before revealing) · 05 event · 06 group · 07 exclusions

Data is the fictitious demo group of the review account (`appreview@`): *Familie Meier* / *Meier Family* /
*Famille Meier*, event *Weihnachten / Christmas / Noël 2026*, 24.12.2026, CHF 20–50, Bern, members Anna (the
account), Ben, Clara, David, Elena, Felix, exclusion Anna ↔ Ben. The reveal is Ben's link (Ben → David). The
QR-code dialog is deliberately **not** used: it shows a working link into the review account.

Re-shooting: [store submission guide](../../docs/deployment/store-submission-guide.md#screenshots-for-google-play).

**Tablet** — captured **from the same Play build on the Pixel 9 Pro** (2026-10-28, `0.1.129`) with the
display switched to a 10-inch tablet geometry (`wm size 1440x2800`, `wm density 320` → 720 dp wide), then
cropped to **1440×2560 (exactly 9:16)**, status bar and gesture bar removed. Same seven shots and same cast as
the phone set, in **de/**, **en/**, **fr/**. At 720 dp the app keeps its bottom navigation and widens the
content column — what a portrait tablet really shows.

The **reveal** shot is the payoff — a recipient opening their private link sees who they drew (Ben →
David) and that person's wishlist; the **share** shot shows the organizer's per-person Share / Copy / QR
links. These two lead the set for the top of the listing.

> **Tablet slots:** each `tablet/<lang>/` set is **1440×2560**, which satisfies **both** the 7-inch slot
> (each side 320–3,840 px) and the 10-inch slot (1,080–7,680 px) — upload the same seven to both, per
> language. Play requires **16:9 / 9:16** for tablets. A portrait-column app leaves whitespace on a tall
> tablet canvas — normal and compliant.
>
> **Phone ratio:** 1280×2560 is exactly 2:1 — the long side may not exceed twice the short side.

The `.svg` files in `assets/` are the editable sources; the `.png`s beside them are what you upload
(rendered from the app's real brand marks in a headless browser at exact pixel size).

## Listing-level facts (not per-language)

- **Category:** Lifestyle · **Free** · **No ads**
- **Privacy policy:** `https://secretsanta.circletools.ch/privacy`
- **Content rating:** Everyone / PEGI 3 / USK all ages (interactive element: *Users Interact*)
- **Contact:** info@circletools.ch · website `https://secretsanta.circletools.ch`
- **Tags (Store settings):** Lifestyle · Events · Holidays (pick the closest offered)

## App content declarations (Policy → App content)

Answers as of the ADR-0086 build. Keep them in sync with the App Store privacy label
(`store/app-store/README.md`) and the privacy policy whenever what the app stores changes.

| Declaration | Answer |
|---|---|
| Privacy policy | `https://secretsanta.circletools.ch/privacy` |
| Ads | **No**, the app contains no ads |
| App access | **All or some functionality is restricted** → add instructions: *"No login is needed for the core app. To try sync, sign in with:"* + the review demo account (`appreview@circletools.ch`, password from the password manager) |
| Content rating (IARC) | category *All other app types*; violence/sexuality/language/drugs/gambling: **no**; *users can interact or exchange content*: **yes** (private groups via links); *shares user location*: **no**; *digital purchases*: **no** → Everyone / PEGI 3 |
| Target audience | **13–15, 16–17, 18 and over** (teenagers take part in family draws); *appeals to children*: **No** — no under-13 group, so the Families policy does not apply |
| News app | No |
| Health apps | No health features |
| Government app | No |
| Financial features | My app doesn't provide any financial features |
| Advertising ID | **No**, the app does not use it (if Play flags an `AD_ID` permission in the merged manifest, a dependency added it — remove it with `tools:node="remove"`, don't declare it) |
| Data safety | see below |

### Data safety form

- **Does your app collect or share any of the required user data types?** Yes.
- **Is all of the user data collected by your app encrypted in transit?** Yes (HTTPS only).
- **Which account creation methods?** Username and password, and passkeys ("Other"); account creation is
  **optional**.
- **Delete-account URL:** `https://secretsanta.circletools.ch/delete-account`. Users can also request
  deletion of some data without deleting the account (groups put online can be deleted in the app): **yes**.

| Data type | Collected | Shared | Optional? | Purposes |
|---|---|---|---|---|
| Personal info → **Name** (display name, group members' names) | yes | no | optional | App functionality, Account management |
| Personal info → **Email address** | yes | no | optional | App functionality, Account management |
| Personal info → **User IDs** | yes | no | optional | App functionality, Account management |
| App activity → **Other user-generated content** (groups, events, wishlists, gift notes, draw results) | yes | no | optional | App functionality |
| Device or other IDs → **Device or other IDs** (the app's own random device key, sent when joining an online group without an account or syncing — ADR-0080/0086) | yes | no | optional | App functionality |

Nothing is *shared*: Infomaniak (hosting) and Brevo (transactional email) are service providers acting on
our instructions, which Play excludes from "sharing". Not collected: location, financial, health, messages,
photos, audio, files, calendar (adding an event to the calendar is handed to the system calendar app and
never leaves the device), contacts, app activity/analytics, web browsing, hardware or advertising IDs, crash logs.
Collection happens only when the user creates an account or puts a group online — that is why each row is
*optional* and none is *processed ephemerally*.

## Notes

- Google applies its own rounded-corner mask to the icon, so `app-icon-512.png` is a **full square**
  with no pre-rounded corners and no transparency (opaque burgundy `#7a1f2b`).
- **Feature graphic is localized** — upload the matching file under each language's graphics:
  `feature-graphic-1024x500.png` (EN, *Secret Santa*), `-de.png` (*Wichteln*), `-fr.png`
  (*Père Noël secret*). The brand mark **floats on the gradient with no tile** (the ring + gift only),
  so it no longer blends into the burgundy background. The standalone **app icon keeps its burgundy
  square** (Google requires a full opaque icon).
- Phone and tablet screenshots are both captured from the **Play-installed app** on the Pixel 9 Pro (the
  tablet set by changing the display geometry with `wm size` / `wm density`), not from the web app.
- German uses **Wichteln** (what German users search) rather than a literal translation; French uses
  **Père Noël secret**.
