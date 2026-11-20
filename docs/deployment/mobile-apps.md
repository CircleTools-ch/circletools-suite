# Deployment — Mobile apps (Android / iOS)

How a MAUI head gets from the repository into the two stores: what it costs, what has to happen in
which order, and how long each part actually takes. Built from the
`CircleTools.<App>.App` projects ([arc42 ch. 5](../arc42/05-building-block-view.md)).

For *testing* on the devices we own — before any of this — see [device testing](device-testing.md).

> **Status — both accounts live; nothing published (2026-09-18).** Both project developer accounts are
> **enabled**. Nothing has been submitted to public review. The immediate goal is to install the app on
> our own devices through each store's **internal testing** channel — which does **not** go through App
> Review — before running the full production tracks. See **First milestone** below.
>
> - **Google Play Console — registered & active.** ✅ USD 25 paid; `circletools.ch@gmail.com`; identity
>   verification cleared. Next: create the app record (Track A step 4).
> - **Apple Developer Program — enrolled.** ✅ Individual enrolment approved on the `info@circletools.ch`
>   Apple ID. Next: DSA trader status and register the App ID (Track B steps 3–4).
>
> Both accounts are project-dedicated per [ADR-0030](../adr/0030-mobile-store-publisher-identity.md).
> This runbook is followed at [milestone 8](../roadmap/roadmap.md); it was written up front because two
> of its steps have long lead times and one of them is irreversible.

## Targets

- **Android** → Google Play, signed **AAB**, phone + tablet.
- **iOS** → Apple App Store, signed **IPA**, iPhone + iPad.

## Before anything else: two prerequisites

**1. Brand and domain — settled.** ✅ The brand is `CircleTools` at `circletools.ch`, and the
`ApplicationId` / bundle identifier is **set** to `ch.circletools.secretsanta` (2026-08-13) — permanent
after the first release, so it will not change again. The same domain fixes the passkey relying-party ID,
which likewise cannot migrate ([ADR-0023](../adr/0023-local-dev-infrastructure-in-apphost.md)); that plus
the privacy-policy and account-deletion URLs the stores require are applied at deployment. See
[naming](../process/naming.md) and [NFR-007](../requirements/non-functional/NFR-007-store-distributability.md).

**2. iOS build host — no Mac needed.** ✅ iOS artifacts are built, signed and uploaded on
**GitHub-hosted macOS runners**, not on an owned Mac
([ADR-0065](../adr/0065-hosted-macos-ios-builds.md), which supersedes
[ADR-0031](../adr/0031-ios-build-host.md)). iOS is on the launch critical path — TestFlight is how the
iPhone-owning family tests. The trade-off is **no local iOS build or simulator**: signing is debugged
through CI runs. Buying an Apple-silicon Mac is retained only as a documented upgrade trigger.

Publisher identity and account ownership are decided in
[ADR-0030](../adr/0030-mobile-store-publisher-identity.md): enrol as a **private individual**, on
**accounts created for the project**, never the personal daily-driver ones.

## Costs

