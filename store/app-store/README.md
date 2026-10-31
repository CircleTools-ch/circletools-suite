# Apple App Store listing — Secret Santa

Everything App Store Connect asks for to ship **Secret Santa** (`ch.circletools.secretsanta`, Apple ID
`6817309711`) through **TestFlight** and to the **App Store**: the per-language copy in
`listing-{en,de,fr}.md`, and below every answer that is *not* per language. The counterpart for Google
Play is `store/google-play/`; the step-by-step order is the
[store submission guide](../../docs/deployment/store-submission-guide.md).

| File | App Store Connect field |
|------|-------------------------|
| `listing-de.md` | German — **primary language** of the app record: name, subtitle, promotional text, keywords, description, TestFlight texts |
| `listing-en.md` | English (U.S.) localization |
| `listing-fr.md` | French localization |
| `screenshots/iphone-6.3/{en,de,fr}/` | iPhone screenshots, medium display (required slot) |
| `screenshots/iphone-6.9/{en,de,fr}/` | iPhone screenshots, large display (optional slot, not uploaded) |
| `screenshots/ipad-13/{en,de,fr}/` | iPad 13" screenshots (required slot) |
| `screenshots/ipad-11/{en,de,fr}/` | iPad 11" screenshots (optional slot) |

Never commit the review demo account's password or any signing material here — those live in the
password manager ([ADR-0050](../../docs/adr/0050-secrets-management.md)).

## TestFlight → Test Information (unblocks the CI upload)

`apple-actions/upload-testflight-build@v5` refuses to upload until this page is complete.

| Field | Value |
|-------|-------|
| Localization | the app's **primary language — German** (required); add English and French for external testers if wanted |
| Beta App Description | `listing-de.md` (then `-en`, `-fr`) → *TestFlight — Beta App Description* |
| Feedback Email | `info@circletools.ch` |
| Marketing URL | `https://secretsanta.circletools.ch` |
| Privacy Policy URL | `https://secretsanta.circletools.ch/privacy` |
| Beta App Review Information — contact | Adrian Moll · `info@circletools.ch` · your phone number (never published) |
| Sign-in required | **checked**, with the demo account (see *App Review Information*); the notes say no login is needed |
| Review notes | the *App Review notes* text below |
| License agreement | Apple's standard EULA (leave the default) |

## App Information (once per app)

| Field | Value |
|-------|-------|
| Primary language | **German** (set at record creation) — the fallback for every storefront without its own localization; can be switched to English (U.S.) later once both are filled |
| Bundle ID | `ch.circletools.secretsanta` |
| SKU | `secretsanta` (internal, any unique string — already set at record creation) |
| Category | Primary **Lifestyle**, secondary **Utilities** |
| Content rights | **No**, the app does not contain, show or access third-party content |
| Age rating | see *Age rating* below → expected **4+** |

## Pricing and Availability

- **Price:** Free (USD 0, tier 0). No in-app purchases.
- **Availability:** all countries and regions **except China mainland** (an app there needs an ICP filing
  number, which a Swiss individual cannot get). Pre-order: no.
- **iPhone and iPad apps on Apple silicon Mac / Apple Vision Pro:** **off** — the build is not tested there.

## App Privacy (privacy "nutrition label")

Privacy Policy URL: `https://secretsanta.circletools.ch/privacy`. User Privacy Choices URL: leave empty.

**Do you or your third-party partners collect data from this app?** → **Yes** (only when the user creates
an account or puts a group online; still a "yes" for the label).

