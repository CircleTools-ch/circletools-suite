---
status: Accepted
date: 2026-08-19
deciders: [Adrian]
tags: [architecture, deployment, distribution, packaging, mobile, web, composition]
---

# ADR-0046 — Suite packaging & app composition

## Context and problem statement

The suite is about to be deployed for the first time (milestone 8). Before the first store
submission and the first production origin, one composition question that has so far been answered
only *implicitly, in code* has to be answered *on the record*, because two of its outputs are
**permanent once shipped**: a mobile **bundle identifier cannot change** after a release, and a
**passkey RP-ID does not migrate** ([ADR-0009](0009-authentication-and-passkeys.md),
[NFR-007](../requirements/non-functional/NFR-007-store-distributability.md)).

The question: **is CircleTools one installable/one origin that contains its apps, or is each app its
own installable/own origin, unified by a shared account rather than by a shared container?**

The code already leans one way, without a decision saying so:

- **Web** is *already two origins.* There are two standalone Blazor WASM PWAs — the Main shell
  (`circletools.ch`) and Secret-Santa (`secretsanta.circletools.ch`) — each its own static host
  ([ADR-0005](0005-web-hosting-render-model.md)), and [ADR-0039](0039-cross-app-single-sign-on.md)
  was built **specifically to carry a session across those separate origins**. If the apps were one
  origin, that whole ADR would be pointless. The web answer is effectively already "separate."
- **Mobile** has exactly one MAUI head today — `CircleTools.SecretSanta.App`, with its own
  `ApplicationId = ch.circletools.secretsanta` set at the naming gate. There is **no Main mobile
  head**, and the mobile-parity work (UX-001) put the account/session stack *inside* the
  Secret-Santa head via [`Ui.Account`](0034-shared-account-session-ui-block.md) rather than behind a
  suite container.

So a de-facto per-app shape exists. What is missing is the decision that (a) confirms it as
intentional, (b) states the **store-distribution** consequences the backlog flagged — how many
listings, bundle ids and review cycles the suite carries — and (c) fixes the **identifier namespace**
so the next app does not have to reopen it. This is the one composition item still listed open in the
[ADR register](index.md#depends-on-deployment-genuinely-open).

## Decision drivers

- **Two outputs are irreversible.** A released bundle id and a passkey RP-ID are permanent; getting
  the composition wrong here is a re-release, not an edit ([NFR-007](../requirements/non-functional/NFR-007-store-distributability.md),
  [ADR-0009](0009-authentication-and-passkeys.md)).
- **Consistency with what is already built.** The web is already per-origin, and SSO
  ([ADR-0039](0039-cross-app-single-sign-on.md)) *presupposes* separate origins. The mobile head is
  already per-app. A decision that contradicts both would mean rework, not just a note.
- **Independent release cadence.** Apps ship on their own clock — a Secret-Santa fix in December must
  not require re-reviewing and re-releasing every other app in the suite.
- **Store discoverability & fit.** A store listing is app-specific — its name, icon, screenshots,
  category, keywords and age rating describe *one* app. A single "CircleTools" container listing
  cannot describe Secret-Santa and a future app at once.
- **Local-first, per-app install footprint.** A user who wants only Secret-Santa should install only
  Secret-Santa ([NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md)) — not a
  bundle carrying apps they will never open.
- **Unification without a container.** The suite must still *feel* like one product — one account,
  one sign-in that carries across apps, shared people (Circles), shared identity. That unification is
  already delivered by the shared account + [SSO](0039-cross-app-single-sign-on.md) + Circles, not by
  a shell that hosts app code.
- **Cost of an extra listing is bounded.** Per-app distribution adds review cycles and listing
  upkeep, but **not** repeated developer-account gates — Google Play's 12-testers-×-14-days barrier is
  a one-time gate on the *account's first* production app, not per app
  ([ADR-0030](0030-mobile-store-publisher-identity.md)).

## Considered options

1. **Per-app installs / per-app origins, unified by the shared account (federated suite).** Each app
   is its own PWA on its own `*.circletools.ch` subdomain and its own store listing with its own
   permanent `ch.circletools.<app>` bundle id. The Main app is the suite **hub** (dashboard, account
   home, Circles) — a web app in its own right, **not** a container that hosts other apps' code.
   Unification is the shared account, cross-app SSO, and shared Circles.
2. **Single suite shell (monolith container).** One "CircleTools" installable / one origin that
   embeds every app as an internal module. One store listing, one bundle id, one review cycle, one
   icon on the home screen; apps are routes inside the shell.
3. **Hybrid — suite launcher + per-app installs.** A thin "CircleTools" launcher app in addition to
   each standalone app, so users can enter either through the suite or directly.

## Decision outcome

**Chosen: option 1 — a federated suite. Each app is its own installable and its own web origin, with
its own permanent bundle id; the suite is unified by the shared account, cross-app SSO and Circles,
not by a container.** This ratifies the shape already built and fixes the rules the next app follows.

### What this pins down

- **Web: one PWA per app, one subdomain per app.** `circletools.ch` is the **Main hub** (account,
  dashboard, Circles); `secretsanta.circletools.ch` is Secret-Santa; app N is
  `<app>.circletools.ch`. Each is a standalone static WASM host per
  [ADR-0005](0005-web-hosting-render-model.md). The **passkey RP-ID is the registrable apex
  `circletools.ch`**, so one passkey spans every current and future subdomain
  ([ADR-0009](0009-authentication-and-passkeys.md), [FR-010](../requirements/functional/FR-010-manage-passkeys.md)) —
  this is the reason the apex, not a subdomain, is the RP-ID.
- **The Main app is a hub, not a host.** It owns the account screens, the dashboard and Circles, and
  it is the **SSO authority** ([ADR-0039](0039-cross-app-single-sign-on.md)). It does **not** load or
  embed another app's UI. Apps depend on shared *building blocks* (`Ui.Account`, `Circles.*`,
  `ApiClient`, `Localization`), never on the Main **host**. This keeps the inward-pointing layering
  (Main is just another host) intact.