| Item | Cost | Notes |
|---|---|---|
| Apple Developer Program | **USD 99 / year** | recurring; lapses stop distribution |
| Google Play Console | **USD 25 once** | one-time registration fee, no renewal |
| Hosted macOS CI (chosen) | ~**CHF 0 / month** at this cadence | iOS builds only on `v*` tags & manual dispatch; macOS minutes bill at a **10× multiplier** (~USD 0.062/min), but a few short builds/month stay within included minutes ([ADR-0065](../adr/0065-hosted-macos-ios-builds.md)) |
| Apple-silicon Mac mini (not bought) | ~**CHF 650–800** once | upgrade path only, if build frequency or a hosted-image SDK lag ever justifies it ([ADR-0065](../adr/0065-hosted-macos-ios-builds.md) upgrade trigger) |
| Suite domain (`.ch`) | ~**CHF 10–20 / year** | required before first submission, see above |
| Cloud hosting for Web/API/DB | open | pending the [cloud-provider ADR](../adr/index.md#open-decisions-backlog) |

Year one, excluding hosting: roughly **CHF 130–150** — Apple USD 99/year, Google USD 25 once, and the
domain — with iOS built on hosted CI at ~CHF 0 and no Mac purchase.

## First milestone — our own devices, via the stores, no public review

The goal here is the first honest test: the **real store-delivered artifact** running on our own
Pixel 9 Pro and iPad Air. Both stores have an **internal testing** channel that does exactly this
**without going through public App Review**, and puts the build on the device within (roughly) an hour
of upload. Reach this before the full production tracks below.

Why internal testing and not sideloading or a 7-day dev profile: it installs the **same signed build a
public user will get** — Play App Signing on Android, an App Store distribution build on iOS — so it
exercises release signing, the store install/update flow, and (once the `.well-known` files are
finalised) real deep-link verification. Sideloading and free-provisioning builds skip all of that.

> There is exactly one native mobile head today — **Secret Santa** (`ch.circletools.secretsanta`),
> shipped to Android **and** iOS. "Both apps" is this one app on two platforms; the Main app has no
> MAUI head yet.

### Android → Pixel 9 Pro (Google Play internal testing)

Internal testing allows **up to 100 testers**, releases go live in **minutes with no review**, and it
is **not** subject to the 12-testers/14-day rule — that rule (Track A step 9) gates only the later
*production* application. So it is available as soon as the app record exists.

1. **Create the app** in Play Console (Track A step 4) and **set up Play App Signing + the upload key**
   (step 5). Back the upload key up offline before anything else.
2. **Give CI the signing secrets** — `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASSWORD`,
   `ANDROID_KEY_ALIAS`, `ANDROID_KEY_PASSWORD` — so `release-mobile.yml` emits a **signed AAB**; or build
   the signed AAB locally. The unsigned scaffold build cannot be uploaded.
3. In Play Console → **Testing → Internal testing**, create a release and **upload the AAB**. Add
   yourself and any family testers to the tester list by Google-account e-mail.
4. **Complete the declarations Play blocks the release on** — app content: privacy policy, data safety,
   content rating, target audience, ads (the same forms as Track A step 8; internal testing needs them
   too). The data-safety form needs the **privacy-policy URL** — now **live** at
   `https://secretsanta.circletools.ch/privacy` (with `/imprint` and `/delete-account`), so this no
   longer blocks. Caveat: the page renders **client-side** (WASM); a no-JavaScript fetch returns the app
   shell showing an error, so if a store validator or reviewer inspects the URL without running JS they
   may not see the policy text. Verify it passes, and consider a prerendered/static fallback for the
   legal routes if it trips.
5. **Copy the internal-testing opt-in link**, open it on the Pixel signed in with a tester Google
   account, accept, and install **from the Play Store**. Later builds arrive by bumping
   `ApplicationVersion` and uploading a new AAB.

The **app signing key SHA-256** (Play Console → *Protected with Play → App signing*) is now filled in
`assetlinks.json` (both WebApps) — **done 2026-09-18** — so App Links verify. It takes effect once the
web front is **redeployed** to serve the updated `.well-known/assetlinks.json`.

### iOS → iPad Air (TestFlight internal testing)

Internal testers (**up to 100**, members of the App Store Connect team) get builds with **no Beta App
Review** — the only wait is build **processing** (a few minutes to ~an hour). External testers would
need the one-time beta review; we don't need them to reach our own iPad.

1. **Register the App ID** for `ch.circletools.secretsanta` with the **Associated Domains** capability
   (Track B step 4 — needed for Universal Links and passkeys), and **create the App Store Connect app
   record** (step 6). Then fill **TestFlight → Test Information** for the record (Beta App
   Description, Feedback Email, primary locale): `apple-actions/upload-testflight-build@v5` checks it
   before uploading and fails the job with *"Refusing to upload: TestFlight Test Information is not
   ready"* if it is missing.
2. **Produce the signing assets and set the 6 iOS secrets** (Track B step 5): a distribution `.p12`, an
   App Store provisioning profile, and an App Store Connect API key →
   `IOS_DIST_CERT_P12_BASE64`, `IOS_DIST_CERT_PASSWORD`, `IOS_PROVISIONING_PROFILE_BASE64`,
   `APPSTORE_ISSUER_ID`, `APPSTORE_KEY_ID`, `APPSTORE_API_PRIVATE_KEY`. All generatable on Windows with
   `openssl` — no Mac.
3. **Trigger the iOS build**: push a `v*` tag, or run `release-mobile.yml` via *Run workflow*
   (workflow_dispatch). The hosted `macos-15` job builds, signs and **uploads the `.ipa` to TestFlight**.
4. **Wait for processing** in App Store Connect → **TestFlight**. Export compliance is already answered
   in the build (`ITSAppUsesNonExemptEncryption=false`), so there is no per-build questionnaire.
5. **Add yourself as an internal tester** (App Store Connect → Users and Access, then an Internal group
   in TestFlight), install the **TestFlight** app from the App Store on the iPad Air, and install the
   build from it. New builds then appear automatically once they finish processing.

Then grab the **Team ID** (Apple Developer → Membership) and put it in both
`apple-app-site-association` files (replacing `REPLACE_WITH_APPLE_TEAM_ID`) so Universal Links verify.

> **The production origins are live.** The web front is deployed, so the **privacy / imprint /
> account-deletion URLs are reachable** (`https://secretsanta.circletools.ch/privacy`) and **reveal /
> deep links** resolve at their production origin (`secretsanta.circletools.ch`, ADR-0053) — those
> pages are static WASM and need no backend. The app still installs and runs **offline, with no
> account**, regardless. The one thing to confirm before relying on it in a test is that the **API**
> (`api.circletools.ch`) is serving in production, which is what **account sign-in / sync** needs.

## Track A — Google Play

Start this track **first**. Its critical path is measured in weeks and none of it is work — it is
waiting.

1. **Create the project Google account** (not the personal one) with 2FA and offline recovery codes.
   ✅ Done — `circletools.ch@gmail.com`.
2. **Register in the Play Console** — USD 25, one-time. ✅ Done — account active, no pending checks.
3. **Identity verification** — legal name, address, government ID, phone. Hours to a few days.
   A personal account's contact address becomes **publicly visible** on the listing. ✅ Cleared — the
   console shows no pending verification.
4. **Create the app**, choose free/paid (free; this cannot be changed to paid later), and declare the
   app category and target audience. ⏳ **Next** — the console shows the **Create app** button.
5. **Set up Play App Signing** and generate the **upload key**.
   > Back the upload keystore up to a password manager *and* an offline copy. Play App Signing means
   > a lost upload key can be reset by Google, but losing both the key and the account is terminal.
6. **Build the AAB**, targeting **API 36** — mandatory for new apps and updates from **31 Aug 2026**.
7. **Fill the listing:** DE / EN / FR title, short and full description, feature graphic,
   screenshots per device class (phone + tablet), icon. **The ready-made listing package** — EN/DE/FR
   copy, the 512 icon, the 1024×500 feature graphic, and phone screenshots in all three languages —
   lives in the repo at `store/google-play/` (see its `README.md`); paste from there.
8. **Complete the declarations:** data safety form, privacy-policy URL, content rating
   questionnaire, ads declaration, government-app and financial-features questions.
9. **Closed testing — the long pole.** A personal developer account created after 13 Nov 2023 must
   run a closed test with **12 testers opted in for 14 continuous days** before it may apply for
   production access.
   - "Opted in" means the tester accepted the invitation **and installed the app** under the matching
     Google account. Invited-but-not-installed does not count.
   - Real devices and real Google accounts only — emulators, bots and duplicate accounts do not
     count.
   - The 14-day clock starts only once the release is approved **and** 12 testers have opted in. Any
     drop below 12 is a problem; recruit ~15 for headroom.
   - Practically: this is the family-and-friends circle the suite is being built for anyway. Line
     them up before starting.
10. **Apply for production access** — a three-part application, reviewed in **up to 7 days**.
11. **Publish with a staged rollout** (e.g. 20 % → 50 % → 100 %), watching crash rates in the
    console between steps.

**Elapsed time from zero: roughly 4–6 weeks**, almost all of it steps 9–10.

## Track B — Apple App Store

1. **Create the project Apple ID** with 2FA and offline recovery codes. ✅ Done — `info@circletools.ch`.
2. **Enrol in the Apple Developer Program** as an **Individual** — USD 99/year, identity
   verification. Typically approved in **24–48 hours**, occasionally up to a week. No D-U-N-S number
   is needed for individual enrolment. ⏳ **Pending** — submitted 2026-09-17, awaiting Apple approval.
   > The App Store publishes the individual's **legal name** as the seller. Accepted in
   > [ADR-0030](../adr/0030-mobile-store-publisher-identity.md).
3. **Declare DSA trader status** in App Store Connect. Mandatory for every developer; apps without a
   verified declaration are **removed from the EU App Store**.
4. **Register the App ID / bundle identifier** — permanent, see the blockers above.
5. **Signing (no Mac).** Produce a distribution certificate (`.p12`), an App Store provisioning
   profile, and an App Store Connect API key, then store them as GitHub Actions secrets for the CI
   build ([ADR-0065](../adr/0065-hosted-macos-ios-builds.md),
   [ADR-0050](../adr/0050-secrets-management.md)): `IOS_DIST_CERT_P12_BASE64`,
   `IOS_DIST_CERT_PASSWORD`, `IOS_PROVISIONING_PROFILE_BASE64`, `APPSTORE_ISSUER_ID`,
   `APPSTORE_KEY_ID`, `APPSTORE_API_PRIVATE_KEY`. The certificate and its CSR can be generated on
   Windows with `openssl` — no Mac is involved.
6. **Create the App Store Connect record** and fill the listing in **DE / EN / FR**: name, subtitle,
   description, keywords, support URL, privacy-policy URL.
7. **Screenshots** — required per device class, PNG or JPEG, RGB, **no alpha channel**, exact pixel
   dimensions (App Store Connect rejects off-by-one):

   | Device class | Required size |
   |---|---|
   | iPhone with Dynamic Island, medium display (6.1"/6.3") | 1206 × 2622 or 1179 × 2556 — required since 2026-10; the 6.9" slot is now optional |
   | 13-inch iPad | 2064 × 2752 |

   The iPad set is **required** because the app supports iPad — iPhone-only screenshots block the
   submission. 1–10 per class, per language.
8. **Declarations:** privacy labels (must match what the app actually collects), age rating, export
   compliance (encryption).
9. **Upload the build** from the hosted macOS CI runner (the `ios` job in `release-mobile.yml`,
   [ADR-0065](../adr/0065-hosted-macos-ios-builds.md)) → **TestFlight**. Internal testers get it almost
   immediately; external testers require a beta app review (~24 h). For the step-by-step to reach our
   own iPad this way, see **First milestone** above.
10. **Submit for review** — typically **1–3 days**, longer around major OS releases and holidays.
11. **Release**, manually or automatically on approval.

**Elapsed time from zero: roughly 1–2 weeks** — no Mac needed; the CI signing job is the gating setup.

## Recurring platform deadlines

The obligations that quietly make an app unshippable if ignored — stated as a requirement in
[NFR-007](../requirements/non-functional/NFR-007-store-distributability.md):

- **Apple, every spring:** submissions must be built with the newest SDK. iOS 26 SDK / Xcode 26 has
  been mandatory since **28 Apr 2026**. A newer Xcode implies a newer macOS, which is what makes the
  build host a hardware question.
- **Google, every August:** the target API level floor rises. **API 36 from 31 Aug 2026** for new
  apps and updates; extensions are available to **1 Nov 2026**.

Re-check both at every release and at every .NET/MAUI upgrade.

## Signing & versioning

- `ApplicationDisplayVersion` — the user-visible version (`1.0`, `1.1`), same value on both stores.
- `ApplicationVersion` — the build number; must **increase with every upload** to either store.
- Release channels: **internal → closed → production** (Play) and **TestFlight internal → TestFlight
  external → App Store** (Apple).
- Secrets that must never reach the repository: the Android upload keystore and its passwords, Apple
  certificates and provisioning profiles, and the App Store Connect API key.

### Store-release secrets: lessons from 1.0.28 (2026-11-19)

The signing and store secrets live in the GitHub environment `store-release`. Its first real run
(*Release (mobile)*, 1.0.28) failed three times before it went through. Each time the value in the secret
was wrong; the workflow was fine.

- **iOS, p12 import "MAC verification failed (wrong password?)".** The certificate and its password were
  correct locally (`Get-PfxData`); re-setting both secrets fixed it.
- **Android, `jarsigner` exit code 1.** `keytool -list` said "keystore password was incorrect". The password
  was kept in a password-manager note rendered as Markdown, and the preview hid an escaped character, so the
  copied value was missing it. **Keep passwords in password fields, never in Markdown notes**, and check a
  keystore password with `keytool -list -keystore <file>` before setting the secret.
- **Play upload "invalid_grant: Invalid JWT Signature".** Google rejected the service-account key in
  `PLAY_SERVICE_ACCOUNT_JSON`. First it was a key that had been deleted. Then it was a new key pasted into
  the interactive `gh secret set` prompt, which can break the multi-line JSON. **Set it from the file:**
  `gh secret set PLAY_SERVICE_ACCOUNT_JSON --env store-release --repo CircleTools-ch/circletools-suite < key.json`,
  and check that the file's `private_key_id` is an **active** key on the service account.
- **Order of work.** The repository-level copies of these secrets were deleted before the environment's
  copies had been used once. Next time, **run a release from the new location first**, then delete the old
  copies.
- **Version number.** Read the version from CI (`GitBuildVersionSimple` in the job log) or from the uploaded
  build, not from a local count; the App Store version must match the build.
- A rerun of one job needs the `store-release` approval again, and GitHub refuses to rerun a job while
  another job of the same run is still in progress.

## App preparation — in-repo readiness (2026-09-17)

A store-requirements pass (Apple + Google 2026 rules, tablet/iPad priority) was run against the MAUI
head. What it changed in code, what it confirmed, and what still needs a device or a portal.

**Done in code (`CircleTools.SecretSanta.App`):**

- **App icon & splash are branded** — the burgundy gift-ring `MauiIcon`/`MauiSplashScreen`, not the
  .NET template. The `MauiIcon` background colour makes the generated iOS store icon opaque (Apple
  rejects alpha in the 1024 icon).
- **iOS Universal Links now open the app.** `Platforms/iOS/AppDelegate.cs` handles
  `ContinueUserActivity` (and `OpenUrl`) and forwards the URL to `DeepLinkService` — the counterpart to
  Android's `MainActivity` App Link filters, which previously had no iOS equivalent (reveal/event links
  would have opened the browser). `Platforms/iOS/Entitlements.plist` declares
  `applinks:secretsanta.circletools.ch` and the csproj wires it via `<CodesignEntitlements>` (iOS only).
