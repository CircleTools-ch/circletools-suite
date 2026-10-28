# Deployment — Store submission guide (Secret-Santa 1.0)

The click-by-click order for getting **Secret Santa** (`ch.circletools.secretsanta`) from TestFlight /
closed testing into **production** on both stores. Written **2026-10-28**, after the first iOS release
run failed with *"Refusing to upload: TestFlight Test Information is not ready"*.

This guide is the **order of operations**; the texts and answers to paste live next to the assets:

- `store/app-store/` — App Store Connect: `README.md` (every non-language answer: privacy label, age
  rating, review notes, DSA, pricing) and `listing-{en,de,fr}.md` (name, subtitle, keywords, description,
  TestFlight texts).
- `store/google-play/` — Play Console: `README.md` (app-content declarations, data-safety form) and
  `listing-{en,de,fr}.md` (title, short and full description, release notes).

Background and the reasons behind each step: [mobile runbook](mobile-apps.md) (Tracks A/B) and the
[production-release plan](production-release-plan.md). Decisions it relies on:
[ADR-0030](../adr/0030-mobile-store-publisher-identity.md) (publisher identity),
[ADR-0047](../adr/0047-versioning-and-release-strategy.md) (versions),
[ADR-0057](../adr/0057-app-store-naming-and-localization.md) (store naming).

## Step 0 — Unblock the TestFlight upload (today, ~10 minutes)