- **Mobile: one store listing + one permanent bundle id per app.** Secret-Santa is
  `ch.circletools.secretsanta` (already set) on both stores; app N is `ch.circletools.<app>`. There
  is **no monolithic "CircleTools" container app** on the stores, and **no Main mobile head** — on
  mobile the account/session/Circles surface lives *inside each app's head* through the shared blocks,
  exactly as the Secret-Santa head already composes it (UX-001).
- **Identifier namespace is reserved.** `ch.circletools.*` is the bundle-id and package prefix;
  `*.circletools.ch` is the origin scheme; the apex is the RP-ID. A new app claims
  `ch.circletools.<app>` + `<app>.circletools.ch` and inherits this ADR — it does **not** reopen the
  composition question.
- **One shared identity backend, N frontends.** All apps (web and mobile) authenticate against the
  single Main identity API and its one account; per-app installs do **not** mean per-app accounts.

### Why not the single shell

Option 2 is rejected because it **contradicts what is already built and would mean tearing it out**:
it makes [ADR-0039](0039-cross-app-single-sign-on.md)'s cross-origin SSO pointless (there would be
one origin), it couples every app to one release cadence (a December Secret-Santa fix would re-review
the whole suite), and its single store listing cannot carry app-specific name/screenshots/category —
hurting discoverability of each app. It also forces users to install apps they do not want, against
the local-first, minimal-footprint posture. The unification it offers, the suite is **already
getting** from the shared account + SSO + Circles, without a container.

Option 3 (a launcher app) is rejected as a listing, a bundle id, a review cycle and an icon that
**earns nothing the Main *web* hub does not already provide** — the hub is the launcher, reachable at
the apex origin. A native launcher can be revisited if there is ever real demand to enter the suite on
mobile before choosing an app; nothing here forecloses adding one later under `ch.circletools.suite`.

## Consequences

- **Positive:** the decision matches the code — nothing built (two origins, the SSO hand-off, the
  per-app MAUI head, the set bundle id) has to change. It is a ratification, not a migration.
- **Positive:** apps ship independently — their own release cadence, review cycle, versioning and
  crash surface; a bug in one cannot force a re-release of the others.
- **Positive:** each store listing describes exactly one app (name, icon, screenshots, category, age
  rating), which is what the stores and discoverability want.
- **Positive:** users install only what they use; the suite still feels like one product through the
  shared account, one carried sign-in ([ADR-0039](0039-cross-app-single-sign-on.md)) and shared
  Circles.
- **Positive:** the reserved `ch.circletools.*` / `*.circletools.ch` / apex-RP-ID scheme means the
  next app is a **mechanical** addition, not a reopened architecture question.
- **Negative / accepted:** **N apps = N store listings = N review cycles = N sets of store metadata**
  (DE/EN/FR listings, screenshots, privacy declarations per [NFR-007](../requirements/non-functional/NFR-007-store-distributability.md)).
  This is real recurring upkeep; it is the price of independent cadence and per-app discoverability,
  and it scales with the number of apps, which for a hobby suite is small.