- **iOS export compliance** — `ITSAppUsesNonExemptEncryption=false` in `Info.plist` (only exempt
  HTTPS/OS crypto), so no per-upload encryption questionnaire.
- **iOS privacy manifest** — `PrivacyInfo.xcprivacy` carries the required-reason APIs incl.
  `UserDefaults`/`CA92.1`, and `NSPrivacyTracking=false` (the app does not track).
- **iPad is a first-class target** — device family 1+2, all four iPad orientations, and
  `UIRequiresFullScreen` deliberately **absent** (so the app is resizable / multitasking-capable, which
  Apple's iOS-26 windowing and the TN3192 deprecation require).
- **Android target API 36 confirmed** in the merged manifest (`targetSdkVersion=36`, `minSdkVersion=24`);
  `MainActivity` is `exported="true"`; permissions are scoped and use **inexact** alarms (no exact-alarm
  Play declaration). Release job builds an **AAB** (`release-mobile.yml`).
- **Tablet layout verified in a browser** at 13" iPad portrait and landscape (the shared
  `SecretSantaShell`): persistent nav drawer, centered readable content, options reflow in landscape,
  no letterboxing or horizontal scroll — an adaptive layout, not an iPhone-scaled one. Satisfies
  Google's adaptive/large-screen quality tier and Apple's "not iPhone-scaled" expectation.

