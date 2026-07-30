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

### `Circle` direction — **Circle-Tools** (added 2026-07-30, current front-runner)

- **`circle-tools.ch`** — the name that prompted this direction; noted as available.
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