- **Negative / accepted:** the suite's cross-app coherence rests on **discipline in the shared blocks**
  rather than being enforced by a single binary — each new head must correctly compose `Ui.Account`,
  Circles and the SSO flow. The mitigation is that these are shared libraries with their own tests,
  not copy-paste per app.
- **Negative / accepted:** there is **no single mobile front door** to the suite. Accepted, because
  the Main **web** hub is that front door, and a per-app store listing is a better mobile entry point
  than a container anyway. Reversible by adding a `ch.circletools.suite` launcher later.
- **Follow-ups:**
  - Record the origin/bundle-id/RP-ID scheme in [naming](../process/naming.md) as the durable
    identifier table (apex = RP-ID; `<app>.circletools.ch`; `ch.circletools.<app>`), and reference it
    from the [mobile runbook](../deployment/mobile-apps.md) and the
    [web/API/DB runbook](../deployment/web-api-db.md).
  - Add the per-app-listing consequence to [NFR-007](../requirements/non-functional/NFR-007-store-distributability.md)'s
    obligations (each app carries its own DE/EN/FR listing, screenshots, privacy declaration and
    account-deletion URL) and to the store checklist in the mobile runbook.
  - Confirm arc42 [ch. 5 (building-block view)](../arc42/05-building-block-view.md) and
    [ch. 7 (deployment view)](../arc42/07-deployment-view.md) describe the federated topology (N
    origins, N heads, one identity backend, hub ≠ host) and add the one-line rule "the Main app is a
    hub, not a container" to [ch. 4 (solution strategy)](../arc42/04-solution-strategy.md).
  - This **closes the last "genuinely open" composition item** in the
    [ADR register](index.md#depends-on-deployment-genuinely-open); mark it closed there.
- **New risks:** none structural — the risk this ADR *removes* is shipping an irreversible identifier
  under an unexamined composition. The residual is listing-upkeep load, which grows only as fast as
  the app count and is visible in every release.

## Pros and cons of the options

### 1. Federated suite — per-app installs/origins, shared account (chosen)
- 👍 Matches everything already built (two origins, SSO, per-app MAUI head, set bundle id); no rework.
- 👍 Independent release cadence, per-app store listings, install-only-what-you-use footprint.
- 👍 Reserved identifier scheme makes the next app mechanical; one account/backend unifies without a container.
- 👎 N apps = N store listings and review cycles = recurring per-app metadata upkeep.
- 👎 Suite coherence depends on shared-block discipline, not on a single enforcing binary.

### 2. Single suite shell (monolith container)
- 👍 One listing, one bundle id, one review cycle, one home-screen icon; simplest store story.
- 👎 Contradicts and would require tearing out the two origins and [ADR-0039](0039-cross-app-single-sign-on.md)'s SSO.
- 👎 Couples every app to one release cadence and one crash surface; one listing can't describe many apps.
- 👎 Forces users to install apps they don't want; against local-first minimal footprint.

### 3. Hybrid — suite launcher + per-app
- 👍 Offers a suite front door on mobile as well as per-app entry.
- 👎 An extra listing/bundle id/review cycle/icon that earns nothing the Main web hub doesn't already provide.
- 👎 A launcher only makes sense once several mobile apps exist; premature now.

## Links

- Related ADRs: [ADR-0005](0005-web-hosting-render-model.md) (standalone WASM PWA per app — the web
  per-origin premise), [ADR-0039](0039-cross-app-single-sign-on.md) (cross-origin SSO — presupposes
  separate origins), [ADR-0009](0009-authentication-and-passkeys.md) (apex RP-ID spanning subdomains),
  [ADR-0034](0034-shared-account-session-ui-block.md) (shared account block that unifies without a
  container), [ADR-0030](0030-mobile-store-publisher-identity.md) (project-owned store accounts;
  one-time Play tester gate), [ADR-0036](0036-cloud-provider-and-hosting.md) (where the origins are
  hosted)
- arc42 chapters: [04 Solution strategy](../arc42/04-solution-strategy.md),
  [05 Building-block view](../arc42/05-building-block-view.md),
  [07 Deployment view](../arc42/07-deployment-view.md)
- Requirements: [NFR-007](../requirements/non-functional/NFR-007-store-distributability.md),
  [NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md),
  [FR-010](../requirements/functional/FR-010-manage-passkeys.md)
- Process: [naming](../process/naming.md) (durable identifier scheme — apex RP-ID, subdomains, bundle-id prefix)
- Runbooks: [Mobile apps](../deployment/mobile-apps.md), [Web / API / Database](../deployment/web-api-db.md)
