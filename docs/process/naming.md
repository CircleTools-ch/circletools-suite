# Naming — Working Name & Brand Candidates

The solution and documentation use the **neutral working name `AppSuite`** for the app-suite. This
is **not a brand** — it is a placeholder for the solution / project / namespace prefix
(`AppSuite.AppHost`, `AppSuite.Main.*`, …). The real brand is **deliberately deferred**: branding
may still change, so we don't bake it into project or namespace names yet.

## Decisions taken
- **Working name:** `AppSuite` (neutral; used in code + docs until a brand is chosen).
- **Future brand language:** English preferred.
- **TLD:** `.ch`-first (Swiss audience); a matching `.com` is a nice-to-have.
- **App domains:** apps are **subdomains** of the (future) suite domain — e.g.
  `secretsanta.<suite-domain>`. Per-app brand names are decided during each app's idea step.

## Leading brand candidate
- **Walf** — a backronym for **W**e **A**re **L**ike **F**amily (captures *friends-as-family*
  alongside families).
  - `walf.ch` appeared available via RDAP/DNS (confirm at a Swiss registrar before relying on it);
    `walf.com` is owned by a third party.
  - ⚠️ Open concern: **pronunciation** of "Walf" — revisit before committing.

## Other candidates found free on `.ch` (earlier exploration)
- *Home / nest:* FamNest, OurNest, FamHaus, KinHaus
- *People / tribe:* WeTribe, OurTribe, OurFam
- *Coined:* Kinvo, Famveo, Famoji, Hearthly, KinHub

_Taken (for the record): Famigos/Famigo, Tribely, Framily, Kinly, Nestly, Familio, and most short
single-word `.ch`/`.com` names._

## Why this is now a hard gate

Deferring the brand was cheap while everything was internal. It stops being cheap at the first store
submission, because three things derived from the domain are **permanent or non-migratable**:

- **The bundle identifier / `ApplicationId`** — cannot be changed after an app's first store release.
  Ship the current placeholder `com.companyname.appsuite.secretsanta.app` once and it is the app's
  identity forever ([NFR-007](../requirements/non-functional/NFR-007-store-distributability.md)).
- **The passkey relying-party ID** — RP IDs do not migrate. Passkeys registered against one domain do
  not work against another, so the domain must be final **before the first real user registers a
  passkey** ([ADR-0023](../adr/0023-local-dev-infrastructure-in-apphost.md)).
- **The published URLs both stores require** — privacy policy, support and account deletion.

The store accounts themselves do **not** wait on this: they can be created and verified on a neutral
address and pointed at a suite-domain role address later
([ADR-0030](../adr/0030-mobile-store-publisher-identity.md)). Only submission is gated.

Tracked as risk **R8** in [arc42 ch. 11](../arc42/11-risks-and-technical-debt.md).

## Next step (milestone 1)
Choose and confirm the final brand, register the domain, then rename the `AppSuite` working-name
token repo-wide (single find-replace) and record the decision here.