**Must verify on a real device / first TestFlight build (cannot be checked on Windows):**

- **iPad on iPadOS 26.** Confirm the built `.ipa` behaves in Split View / Stage Manager and all four
  orientations, and that MAUI net10 doesn't need a `UIApplicationSceneManifest` for iPadOS 26 (adding
  one would move deep-link handling to a scene delegate — do not add it blind). Highest-consequence
  open item for the iPad priority.
- **16 KB page size** (Play, enforced **1 Feb 2027**): the app ships native `.so` libs, so it is in
  scope. Confirm the .NET 10 release AAB emits 16 KB-aligned libraries (APK Analyzer / `check_elf_alignment`).
- **Passkey affordance on mobile.** Native passkeys are implemented on both heads
  ([ADR-0066](../adr/0066-mobile-native-passkeys.md)) and the server now carries the origin allow-list
  they need ([ADR-0069](../adr/0069-passkey-origin-allow-list-for-native-heads.md)). Three runtime
  prerequisites gate them working on device:
  1. **Deploy the Main API** with `Auth:Passkey:Origins` set (in `appsettings.Production.json`): the web
     origins + `https://circletools.ch` (iOS) + the Android `android:apk-key-hash:<…>` derived from the
     `assetlinks.json` signing-cert SHA-256. Without this, iOS fails with *"invalid origin
     'https://circletools.ch'"*.
  2. **Android:** `circletools.ch/.well-known/assetlinks.json` live over HTTPS with the
     `get_login_creds` relation and a SHA-256 matching the **installed build's** Play App Signing cert —
     a mismatch is the generic *"Passkey could not be created here"* (client-side, before the server).
  3. **iOS:** `apple-app-site-association` `webcredentials` live + Associated Domains capability on the
     App ID/profile (iOS already produced a `circletools.ch` origin on TestFlight, so this appears set —
     confirm at go-live).

