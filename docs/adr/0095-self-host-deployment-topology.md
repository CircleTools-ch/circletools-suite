---
status: Proposed
date: 2026-11-13
deciders: [Adrian]
tags: [deployment, self-hosting, configuration, docker, security]
---

# ADR-0095 — Self-host deployment: one domain, configuration at runtime, minimal stack

> **Revised 2026-11-20** (review against the code, still Proposed): service-worker and cache rules for
> several apps on one origin; API routing needs no API change; what "mail is optional" means for sign-up
> and password reset; the first admin comes from a setup link, not from being first; the instance admin
> and account invitations are a separate feature spec, with `closed` as the default until it ships.

## Context and problem statement

The production stack ([ADR-0036](0036-cloud-provider-and-hosting.md), `deploy/`) is built
for **our** server:

- three origins (`circletools.ch`, `secretsanta.circletools.ch`, `api.circletools.ch`) with the apex as
  passkey RP-ID ([ADR-0046](0046-suite-packaging-and-app-composition.md));
- the web image bakes each app's `wwwroot/appsettings.Production.json` (API origin, SSO authority, suite
  apps) into the build, and the `Caddyfile` hardcodes site names and the API origin in the CSP;
- the API's CORS list, passkey domain and origins, mail branding and link origins live in its
  `appsettings.Production.json`;
- `compose.yaml` *requires* our operations stack (Brevo, Grafana alerting, ntfy, healthchecks.io) and
  Grafana/cAdvisor/node-exporter;
- the legal pages describe the CircleTools hosted service.

A self-hoster typically has **one hostname** (e.g. `santa.example.net`), often a **Raspberry Pi or NAS**,
and an **existing reverse proxy** (Traefik, Nginx Proxy Manager, Synology, Caddy) that handles TLS.

**How does someone run the suite on their own hardware, under their own domain, without rebuilding
images or editing code?**

## Decision drivers

- No rebuild for a self-hoster: the same published images run our service and theirs.
- One hostname must be enough; several subdomains stay possible (we keep ours).
- Works behind an existing reverse proxy; also works stand-alone with automatic TLS.
- Our own operations stack is optional, never required.
- Secure by default: no default secrets, nothing published that should be internal.
- The design must scale to future apps without a new topology per app.

## Considered options

1. **Single-origin mode + runtime configuration from environment variables, minimal compose file**
2. **Keep subdomains only**, document how to map them to a self-hoster's domain
3. **One all-in-one container** (API + web + Postgres in one image)

## Decision outcome

**Chosen: Option 1.**

**Two topologies, one set of images:**

| Mode | Origins | Used by |
|------|---------|---------|
| `multi-origin` | hub at the apex, one subdomain per app, API on its own subdomain | the hosted service (unchanged) |
| `single-origin` | one host: hub at `/`, each app under `/<app>/` (e.g. `/secretsanta/`), API under `/api/` | self-hosters (default in the self-host compose file) |

In `single-origin` mode all apps share one origin, so CORS is not needed, the CSP's `connect-src`
is `'self'`, and the passkey RP-ID is simply the host. Each WASM app gets a configurable base path
(`<base href>`), and the SSO hand-off ([ADR-0039](0039-cross-app-single-sign-on.md)) becomes a
same-origin navigation.

- **API routing needs no change in the API.** Every public API route already starts with `/api/v…`, so
  the web container's Caddy proxies `/api/*` to the API unchanged (no path stripping, no `PathBase`).
  `/alive` and `/metrics` stay internal, as on the hosted service.
- **The SSO hand-off can usually be skipped.** The apps keep separate refresh-token keys
  (`main.refreshToken`, `secretsanta.refreshToken`), so each app still signs in on its own; on one origin
  the hand-off is a same-origin redirect with no new trust boundary. Whether the apps should instead share
  one sign-in in this mode is left to the base-path work (SH-1), not decided here.
