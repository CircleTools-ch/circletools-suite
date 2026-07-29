# Deployment — Mobile apps (Android / iOS)

How a MAUI head gets from the repository into the two stores: what it costs, what has to happen in
which order, and how long each part actually takes. Built from the
`AppSuite.<App>.App` projects ([arc42 ch. 5](../arc42/05-building-block-view.md)).

For *testing* on the devices we own — before any of this — see [device testing](device-testing.md).

> **Status:** nothing below has been executed yet. This is the runbook to follow at
> [milestone 8](../roadmap/roadmap.md), written up front because two of its steps have long lead
> times and one of them is irreversible.

## Targets

- **Android** → Google Play, signed **AAB**, phone + tablet.
- **iOS** → Apple App Store, signed **IPA**, iPhone + iPad.

## Before anything else: two blockers

**1. The brand and domain must be settled first.** The `ApplicationId` / bundle identifier is
**permanent after the first release**, and it should be derived from the suite domain
(`ch.<suite-domain>.secretsanta` or similar). The head currently carries the template value
`com.companyname.appsuite.secretsanta.app`. The same domain also fixes the passkey relying-party ID,
which likewise cannot migrate ([ADR-0023](../adr/0023-local-dev-infrastructure-in-apphost.md)), plus
the privacy-policy and account-deletion URLs the stores require. See
[naming](../process/naming.md) and [NFR-007](../requirements/non-functional/NFR-007-store-distributability.md).

**2. iOS needs a Mac.** Not "is easier with" — iOS artifacts cannot be produced on Windows at all.
See [ADR-0031](../adr/0031-ios-build-host.md) for which Mac, and why the Intel MacBook Pro is at best
a one-release bridge.

Publisher identity and account ownership are decided in
[ADR-0030](../adr/0030-mobile-store-publisher-identity.md): enrol as a **private individual**, on
**accounts created for the project**, never the personal daily-driver ones.

## Costs

| Item | Cost | Notes |
|---|---|---|
| Apple Developer Program | **USD 99 / year** | recurring; lapses stop distribution |
| Google Play Console | **USD 25 once** | one-time registration fee, no renewal |
| Apple-silicon Mac mini | ~**CHF 650–800** once | the durable iOS build host ([ADR-0031](../adr/0031-ios-build-host.md)) |
| Hosted macOS CI (alternative) | ~**USD 0.062 / minute** | note macOS minutes drain included CI allowances at a **10× multiplier** |
| Suite domain (`.ch`) | ~**CHF 10–20 / year** | required before first submission, see above |
| Cloud hosting for Web/API/DB | open | pending the [cloud-provider ADR](../adr/index.md#open-decisions-backlog) |

Year one, excluding hosting: roughly **CHF 800–950** if the Mac is bought, or **CHF 130–150** if an
existing Mac carries the first release.

## Track A — Google Play

Start this track **first**. Its critical path is measured in weeks and none of it is work — it is
waiting.

1. **Create the project Google account** (not the personal one) with 2FA and offline recovery codes.
2. **Register in the Play Console** — USD 25, one-time.
3. **Identity verification** — legal name, address, government ID, phone. Hours to a few days.
   A personal account's contact address becomes **publicly visible** on the listing.
4. **Create the app**, choose free/paid (free; this cannot be changed to paid later), and declare the
   app category and target audience.
5. **Set up Play App Signing** and generate the **upload key**.
   > Back the upload keystore up to a password manager *and* an offline copy. Play App Signing means
   > a lost upload key can be reset by Google, but losing both the key and the account is terminal.
6. **Build the AAB**, targeting **API 36** — mandatory for new apps and updates from **31 Aug 2026**.
7. **Fill the listing:** DE / EN / FR title, short and full description, feature graphic,
   screenshots per device class (phone + tablet), icon.
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

1. **Create the project Apple ID** with 2FA and offline recovery codes.
2. **Enrol in the Apple Developer Program** as an **Individual** — USD 99/year, identity
   verification. Typically approved in **24–48 hours**, occasionally up to a week. No D-U-N-S number
   is needed for individual enrolment.
   > The App Store publishes the individual's **legal name** as the seller. Accepted in
   > [ADR-0030](../adr/0030-mobile-store-publisher-identity.md).
3. **Declare DSA trader status** in App Store Connect. Mandatory for every developer; apps without a
   verified declaration are **removed from the EU App Store**.
4. **Register the App ID / bundle identifier** — permanent, see the blockers above.
5. **Signing** — certificates, provisioning profiles, and the app's capabilities. Xcode-managed
   signing is sufficient at this scale.
6. **Create the App Store Connect record** and fill the listing in **DE / EN / FR**: name, subtitle,
   description, keywords, support URL, privacy-policy URL.
7. **Screenshots** — required per device class, PNG or JPEG, RGB, **no alpha channel**, exact pixel
   dimensions (App Store Connect rejects off-by-one):

   | Device class | Required size |
   |---|---|
   | 6.9-inch iPhone | 1320 × 2868 (1290 × 2796 and 1260 × 2736 also accepted) |
   | 13-inch iPad | 2064 × 2752 |

   The iPad set is **required** because the app supports iPad — iPhone-only screenshots block the
   submission. 1–10 per class, per language.
8. **Declarations:** privacy labels (must match what the app actually collects), age rating, export
   compliance (encryption).
9. **Upload the build** from the Mac → **TestFlight**. Internal testers get it almost immediately;
   external testers require a beta app review (~24 h).
10. **Submit for review** — typically **1–3 days**, longer around major OS releases and holidays.
11. **Release**, manually or automatically on approval.

**Elapsed time from zero: roughly 1–2 weeks — once a Mac exists.**

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

## Submission checklist

- [ ] Brand and domain final; `ApplicationId` / bundle id derived from it and set
- [ ] App icon and splash replaced (still the .NET template art today)
- [ ] `RevealBaseUrl` in the MAUI head pointed at the deployed origin, not `localhost`
- [ ] Privacy policy + imprint published, reachable, DE/EN/FR
- [ ] Account-deletion URL published and reachable
- [ ] Store listings and screenshots in DE/EN/FR, per device class
- [ ] Data safety (Play) / privacy labels (Apple) match actual data collection
- [ ] DSA trader status declared
- [ ] Android targets the current required API level; iOS built with the current required SDK
- [ ] Upload keystore and signing assets backed up offline
- [ ] Staged rollout plan and a way to watch crashes after release

## Related

- [Device testing](device-testing.md) — what can be built and tested on which machine
- [Web / API / database](web-api-db.md) — the other deployment track
- [ADR-0030](../adr/0030-mobile-store-publisher-identity.md) — publisher identity
- [ADR-0031](../adr/0031-ios-build-host.md) — iOS build host
- [NFR-007](../requirements/non-functional/NFR-007-store-distributability.md) — platform compliance
- [arc42 ch. 7](../arc42/07-deployment-view.md) — deployment view
