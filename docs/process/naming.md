# Naming — Brand & Domain

The brand is **`CircleTools`**, at **`circletools.ch`** (registered at Infomaniak, **2026-08-13**). It is
also the solution / project / namespace prefix (`CircleTools.AppHost`, `CircleTools.Main.*`, …). The
project ran on a neutral working name `AppSuite` until the brand landed; that token was **renamed away
repo-wide on 2026-08-13** (a single find-replace, as intended). The exploration that led to the name is
kept below as the record.

## Decisions taken
- **Brand (final):** **CircleTools** — chosen 2026-08-13.
- **Suite domain (registered):** **`circletools.ch`** — registered at Infomaniak, 2026-08-13.
- **Derived permanent identifiers:** brand token `CircleTools`; bundle prefix `ch.circletools.*`
  (e.g. `ch.circletools.secretsanta`); passkey relying-party ID `circletools.ch`.
- **Working name (retired):** the neutral `AppSuite` token was renamed to `CircleTools` repo-wide on 2026-08-13.
- **Brand language:** English.
- **TLD:** `.ch`-first (Swiss audience); a matching `.com` is a nice-to-have.
- **App domains:** apps are **subdomains** of the (future) suite domain — e.g.
  `secretsanta.<suite-domain>`. Per-app brand names are decided during each app's idea step.
- **App store naming:** one universal brand name per app (identical in DE/EN/FR and on-device); the
  store name is *brand + localized descriptor* on both stores (amended 2026-11-17); no
  per-language or per-name domains — see [ADR-0057](../adr/0057-app-store-naming-and-localization.md)
  and the dedicated section below.

### Durable identifier scheme (per [ADR-0046](../adr/0046-suite-packaging-and-app-composition.md))

The suite is **federated**: each app is its own installable and its own origin, unified by the shared
account rather than by a container. The identifier scheme below is fixed once, and a new app **claims a
row** — it does not reopen the composition decision. The apex and the bundle prefix are permanent
(RP-IDs do not migrate; bundle ids cannot change after first release).