1. Sign in to [App Store Connect](https://appstoreconnect.apple.com) with `info@circletools.ch`.
2. The app record's **Primary Language** is **German** (App Information). The TestFlight texts must
   exist for the primary language — that is the "primary locale" in the error — so fill them **in German
   first**.
3. **TestFlight** tab → left sidebar **Test Information**, localization **German**. Fill, from
   `store/app-store/README.md` → *TestFlight → Test Information* and `listing-de.md`:
   - Beta App Description, Feedback Email `info@circletools.ch`, Marketing URL, Privacy Policy URL;
   - **Beta App Review Information**: first/last name, phone, email; keep *Sign-in required* checked with the demo account (A3)
     and paste the review notes (needed later for external testers; fill it now).
   - Optional: add English and French in the localization picker and paste `listing-en.md` /
     `listing-fr.md` — external testers then see their language.
   - **Save.**
4. GitHub → **Actions → Release (mobile)** → the failed run → **Re-run failed jobs** (or run the
   workflow again). Nothing was uploaded, so the same build number is still free.
5. Wait for **processing** (a few minutes to ~1 h; Apple emails when done). Export compliance is
   pre-answered in `Info.plist`, so no questionnaire appears.

## Part A — Apple App Store

### A1. Account-level, once

1. **Business → Agreements**: the *Free Apps* agreement is part of the program licence — confirm nothing
   shows *Action needed* (a pending new licence version blocks uploads and submissions).
2. **Business → Digital Services Act → trader status**: declare **non-trader** (reasoning in
   `store/app-store/README.md`). Without a declaration the app is not distributed in the EU.

### A2. Get the build onto the test iPhone

Internal TestFlight testers must be members of the App Store Connect team, and need no review:

1. **Users and Access → +**: invite the iPhone owner's Apple ID email with the role **Customer Support**
   (no access to code signing or the business side). They accept the email invitation.
2. **TestFlight → Internal Testing → +** create a group "Family", enable *automatic distribution*, add the
   new user and yourself.
3. On the iPhone: install **TestFlight** from the App Store (signed in with that same Apple ID), open the
   invitation, **Install**.

Alternative without a team seat: an **External Testing** group with a public link — requires a one-time
Beta App Review (~24 h) for the first build, which Step 0 already prepared.

**Before going further, walk the full flow on the TestFlight build** (create → members → exclusions → draw
→ share → open a reveal link → own wishlist; sign in; reminders) in DE/EN/FR, and work through the open
device-verify items in the [mobile runbook checklist](mobile-apps.md#submission-checklist) — notably the
**Apple Team ID in `apple-app-site-association`** (Universal Links) and native passkeys on iOS.

### A3. Prepare the review demo account (production)

1. Create a mail alias `appreview@circletools.ch` on the Infomaniak mailbox.
2. In the production app, register that address, confirm it, and create one **fictitious** group (the
   cast from the screenshot script below) with an event and a completed draw.
3. Store the password in the password manager — **never** in the repo.

### A4. App record, once

From `store/app-store/README.md`:

1. **App Information**: subtitle (per language, from `listing-*.md`), category *Lifestyle* / *Utilities*,
   content rights *No*, **age rating** questionnaire → 4+.
2. **Pricing and Availability**: Free; all regions except China mainland; Mac / Vision Pro off.
3. **App Privacy**: privacy-policy URL, then *Get Started* → the five data types in the README table, each
   *App Functionality*, *linked*, *not tracking*. **Publish** the label.
4. Add the **English (U.S.)** and **French** localizations (language picker, top right of the version
   page); German is the primary language and already exists.
5. **Primary language — German or English?** A storefront whose language the app does not have (Italy,
   Spain, the Netherlands, …) shows the **primary** language. German keeps the app Swiss/DACH-first;
   English reads for more of the world. It can be changed later under App Information once both
   localizations are filled, so keep German for now and revisit if the app is offered worldwide.

### A5. Version number

The App Store version must equal the build's `CFBundleShortVersionString`. Today `version.json` says
`0.1`, so builds are `0.1.<height>`. Per [ADR-0047](../adr/0047-versioning-and-release-strategy.md) the
launch bump is **`"version": "1.0"`** in `version.json` (one line, merged to `main`); every build after it
is `1.0.<height>`. Create the App Store version with **exactly** the string TestFlight shows for the build
you submit (version page → *+ Version* or edit the number before submission). The same bump makes the
Android `versionName` read `1.0.x`.

### A6. Screenshots

Capture per [Screenshots for the App Store](#screenshots-for-the-app-store) below; upload per language
under **iPhone 6.9"** and **iPad 13"** on the version page (drag to reorder — reveal first).

### A7. Version page and submission

1. Per language: promotional text, description, keywords (from `listing-*.md`), screenshots.
2. Once: support URL, marketing URL, copyright, **Build** (pick the processed build), **App Review
   Information** (demo account, contact, notes from the README), **Manually release this version**.
3. **Add for Review → Submit.** Review takes typically 1–3 days. Typical first-round questions for this
   app — WebView/minimum functionality (4.2), account deletion (5.1.1(v)), demo access (2.1) — are
   pre-answered in the review notes; reply in *App Review* messages, don't resubmit blindly.
4. On *Pending Developer Release*: **Release this version**. It appears in the store within ~24 h.

## Part B — Google Play

### B1. Closed test — the long pole (if not already running)

From [Track A step 9](mobile-apps.md#track-a--google-play): **12 testers opted in for 14 continuous
days**, then the production-access application. Every day of delay comes off December.

1. **Testing → Closed testing → Create track** (or use *Alpha*), **Testers**: an email list of ~15 family
   and friends' **Google-account** addresses.
2. Promote the newest internal build to the track, roll out, and send testers the **opt-in link**. Each
   must accept *and install from Play* on a real device.
3. Watch *Testing → Closed testing → Testers* — the clock runs once ≥ 12 are in. Keep shipping fix
   builds to the track during the window; regular updates count in your favour.
4. Collect what testers say — the application asks about it.

### B2. App content declarations

**Policy → App content**: work through every card with the answers in `store/google-play/README.md` →
*App content declarations* and *Data safety form*. Re-submit the **data-safety form** even if it was
filled before — it predates sync, online groups and own-entry editing.

### B3. Store listing

1. **Grow → Store presence → Main store listing** (English, default): app name, short and full description
   from `listing-en.md`; app icon `assets/app-icon-512.png`; feature graphic
   `assets/feature-graphic-1024x500.png`; phone screenshots (the Pixel 9 Pro set, see below); 7- and 10-inch
   tablet screenshots `screenshots/tablet/en/`.
2. **Manage translations → Add your own translations**: German (`de-DE`) and French (`fr-FR`) —
   `listing-de.md` / `listing-fr.md`, plus the localized feature graphic and phone screenshots per
   language.
3. **Store settings**: category *Lifestyle*, tags, contact email `info@circletools.ch`, website.

### B4. Apply for production access (day 14+)

**Dashboard → Apply for production**. Answer honestly in your own words; the shape Google expects:

- **About your closed test** — how testers were recruited (family and friends, the target group), how
  easy it was to get testers, how engaged they were, and a summary of the feedback (e.g. the issues the
  S24 and release device tests found and fixed: date picker on phones, link handling, reminders).
- **About your app** — intended audience (families and friend groups organizing a gift exchange, DACH +
  Romandy first), what it offers (offline, no account, private links), expected installs in year one
  (honest small number).
- **Production readiness** — what changed based on testing, and how you decided it is ready (full-flow
  device passes in DE/EN/FR, store review of data safety).

Review: up to 7 days. A rejection usually asks for more testing — keep the closed test running meanwhile.

### B5. Production release

1. **Production → Create new release**, add the newest AAB (promote from closed testing), release notes
   per language from `listing-*.md` (`<en-US>…</en-US>` block format).
2. **Countries / regions**: all, or start with Switzerland, Germany, Austria, France, Belgium,
   Luxembourg, Liechtenstein and add the rest later.
3. **Staged rollout** 20 % → 50 % → 100 %, checking *Android vitals* (crashes, ANRs) between steps.

## Screenshots

One story in every shot and every language, with **fictitious** people only (no real personal data,
CLAUDE.md). Clear the app first (fresh install or delete all groups), set the **device language** to the
target language, and build this cast:

| | |
|---|---|
| Group | *Family* / *Familie* / *Famille* |
| Members | Anna, Ben, Clara, David, Elena, Felix |
| Exclusion | Anna ↔ Ben (a couple) |
| Event | *Christmas 2026* / *Weihnachten 2026* / *Noël 2026*, 24.12.2026, budget 20–50 CHF |
| Wishlist (Clara) | a good novel; warm socks; dark chocolate |

The six shots, in upload order: **1 reveal** (Clara's wishlist seen from Anna's link) · **2 share links**
(with a QR open) · **3 draw** (just completed) · **4 group** · **5 event details** · **6 wishlist** (own
entry from a link). Before each shot: Wi-Fi on, battery charged, **Do Not Disturb** on (no notification
icons), no snackbar on screen.

### Screenshots for the App Store

**iPhone** — on the TestFlight build, press **side button + volume up**. Transfer the originals without
recompression: USB cable to the Windows PC (*Photos* import or Explorer → `DCIM`), or AirDrop / iCloud
Photos *Download unmodified original*. **Not** via WhatsApp or Messages — they downscale.

The upload slot needs exact sizes. What the phone produces, and what to do with it:

| iPhone | Native screenshot | Slot | Action |
|---|---|---|---|
| 16 Pro Max, 17 Pro Max | 1320×2868 | 6.9" | upload as is |
| 14 Plus, 15 Plus/Pro Max, 16 Plus | 1290×2796 | 6.9" | upload as is |
| Air | 1260×2736 | 6.9" | upload as is |
| 16 Pro, 17, 17 Pro | 1206×2622 | 6.9" | scale to 1320×2868 (same aspect ratio) |
| 14 Pro, 15, 15 Pro, 16 | 1179×2556 | 6.9" | scale to 1290×2796 (same aspect ratio) |
| 12, 13, 14 | 1170×2532 | 6.5" | scale to 1284×2778 (same aspect ratio) |

Scaling within the same aspect ratio is invisible; any image tool that resizes to an exact size and saves
PNG without alpha works. Put the results in `store/app-store/screenshots/iphone-6.9/<lang>/`.

**iPad 13"** — required because the app supports iPad. An iPad Pro/Air 13" screenshots at 2048×2732 or
2064×2752 directly. Without one, render the same cast from the web app (identical shared UI) in a
headless browser at a 1032×1376 viewport and device-scale factor 2 → **2064×2752**, as was done for the
Play tablet set. Put them in `store/app-store/screenshots/ipad-13/<lang>/`.

### Screenshots for Google Play

Done 2026-10-28 on a **Pixel 9 Pro** with the Play-installed build (inventory in `store/google-play/README.md`).
To re-shoot:

1. USB debugging on; `adb devices` lists the phone. For the session: `adb shell wm size 1280x2856`,
   `adb shell wm density 480` (full resolution, same layout), `adb shell cmd uimode night no` (light), and
   Android demo mode for a clean status bar (`settings put global sysui_demo_allowed 1`, then
   `am broadcast -a com.android.systemui.demo -e command clock -e hhmm 1200`, battery 100, Wi-Fi 4).
2. Sign in as the review account; set the group/event names and wishlists to the target language on the web
   (they sync), and the app language under *Settings → Language*.
3. Per shot `adb exec-out screencap -p > shot.png`; open a reveal link with
   `adb shell am start -a android.intent.action.VIEW -d '<link>'` (App Links open it in the app).
4. **Crop to 1280×2560** (y = 210…2770): Play requires the long side to be at most twice the short side;
   this removes the status bar and the gesture bar.
5. Afterwards reset: `wm size reset`, `wm density reset`, night mode back, demo mode `exit`.

Tablet shots (`screenshots/tablet/en/`) stay valid unless the tablet layout changed visibly.

## Related

- [Mobile apps runbook](mobile-apps.md) — Tracks A/B, the submission checklist
- [Production-release plan](production-release-plan.md) — the parallel tracks and the calendar
- [Device testing](device-testing.md) — USB / `adb` set-up
- [ADR-0057](../adr/0057-app-store-naming-and-localization.md) — store naming & listing localization