**Backup posture — decided ✅ ([ADR-0067](../adr/0067-android-backup-posture.md)):**
`android:allowBackup` is set to **`false`**. Android Auto Backup would silently copy on-device personal
data (member names, assignments) to the user's Google Drive by default, against the local-first,
data-minimised posture (NFR-004); the app's own **export + opt-in sync** are the user-chosen backup path
instead — consistent across Android and iOS. Trade-off accepted: no automatic Google restore on
reinstall / new device (the user migrates via export/sync). The iOS iCloud counterpart is left as a
separate open call (see the ADR).

### `.well-known` association files (host on `secretsanta.circletools.ch`)

Both deep-link mechanisms auto-verify only once these are served over HTTPS (valid cert, no redirects)
from the reveal origin. Neither can be finalised yet: the values need the **Play App Signing** SHA-256
(available after the app is created in Play Console) and the **Apple Team ID** (after enrolment). Serve:

- `/.well-known/assetlinks.json` (Android App Links):

  ```json
  [{ "relation": ["delegate_permission/common.handle_all_urls"],
     "target": { "namespace": "android_app", "package_name": "ch.circletools.secretsanta",
                 "sha256_cert_fingerprints": ["<PLAY_APP_SIGNING_SHA256>"] } }]
  ```

- `/.well-known/apple-app-site-association` (iOS Universal Links; JSON, no extension, `Content-Type:
  application/json`; use the current `components` form):

  ```json
  { "applinks": { "details": [
      { "appIDs": ["<TEAMID>.ch.circletools.secretsanta"],
        "components": [ { "/": "/r*" }, { "/": "/groups/*" },
                        { "/": "/verify-email*" }, { "/": "/reset-password*" },
                        { "/": "/confirm-email-change*" } ] } ] } }
  ```