| Identifier | Value | Scope / permanence |
|---|---|---|
| Passkey relying-party ID | **`circletools.ch`** (apex) | One RP-ID spans every subdomain, so one passkey works across all apps. **Non-migratable.** |
| Web origin — suite hub | **`circletools.ch`** | The Main app: account, dashboard, Circles, SSO authority. A hub, **not** a container that hosts other apps. |
| Web origin — per app | **`<app>.circletools.ch`** | One standalone WASM PWA per app (e.g. `secretsanta.circletools.ch`). |
| Bundle id / `ApplicationId` prefix | **`ch.circletools.*`** | One permanent id per app (e.g. `ch.circletools.secretsanta`). **Permanent after first store release.** |
| Store presence | **one listing per app** | No monolithic container app; no Main mobile head (account lives inside each app's head). |

Reserved but unused: **`ch.circletools.suite`** — a native suite launcher, only if there is ever
demand to enter the suite on mobile before choosing an app (ADR-0046 keeps this open; nothing forces
it).

## App store naming & listing localization (per [ADR-0057](../adr/0057-app-store-naming-and-localization.md), 2026-09-02)

Standing rule for **every** suite app, so each app's branding step inherits it instead of reopening it:

- **One universal, stable visible name per app**, kept in English and **identical across all three
  store locales (DE/EN/FR) and on-device** (`ApplicationTitle` / `CFBundleDisplayName`). It follows the
  brand's English-reads-everywhere principle. The first app's name is **"Secret Santa"**.
- **Per-market discoverability (ASO) lives in the localized listing, not in the name.** The listings
  are DE/EN/FR anyway ([NFR-003](../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)):
  put the local search terms in the localized **Play title** (e.g. `Secret Santa – Wichteln` (DE),
  `Secret Santa – Père Noël secret` (FR)) and the **Apple keyword field** (`Wichteln`,
  `Schrottwichteln`, `Père Noël secret`, `tirage au sort`, …). The visible name stays single.
- **Amended 2026-11-17:** the **App Store Name** follows the Play pattern too — brand first, identical in
  every locale, then a localized descriptor with the local search term (Secret Santa: `– Wichtel ziehen`
  (DE), `– Draw Names` (EN), `– Tirage au sort` (FR)). Apple localizes the name per language and requires
  it to be unique on the store, so check availability before deciding. The on-device name stays the brand
  alone. See the ADR-0057 amendment.
- **A store name is NOT derived from a domain.** A localized name needs **no** new domain — the single
  per-app origin `<app>.circletools.ch` serves every locale. **No per-language or per-name domains** are
  created. Vanity marketing redirects (e.g. `wichteln.ch`) are a separate, optional future decision,
  decoupled from the app's identity, listing and deep links.

This fixes *how* the one per-app brand name relates to the three locales and the domain; *which* name a
given app gets is still chosen at that app's idea/branding step (below).

## Visual identity — brand mark (decided 2026-08-13)

The circle primitive from the naming rationale (below) is now drawn. The system:

- **CircleTools (suite) mark:** a **thin ring enclosing an overlapping trio of people**. The ring is a
  real circular border (it is *not* formed by the figures), drawn in **deep navy-blue `#0a2c6b`** so it
  reads as a quiet tone-on-tone frame on the **navy `#03173d`** app-icon tile; the **white** trio in the
  centre is what carries the mark. Redesigned **2026-09-15** (previously a *ring of five figures*, which
  turned to mush at favicon size) — see the refinement note below and
  [ADR-0059](../adr/0059-brand-mark-ring-and-central-glyph.md).
- **Per-app rule:** each app keeps the **ring + a single central white glyph** and **swaps the glyph**
  plus the **ring colour**. Secret-Santa is a **white wrapped present** (box with a cross ribbon and a
  bow, the ribbon cut out in the ground colour) inside a **maroon `#4a0f18`** ring on the brand
  **burgundy `#7a1f2b`** ground. The maroon ring mirrors CircleTools' deep-navy ring as a recessed,
  tone-on-tone frame, and the white-on-brand glyph blends into the burgundy app bar exactly as the Main
  mark does on navy.
- **Accessibility / stores:** the **central white glyph carries the mark** and clears WCAG AA on every
  ground (white 17.6:1 on navy, 10.2:1 on burgundy); the tone-on-tone ring (`#0a2c6b` / `#4a0f18`) is a
  deliberately low-contrast decorative frame, so legibility never depends on it, and each mark still
  reads when flattened to a single colour (monochrome-safe, per
  [ADR-0024](../adr/0024-ui-styling-and-theming.md)). App icons are full-bleed opaque squares with the
  content inside the adaptive-icon safe zone (no alpha, no pre-rounded corners — the OS applies the
  mask).

**Where the assets live** (source of truth):
- Native (Secret-Santa MAUI): `…/CircleTools.SecretSanta.App/Resources/AppIcon/appicon.svg` (burgundy
  background) + `appiconfg.svg` (white gift inside a maroon ring) + `Splash/splash.svg`, with `MauiIcon Color="#7a1f2b"`.
  Resizetizer rasterises the store sizes at build.
- Web PWAs: each app's `wwwroot/favicon.svg` (+ rasterised `favicon.png` / `icon-192.png` /
  `icon-512.png`) and its manifest `theme_color` / `background_color`.
- Docs: `docfx/templates/circletools/public/logo.svg` + `favicon.svg`, wired via `_appLogoPath` /
  `_appFaviconPath` in `docfx.json`.

Placeholder accents that had diverged from the brand were reconciled to navy `#03173d` / accent
`#3c6fd1` (the DocFX `--bs-primary` was a `#1168bd` placeholder).