- **Apps on one origin must not interfere with each other.** Today each app's service worker deletes
  every cache starting with `offline-cache-` except its own, and answers *every* navigation in its scope
  with its own `index.html`. On one origin the hub's worker (scope `/`) would delete Secret-Santa's
  offline cache and vice versa, and on a device that has opened the hub, a first visit to `/secretsanta/`
  could be answered with the hub's page. Rules for `single-origin`, which the multi-origin service also
  follows so there is one code path:
  - each app's cache names carry the app id (`offline-cache-<app>-<version>`), and a worker deletes only
    its own old caches;
  - a worker answers navigations with its `index.html` only for paths under its own base path; the hub's
    worker leaves `/<app>/…` and `/api/…` to the network;
  - each app's worker is registered with its base path as scope;
  - browser-storage keys are already app-prefixed (`circletools.secretsanta.*`, `main.*`); shared keys
    (`circletools.themeMode`, `circletools.sso.*`) are reviewed during SH-1 and kept shared only on purpose.

**Configuration at runtime, not at build time:**

- The **web image** generates each app's `appsettings.json` at container start from environment
  variables (e.g. `CIRCLETOOLS_PUBLIC_URL`, `CIRCLETOOLS_MODE`), and renders the CSP from the same values.
  The baked `appsettings.Production.json` files remain as the hosted defaults.
- The **API** derives CORS, passkey RP-ID and origins, mail link origins and the SSO return-uri allow-list
  from the same `CIRCLETOOLS_PUBLIC_URL` (plus optional explicit overrides). Android passkey origins for
  the official app are a documented default ([ADR-0097](0097-passkeys-and-links-on-self-hosted-servers.md)).
- Every value is documented in one configuration reference in `docs/deployment/`.

**Minimal self-host stack (`deploy/selfhost/compose.yaml`):** `postgres`, `api`, `web`. Migrations run
through the same `--migrate` step, offered as an opt-in `CIRCLETOOLS_MIGRATE_ON_START=true` for
convenience (default off for our production, where the explicit step stays). Observability (Loki,
Prometheus, Grafana) is an **optional profile**; ntfy, cAdvisor and node-exporter are not part of it.

**TLS:** the web image serves **plain HTTP on one port** by default for use behind an existing reverse
proxy; setting `CIRCLETOOLS_TLS=auto` enables Caddy's automatic Let's Encrypt for stand-alone use. The
API trusts forwarded headers only from the configured proxy network.

**Mail is optional:** with no SMTP configured, mail-sending features (invitation emails) are switched off
and the UI says so; invitations by link ([ADR-0058](0058-account-free-link-shared-groups.md)) keep
working. Any SMTP server works (not only Brevo).

Mail is also what confirms an account today: sign-up creates a pending account and sends a verification
mail ([FR-007](../requirements/functional/FR-007-create-suite-account.md)), and a forgotten password is
reset by mail ([FR-009](../requirements/functional/FR-009-reset-password.md)). Without mail:

- **Self-service sign-up is off.** `open` registration requires mail; the API refuses to start with
  `CIRCLETOOLS_REGISTRATION=open` and no SMTP, with a clear log message.
- **Accounts are confirmed by link instead.** An account invitation link (below) proves the address the
  same way the verification mail does — whoever holds it may create that account — so an account created
  through one is confirmed at once.
- **Password reset becomes an admin action:** the instance admin issues a one-time reset link and hands it
  over. Passkeys keep working as on the hosted service.

**Registration policy:** `CIRCLETOOLS_REGISTRATION=open|invite|closed`.

- `open` — today's behaviour (needs mail, see above). The hosted service's mode.
- `closed` — nobody can sign up; only accounts that already exist sign in.
- `invite` — an account can only be created through an **account invitation link** that the instance
  admin issues. This is the self-host default once it exists, so a public instance is not an open sign-up
  by accident.

**The first account is created through a setup link, not by being first.** The `setup` command prints a
one-time link that creates the first account and makes it the instance admin; it expires after first use
or 24 hours, and `setup --admin-link` issues a new one. "Whoever reaches a new server first becomes
admin" would hand a freshly started public server to whoever finds it before its operator.