> **Deep-linked paths.** The app claims these paths on `secretsanta.circletools.ch` so they open in the
> app, not a detached browser tab: `/r*` (reveal links), `/groups/*` (event links), and the account email
> links `/verify-email*`, `/reset-password*`, `/confirm-email-change*` (FR-007/FR-009). Android declares
> them as `MainActivity` intent filters, iOS as the AASA `components` above; both are handled generically
> by `DeepLinkService`. They share the autoVerify prerequisite: until `assetlinks.json` is live and its
> SHA-256 matches the installed build's Play App Signing cert, Android opens them in the browser/chooser
> instead of the app (the exact symptom of an email-confirmation link opening in the browser).

  Also enable the **Associated Domains** capability on the App ID and regenerate the provisioning
  profile, or the entitlement won't be honoured.

### App Review demo notes (paste into App Store Connect / Play review)

Pre-empts the WebView (Apple 4.2) and completeness (2.1) probes: state that the app is **fully
functional offline with no account**, the WebView renders **locally-bundled** UI (not a website), and
account/sync is optional — tell the reviewer no login is required, and how to exercise sync (or supply a
test account).

## Submission checklist

The click-by-click order for the last mile is the [store submission guide](store-submission-guide.md).

- [x] Brand and domain final; `ApplicationId` / bundle id derived from it and set (`ch.circletools.secretsanta`)
- [x] App icon and splash are branded art (not the .NET template)
- [ ] `RevealBaseUrl` in the MAUI head pointed at the deployed origin, not `localhost` — Release defaults to production; verify at build
- [x] Privacy policy + imprint published, reachable, DE/EN/FR *(live at `secretsanta.circletools.ch/privacy`, `/imprint`; WASM-rendered — see the client-side caveat above; light legal review still owed)*
- [x] Account-deletion URL published and reachable *(live at `secretsanta.circletools.ch/delete-account`; in-app deletion exists)*
- [x] `.well-known/assetlinks.json` + `apple-app-site-association` served with the real Play SHA-256 / Apple Team ID *(verified 2026-10-29: HTTP 200, no redirect, both AASA files in Apple's CDN)*
- [x] Apple: Associated Domains capability enabled on the App ID + profile regenerated *(verified 2026-10-29 in the 0.1.129 `.ipa`'s embedded profile)*
- [ ] Main API deployed with `Auth:Passkey:Origins` set (web + iOS `https://circletools.ch` + Android `apk-key-hash`) — passkey origin allow-list ([ADR-0069](../adr/0069-passkey-origin-allow-list-for-native-heads.md))
- [ ] Native passkey create + sign-in device-verified on iOS and Android against the deployed API
- [ ] Store listings and screenshots in DE/EN/FR, per device class **incl. iPad 13"** (mandatory)
- [x] Data safety (Play) / privacy labels (Apple) match actual data collection (email + user content when synced; "not collected" on-device-only) — *Apple label checked against `store/app-store/README.md` and **published** 2026-10-29; Play data-safety form checked 2026-10-29: already matches `store/google-play/README.md` (incl. sync + device key), no re-submit needed*
- [x] DSA trader status declared (Apple: **non-trader**, verified in App Store Connect 2026-10-29 — free, no ads/IAP/income; reasoning in `store/app-store/README.md`; flips to *trader* if ever monetized). Google Play: **non-trader**, shown under *Manage account → Account details → Trader status* (checked 2026-11-10, after a console notice "Declare your trader status by 1 February 2027"; nothing to change)
- [ ] Android targets the current required API level (**36 confirmed**); iOS built with the current required SDK (Xcode 26 / iOS 26 on CI)
- [ ] iPad build tested on iPadOS 26 (Split View / all orientations); 16 KB-aligned native libs verified
- [ ] Upload keystore and signing assets backed up offline
- [ ] Staged rollout plan and a way to watch crashes after release

## Related

- [Device testing](device-testing.md) — what can be built and tested on which machine
- [Web / API / database](web-api-db.md) — the other deployment track
- [ADR-0030](../adr/0030-mobile-store-publisher-identity.md) — publisher identity
- [ADR-0065](../adr/0065-hosted-macos-ios-builds.md) — iOS builds on hosted macOS runners (supersedes ADR-0031)
- [ADR-0031](../adr/0031-ios-build-host.md) — iOS build host (superseded)
- [NFR-007](../requirements/non-functional/NFR-007-store-distributability.md) — platform compliance
- [arc42 ch. 7](../arc42/07-deployment-view.md) — deployment view