**In-app palette (from [ADR-0040](../adr/0040-ui-component-library-mudblazor.md), 2026-08-13):** the
*running app* UI is deliberately **neutral and restrained** — a MudBlazor/Material theme on neutral
surfaces with a single calm accent, plus light/dark. The **per-app brand marks and their festive
colours** (Secret-Santa's burgundy `#7a1f2b` ground with a white gift ring) live on the **app icon,
splash and store/marketing assets** listed above — not on every in-app surface. The mark and asset spec
here is unchanged; only where the saturated colour is applied changed.

**Refinement (2026-08-14):** the Secret-Santa mark was flipped from *coloured gifts on cream* to a
*white gift ring on burgundy* so it reads white-on-brand like the Main mark and blends into the
burgundy app bar. The gift glyph was also redrawn — the earlier box-plus-single-ribbon shape rendered
as a little handled bag at icon size; the current glyph is a box with a full cross ribbon and a compact
bow, legible as a wrapped present down to 24 px. Ring geometry (five nodes) is unchanged.

**Redesign (2026-09-15, [ADR-0059](../adr/0059-brand-mark-ring-and-central-glyph.md)):** the whole mark
system moved from a *ring formed of five glyphs* to a **single circular border enclosing one central
glyph**. The five-glyph ring was busy and illegible at favicon size — the individual figures/gifts
merged into a blob at 16–20 px. The new system keeps the "circle" idea as an explicit ring and lets a
single bold glyph carry the mark: CircleTools = an overlapping **trio of white people** in a **deep
navy-blue `#0a2c6b`** ring on navy; Secret-Santa = the **white wrapped present** (glyph unchanged from
2026-08-14) in a **maroon `#4a0f18`** ring on burgundy. Both rings are recessed, tone-on-tone frames
(darker than the ground), chosen over white/accent rings during the icon exploration for a calmer,
premium look consistent across the two apps. Assets (SVG source + rasterised PNGs + MAUI foreground/
splash) were regenerated at the paths below.

## Chosen brand

> ✅ **Decided 2026-08-13 — `CircleTools`, `circletools.ch` registered at Infomaniak.** The exploration
> below is kept as the record of how the name was reached.

### `Circle` direction — **Circle-Tools** (front-runner from 2026-07-30, **chosen 2026-08-13**)

- **`circletools.ch`** — **registered** (primary). `circle-tools.ch` remains a candidate redirect.
- Why the direction is strong:
  - **"Circle" is already our word.** The suite's central concept is exactly a
    [Circle](../glossary/circle.md) — a family, a household or a group of friends. A brand that says
    the same thing as the ubiquitous language needs no explaining.
  - **It scales down to one person.** A circle of one is still a circle, so tools that are *not*
    meant to be shared fit the brand without a caveat — unlike "family"/"tribe" names.
  - **Free brand mark.** A circle is a primitive: one shape that works as an app icon, as a per-app
    badge (each app = a mark inside the circle), and as a favicon at 16 px.
  - **Reads in all three product languages** — DE/FR speakers parse the English "circle" (cf.
    *Freundeskreis*, *cercle d'amis*) without translation.
- If chosen, the derived permanent identifiers would be: brand token `CircleTools`, suite domain
  `circletools.ch`, bundle prefix `ch.circletools.*`, passkey RP ID `circletools.ch`.
- ⚠️ Open concerns to settle before committing:
  - **Hyphen.** Prefer the un-hyphenated **`circletools.ch`** (also free) as the primary domain and
    keep `circle-tools.ch` as a redirect — hyphens are lost when a name is spoken aloud and are a
    steady source of typos in e-mail addresses.
  - **Word overload.** "Circle" would then mean both the brand *and* the Circles aggregate. Keep the
    glossary rule intact: `Circle` stays the code name of the aggregate, the brand token is
    `CircleTools`; never use bare "Circle" for the suite in code or docs.
  - **Descriptive = weak mark.** "Tools" is generic and hard to protect. Acceptable for a private
    suite; revisit if the project is ever commercialized. The `-kit` / coined variants below are
    stronger marks.
  - `circletools.com` is taken by a third party (`.com` is nice-to-have only).

#### Variations explored (`.ch`, DNS-probed 2026-07-30)

| Candidate | DNS finding | Note |
|-----------|-------------|------|
| `circletools.ch` | no DNS → possibly free | **Recommended primary** — same name, no hyphen |
| `circle-tools.ch` | no DNS → possibly free | Keep as redirect |
| `circlekit.ch` / `circle-kit.ch` | no DNS → possibly free | Warmer than "tools"; "kit" suits a suite of small apps |
| `circli.ch` | no DNS → possibly free | Short, coined, Swiss-diminutive feel (*-li*); most brandable |
| `circlio.ch` | no DNS → possibly free | Coined |
| `kincircle.ch` | no DNS → possibly free | Warm, but re-narrows to family |
| `closecircle.ch` | no DNS → possibly free | Warm and honest about scope |
| `circlewise.ch` | no DNS → possibly free | |
| `circlespace.ch`, `circleworks.ch`, `circlehub.ch`, `circlesuite.ch`, `circleapps.ch`, `circled.ch`, `circles.ch` | no DNS → possibly free | Weaker / more generic |
| `innercircle.ch` | no DNS → possibly free | Idiomatic but connotes exclusion |
| `circly.ch`, `circlet.ch`, `circle.ch`, `thecircle.ch`, `fullcircle.ch`, `incircle.ch`, `circlo.ch`, `circleo.ch`, `circlin.ch`, `circle.tools` | resolves → registered | Not available |
| `circleup.ch`, `mycircle.ch` | NS only → registered/parked | Not available |

> ⚠️ **Availability caveat.** `.ch` has no public WHOIS, so these are **DNS probes only**: a resolving
> name is certainly taken, but "no DNS" only means *not in use* — it can still be registered and
> parked. Confirm any shortlisted name at a Swiss registrar before relying on it.

### `Walf` (previous front-runner — kept on the list)
- **Walf** — a backronym for **W**e **A**re **L**ike **F**amily (captures *friends-as-family*
  alongside families).
  - `walf.ch` appeared available via RDAP/DNS (confirm at a Swiss registrar before relying on it);
    `walf.com` is owned by a third party.
  - ⚠️ Open concern: **pronunciation** of "Walf" — the reason the `Circle` direction now leads.

## Other candidates found free on `.ch` (earlier exploration)
- *Home / nest:* FamNest, OurNest, FamHaus, KinHaus
- *People / tribe:* WeTribe, OurTribe, OurFam
- *Coined:* Kinvo, Famveo, Famoji, Hearthly, KinHub

_Taken (for the record): Famigos/Famigo, Tribely, Framily, Kinly, Nestly, Familio, and most short
single-word `.ch`/`.com` names._

## Why this was a hard gate

> ✅ **Resolved 2026-08-13** by registering `circletools.ch`. The reasoning below is why it could not be
> deferred any further — it now reads as the justification for the identifiers fixed above.

Deferring the brand was cheap while everything was internal. It stops being cheap at the first store
submission, because three things derived from the domain are **permanent or non-migratable**:

- **The bundle identifier / `ApplicationId`** — cannot be changed after an app's first store release.
  Now set to `ch.circletools.secretsanta`; once it ships it is the app's identity forever
  ([NFR-007](../requirements/non-functional/NFR-007-store-distributability.md)).
- **The passkey relying-party ID** — RP IDs do not migrate. Passkeys registered against one domain do
  not work against another, so the domain must be final **before the first real user registers a
  passkey** ([ADR-0023](../adr/0023-local-dev-infrastructure-in-apphost.md)).
- **The published URLs both stores require** — privacy policy, support and account deletion.

The store accounts themselves do **not** wait on this: they can be created and verified on a neutral
address and pointed at a suite-domain role address later
([ADR-0030](../adr/0030-mobile-store-publisher-identity.md)). Only submission is gated.

Tracked as risk **R8** in [arc42 ch. 11](../arc42/11-risks-and-technical-debt.md).

## Milestone 1 — status

✅ Brand confirmed (**CircleTools**) and domain **`circletools.ch`** registered at Infomaniak (2026-08-13).

**Done 2026-08-13:**
- **Repo-wide rename `AppSuite` → `CircleTools`** across code + docs (namespaces, projects, folders,
  solution, CSS classes, JWT issuer, dev mail domain, docfx theme, localStorage keys) and the CLAUDE.md
  naming section.
- **MAUI bundle id set** to `ch.circletools.secretsanta` (was the `com.companyname.appsuite…` template).

**Remaining, gated on deployment (not on the brand):**
- **Passkey RP ID** `circletools.ch`, the Secret-Santa **`RevealBaseUrl`**, and the **CSP** origin —
  set these when the production origin exists (deployment milestone); the local-dev RP-ID/domain stays
  `circletools.test`.
- ✅ App **icon and splash** replaced with the CircleTools / Secret-Santa brand mark (2026-08-13) —
  see *Visual identity* above; native store sizes are generated by resizetizer at build.
- Create the free **`hello@circletools.ch`** (or `support@`) mailbox and point the store / Brevo
  accounts at it ([ADR-0030](../adr/0030-mobile-store-publisher-identity.md) role address).
- Optionally register **`circle-tools.ch`** as a redirect (hyphen-typo insurance).
- Close arc42 risk **R8** (brand gate resolved) and **D7**'s bundle-id half.