**Account invitations and the instance admin are new features, not settings.** Today the code has no admin
role and no way to invite someone to create an account (invitations exist only to groups and circles).
They get their own feature spec and test plan (admin role, issuing and revoking account invitation and
reset links, what the admin can and cannot see — never other people's groups). Their names go into the
glossary first: **instance admin** and **account invitation** must not be confused with the
[organizer link](../glossary/organizer-link.md) or the superseded [admin link](../glossary/admin-link.md),
which are about organizing one group, not about the server. **Until that feature ships**, the self-host default is
`closed` with the setup link for the first account, so a self-hosted server works for one household
from the start.

**Secrets:** a `setup` command (shell script and documented `openssl rand` equivalents) generates the JWT
signing key, the share-token encryption key ([ADR-0061](0061-share-token-and-creator-key-at-rest-protection.md))
and the email HMAC key ([ADR-0062](0062-email-address-hashing-keyed-hmac.md)) into `.env`. The API keeps
failing closed when they are missing.

**Legal pages:** an operator supplies their own imprint/privacy text (Markdown via a mounted file or URL);
without it the pages say that this is a self-hosted instance and name the operator from configuration.
The CircleTools texts are shown only on the hosted service.

**Future apps** ship in the same images, enabled by `CIRCLETOOLS_APPS=secretsanta,…`. A new app means a new
image version, not a new deployment design.

## Consequences

- **Positive:** a self-hoster needs one hostname, three containers and a `.env`; the hosted service runs the
  same images with different configuration; adding an app is a config change.
- **Negative:** two topologies to test; base-path support touches routing, service worker scope and static
  asset paths in every WASM app; runtime config generation adds startup code to the web image.
- **Follow-ups:** base-path support in the WASM apps, including the service-worker and cache rules above;
  config generation in the web image; API options derived from one public URL; self-host compose file and
  `setup` script (keys and the first-admin setup link); configurable legal pages; registration policy and
  the no-mail rules; a feature spec + test plan for the instance admin and account invitations, with
  glossary entries (**instance admin**, **account invitation**, **operator**) before any code; self-hosting
  guide and configuration reference in `docs/deployment/`; a CI job that boots the self-host stack and runs
  an E2E smoke test against it, which opens the hub first and then an app, so the service-worker rules are
  covered.
- **New risks:** a misconfigured proxy (wrong forwarded headers) breaks passkeys or links — the guide covers
  the common proxies, and the API logs a clear error when the request origin does not match the configured
  public URL.

## Pros and cons of the options

### Single-origin mode + runtime config
- 👍 Fits how people self-host; no rebuild; scales to more apps.
- 👎 Base-path work in every app; two modes to keep green.

### Subdomains only
- 👍 No app changes.
- 👎 Needs wildcard DNS and certificates and a rebuild per domain — most self-hosters stop there.

### All-in-one container
- 👍 Simplest to start.
- 👎 Postgres inside an app image is hard to back up and upgrade; against container best practice.

## Links

- Related ADRs: [ADR-0036](0036-cloud-provider-and-hosting.md), [ADR-0046](0046-suite-packaging-and-app-composition.md),
  [ADR-0039](0039-cross-app-single-sign-on.md), [ADR-0021](0021-web-auth-token-model.md) (CSP),
  [ADR-0049](0049-disable-wasm-fingerprinting-for-csp.md), [ADR-0050](0050-secrets-management.md),
  [ADR-0037](0037-transactional-email-provider.md), [ADR-0051](0051-production-observability-backend.md),
  [ADR-0096](0096-client-server-selection-and-discovery.md), [ADR-0097](0097-passkeys-and-links-on-self-hosted-servers.md),
  [ADR-0098](0098-container-image-distribution.md)
- Deployment: `deploy/README.md`, [web-api-db runbook](../deployment/web-api-db.md)
- Backlog: [Open source & self-hosting](../roadmap/backlog.md#open-source--self-hosting-2026-11-13) (SH-1…SH-9)