| Data type | Collected | Used for | Linked to the user | Used for tracking |
|-----------|-----------|----------|--------------------|-------------------|
| Contact Info → **Email Address** | yes (accounts) | App Functionality | yes | no |
| Contact Info → **Name** (display name, names of group members) | yes | App Functionality | yes | no |
| User Content → **Other User Content** (group, event details, wishlists, gift notes, draw results) | yes | App Functionality | yes | no |
| Identifiers → **User ID** (account id) | yes | App Functionality | yes | no |
| Identifiers → **Device ID** (the app's own random device key, for joining an online group without an account and for sync — ADR-0080/0086) | yes | App Functionality | yes | no |

Everything else — location, contacts, photos, health, financial, browsing, search, purchases, usage data,
diagnostics, advertising data — is **not collected**. The app has no analytics or crash SDK;
location purpose strings exist in `Info.plist` only because a framework references the API (it is never
called). Keep this label in sync with the Play data-safety form (`store/google-play/README.md`) and the
privacy policy.

## Age rating questionnaire

Answer **None / No** to everything: no violence, sexual content, profanity, horror, drugs/alcohol/tobacco,
simulated or real gambling, contests, medical/wellness content; no unrestricted web access; no
advertising; no messaging or chat; no parental controls or age assurance.

**User-generated content → No.** Names and wishlists are shared only inside a private group, through
links the organizer hands to people they know — there is no public feed, no discovery and no contact
with strangers. (Answering *yes* would bring Guideline 1.2 — reporting, blocking, moderation — into
scope.) Expected result: **4+**.

## Version page (per release)

| Field | Value |
|-------|-------|
| Version | must equal the build's version string as shown in TestFlight (nbgv `SimpleVersion`, e.g. `1.0.412`, [ADR-0047](../../docs/adr/0047-versioning-and-release-strategy.md)) |
| Copyright | `2026 Adrian Moll` |
| Support URL | `https://secretsanta.circletools.ch/imprint` (has the contact address) — replace with a `/support` page once one exists ([backlog PR-7](../../docs/roadmap/backlog.md)) |
| Marketing URL | `https://secretsanta.circletools.ch` |
| Routing app coverage file | none |
| Build | the processed TestFlight build |
| Release | **Manually release this version** (so the go-live moment is ours) |
| Phased release | **on** for updates (7-day ramp); a first version always goes to everyone |
| Reset ratings | no |

### App Review Information

- **Sign-in required:** leave **checked** — it is the only place for the **demo account** credentials;
  the notes tell the reviewer the app works without signing in. User name `appreview@circletools.ch` (a
  mail alias you control) and its password from the password manager. The account should already contain
  one fictitious group with a drawn event.
- **Contact:** Adrian Moll, `info@circletools.ch`, phone number.
- **Notes** (English, paste as is):

```
Secret Santa organizes a gift exchange: create a group, add people, set exclusions, draw names and share a private link with each person that reveals only who they drew, plus that person's wishlist.

No account is needed: the app is fully functional offline, and all data stays on the device. You can review everything without signing in: create a group, add a few people and an event, open "Draw" and tap "Run draw", then open "Share links".

The account is optional and only used to sync between devices and to join groups shared with an account. To try it, sign in with the demo account provided; it already contains a group with a completed draw. The account can be deleted in the app: account menu (top right) → account settings → "Delete account".

The interface is a locally bundled Blazor UI rendered in a native .NET MAUI shell; it does not load a website. Reveal links open at https://secretsanta.circletools.ch, or in the app when it is installed (Universal Links).

The app uses only exempt encryption (HTTPS, system passkey APIs). Location purpose strings exist only because a system framework references the API; the app never requests location.
```

> Check the button labels in the notes against the build before submitting — they are the English UI
> strings and change with the UI.

### Other declarations

- **Export compliance:** answered in the build (`ITSAppUsesNonExemptEncryption = false`) — no prompt.
- **Advertising identifier (IDFA):** not used.
- **Sign in with Apple:** not required — the app has its own email/password + passkey sign-in and no
  third-party or social login (Guideline 4.8).
- **Account deletion:** in the app (Guideline 5.1.1(v)) and at `https://secretsanta.circletools.ch/delete-account`.

## Business → DSA trader status (once per developer account)

Declare **non-trader**: the app is a free personal project with no ads, no in-app purchases and no
income, consistent with the imprint and with the "no postal address" decision in
[launch-readiness §B](../../docs/deployment/launch-readiness.md). If it is ever monetized, this flips to
*trader*, and Apple then publishes an address, phone and email on the EU storefronts.

## Screenshots

PNG or JPEG, RGB, **no alpha**, **exact** pixel sizes, 1–10 per device class per language (3–6 is the
sweet spot; the first three show in search results). Upload in file order — the payoff first, the same
seven shots as the Play listing.

| Slot (Media Manager name) | Accepted sizes (portrait) | Required |
|------|---------------------------|----------|
| iPhone **with Dynamic Island (medium display)** — 6.1"/6.3" | 1206×2622, 1179×2556 | **yes** |
| iPhone with Dynamic Island (large display) — 6.9" | 1320×2868, 1290×2796, 1260×2736 | no |
| iPad **13"** | 2064×2752, 2048×2732 | **yes** — the app runs on iPad |
| iPad 11" | 1640×2360, 1668×2388, 1668×2420, 1488×2266 | no — scaled down from 13" if missing |

As of 2026-10-31 App Store Connect requires the **medium** iPhone slot (it used to be 6.9") and uses it for
every iPhone size; the 13" iPad set covers every iPad. A localization without its own screenshots
("Using Existing Assets") shows the primary-language (German) ones — so each of the four English
storefronts (U.S., U.K., Australia, Canada) gets the `en/` set uploaded separately.

**Uploaded 2026-10-31:** `iphone-6.3/` and `ipad-13/` in German, English (U.S., U.K., Australia, Canada) and
French — 7 each. `iphone-6.9/` (the same shots scaled up) is kept for the optional large slot but not
uploaded. How to capture them is in the
[store submission guide](../../docs/deployment/store-submission-guide.md#screenshots-for-the-app-store).

### Screenshot inventory

Same seven shots in **de/**, **en/**, **fr/**, same cast and file names as the Play set
(`store/google-play/README.md`): 01 reveal · 02 share · 03 draw · 04 own-wishlist · 05 event · 06 group ·
07 exclusions. Data is the review account's demo group, renamed per language (*Familie Meier* /
*Meier Family* / *Famille Meier*, *Weihnachten / Christmas / Noël 2026*, Ben's and David's wishlists
translated) and **restored to German** afterwards. The QR dialog is not used.

**iPhone** (`iphone-6.3/`, 1206×2622 native — uploaded; `iphone-6.9/`, the same scaled to 1320×2868 — spare)
— captured **on an iPhone from the TestFlight build** on 2026-10-31 (16 Pro / 17 / 17 Pro class), light mode, app opened from its
home-screen icon, silent mode off and unplugged so the status bar shows no bell or charging bolt. The
demo data was switched per language from a desktop browser signed in as the review account (group and
event renamed, Ben's and David's wishlists edited through their own reveal links).

**iPad 11"** (`ipad-11/`, 1640×2360) — captured **on an iPad Air (M1) from the TestFlight build** on
2026-10-30, light mode, *Full Screen Apps* multitasking (windowed mode draws a resize handle in the corner),
app opened from its home-screen icon (an app opened from another app shows a "◀ TestFlight" / "◀ Notes"
back-link in the status bar). The status-bar date follows the iPad's system language (German).

**iPad 13"** (`ipad-13/`, 2064×2752) — the Air's native size does not fit this slot, so these are
**rendered from the production web app** (identical shared UI) signed in as the review account, in a
Playwright Chromium at a **1032×1376 viewport, device-scale factor 2** — exactly a 13" iPad's CSS
viewport, so the layout (permanent side drawer at ≥ 960 px) is what the app shows there. The cross-app
switcher icon is hidden for the shots because the native app hides it. No iOS status bar.
